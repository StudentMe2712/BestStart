/**
 * Choose a Vibe — Vercel Serverless Function: /api/sync
 * Two-way cloud persistence via Upstash Redis REST API.
 * Validates 24-hour HMAC token before any read/write operation.
 */

const crypto = require('crypto');

const REDIS_KEY = 'choose_a_vibe:couple_state:v1';
const DEFAULT_TOKEN_SECRET = 'choose-a-vibe-default-secret-2026';

function verifyToken(token) {
  if (!token || typeof token !== 'string') return null;
  const parts = token.split('.');
  if (parts.length !== 2) return null;

  const [b64Payload, signature] = parts;
  const secret = process.env.AUTH_TOKEN_SECRET || DEFAULT_TOKEN_SECRET;
  const expectedSig = crypto.createHmac('sha256', secret).update(b64Payload).digest('hex');

  const bufA = crypto.createHash('sha256').update(signature).digest();
  const bufB = crypto.createHash('sha256').update(expectedSig).digest();
  if (!crypto.timingSafeEqual(bufA, bufB)) {
    return null;
  }

  try {
    const raw = Buffer.from(b64Payload, 'base64url').toString('utf8');
    const payload = JSON.parse(raw);
    if (!payload || payload.scope !== 'couple_access') return null;
    if (typeof payload.exp !== 'number' || Date.now() >= payload.exp) return null;
    return payload;
  } catch {
    return null;
  }
}

async function parseBody(req) {
  if (req.body) {
    if (typeof req.body === 'string') {
      try { return JSON.parse(req.body); } catch { return {}; }
    }
    return req.body;
  }
  return new Promise((resolve) => {
    let data = '';
    req.on('data', (chunk) => { data += chunk; });
    req.on('end', () => {
      try { resolve(JSON.parse(data)); } catch { resolve({}); }
    });
    req.on('error', () => resolve({}));
  });
}

function getRedisConfig() {
  const url = process.env.UPSTASH_REDIS_REST_URL || process.env.KV_REST_API_URL;
  const token = process.env.UPSTASH_REDIS_REST_TOKEN || process.env.KV_REST_API_TOKEN;
  if (!url || !token) {
    return null;
  }
  return {
    url: url.replace(/\/+$/, ''),
    token: token.trim()
  };
}

module.exports = async function handler(req, res) {
  // CORS & Security Headers
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
  res.setHeader('Cache-Control', 'no-store, max-age=0');

  if (req.method === 'OPTIONS') {
    res.statusCode = 204;
    return res.end();
  }

  // 1. Authorization Bearer Token Validation
  const authHeader = req.headers['authorization'] || '';
  const match = authHeader.match(/^Bearer\s+(.+)$/i);
  const token = match ? match[1].trim() : null;

  const tokenPayload = verifyToken(token);
  if (!tokenPayload) {
    res.statusCode = 401;
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    return res.end(JSON.stringify({
      ok: false,
      error: 'Требуется авторизация'
    }));
  }

  // 2. Upstash Redis configuration check
  const redis = getRedisConfig();
  if (!redis) {
    // Graceful fallback when environment variables are not yet configured
    res.statusCode = 200;
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    return res.end(JSON.stringify({
      ok: true,
      state: null,
      warning: 'Redis not configured',
      serverTime: Date.now()
    }));
  }

  // 3. GET /api/sync -> Pull state from Upstash Redis
  if (req.method === 'GET') {
    try {
      const upstashUrl = `${redis.url}/get/${REDIS_KEY}`;
      const response = await fetch(upstashUrl, {
        headers: {
          Authorization: `Bearer ${redis.token}`
        }
      });

      if (!response.ok) {
        throw new Error(`Upstash HTTP ${response.status}`);
      }

      const data = await response.json();
      let state = null;
      if (data && data.result !== null && data.result !== undefined) {
        if (typeof data.result === 'string') {
          try {
            state = JSON.parse(data.result);
          } catch {
            state = data.result;
          }
        } else if (typeof data.result === 'object') {
          state = data.result;
        }
      }

      res.statusCode = 200;
      res.setHeader('Content-Type', 'application/json; charset=utf-8');
      return res.end(JSON.stringify({
        ok: true,
        state,
        serverTime: Date.now()
      }));
    } catch (err) {
      console.error('[API/Sync GET Error]', err);
      res.statusCode = 502;
      res.setHeader('Content-Type', 'application/json; charset=utf-8');
      return res.end(JSON.stringify({
        ok: false,
        error: 'Ошибка чтения из облачной БД Upstash'
      }));
    }
  }

  // 4. POST /api/sync -> Push updated state to Upstash Redis
  if (req.method === 'POST') {
    try {
      const body = await parseBody(req);
      const incomingState = body && typeof body.state === 'object' ? body.state : null;

      if (!incomingState) {
        res.statusCode = 400;
        res.setHeader('Content-Type', 'application/json; charset=utf-8');
        return res.end(JSON.stringify({
          ok: false,
          error: 'Поле state обязательно для сохранения'
        }));
      }

      incomingState.updatedAt = Date.now();
      const payloadString = JSON.stringify(incomingState);

      // Save to Upstash Redis via REST API
      const upstashUrl = `${redis.url}/set/${REDIS_KEY}`;
      const upstashRes = await fetch(upstashUrl, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${redis.token}`,
          'Content-Type': 'application/json'
        },
        body: payloadString
      });

      if (!upstashRes.ok) {
        // Fallback to command array POST endpoint if /set/ path is rejected
        const cmdRes = await fetch(redis.url, {
          method: 'POST',
          headers: {
            Authorization: `Bearer ${redis.token}`,
            'Content-Type': 'application/json'
          },
          body: JSON.stringify(['SET', REDIS_KEY, payloadString])
        });
        if (!cmdRes.ok) {
          throw new Error(`Upstash SET failed with HTTP ${cmdRes.status}`);
        }
      }

      res.statusCode = 200;
      res.setHeader('Content-Type', 'application/json; charset=utf-8');
      return res.end(JSON.stringify({
        ok: true,
        updatedAt: incomingState.updatedAt
      }));
    } catch (err) {
      console.error('[API/Sync POST Error]', err);
      res.statusCode = 502;
      res.setHeader('Content-Type', 'application/json; charset=utf-8');
      return res.end(JSON.stringify({
        ok: false,
        error: 'Ошибка сохранения в облачную БД Upstash'
      }));
    }
  }

  // Other methods
  res.statusCode = 405;
  res.setHeader('Content-Type', 'application/json; charset=utf-8');
  return res.end(JSON.stringify({ ok: false, error: 'Метод не поддерживается' }));
};
