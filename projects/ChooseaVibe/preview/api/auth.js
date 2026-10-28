/**
 * Choose a Vibe — Vercel Serverless Function: /api/auth
 * Handles 24-hour HMAC token issuance & verification.
 * Zero external dependencies (uses native Node.js crypto).
 */

const crypto = require('crypto');

const DEFAULT_MASTER_PASSWORD = 'Vibe2026!';
const DEFAULT_TOKEN_SECRET = 'choose-a-vibe-default-secret-2026';
const TOKEN_TTL_MS = 86400000; // 24 hours in milliseconds

function safeCompare(a, b) {
  if (typeof a !== 'string' || typeof b !== 'string') return false;
  const bufA = crypto.createHash('sha256').update(a).digest();
  const bufB = crypto.createHash('sha256').update(b).digest();
  return crypto.timingSafeEqual(bufA, bufB);
}

function createToken() {
  const secret = process.env.AUTH_TOKEN_SECRET || DEFAULT_TOKEN_SECRET;
  const payload = {
    exp: Date.now() + TOKEN_TTL_MS,
    scope: 'couple_access'
  };
  const b64Payload = Buffer.from(JSON.stringify(payload), 'utf8').toString('base64url');
  const signature = crypto.createHmac('sha256', secret).update(b64Payload).digest('hex');
  return {
    token: `${b64Payload}.${signature}`,
    expiresAt: payload.exp
  };
}

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

  // 1. GET /api/auth -> Verify token session
  if (req.method === 'GET') {
    const authHeader = req.headers['authorization'] || '';
    const match = authHeader.match(/^Bearer\s+(.+)$/i);
    const token = match ? match[1].trim() : null;

    const payload = verifyToken(token);
    if (!payload) {
      res.statusCode = 401;
      res.setHeader('Content-Type', 'application/json; charset=utf-8');
      return res.end(JSON.stringify({
        ok: false,
        error: 'Сессия истекла или недействительна'
      }));
    }

    const remainingMs = Math.max(0, payload.exp - Date.now());
    res.statusCode = 200;
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    return res.end(JSON.stringify({
      ok: true,
      valid: true,
      expiresAt: payload.exp,
      remainingMs
    }));
  }

  // 2. POST /api/auth -> Authenticate with master password
  if (req.method === 'POST') {
    const body = await parseBody(req);
    const candidate = typeof body.password === 'string' ? body.password.trim() : '';
    const masterPassword = (process.env.APP_MASTER_PASSWORD || DEFAULT_MASTER_PASSWORD).trim();

    if (!candidate || !safeCompare(candidate, masterPassword)) {
      res.statusCode = 401;
      res.setHeader('Content-Type', 'application/json; charset=utf-8');
      return res.end(JSON.stringify({
        ok: false,
        error: 'Неверный пароль доступа'
      }));
    }

    const { token, expiresAt } = createToken();
    res.statusCode = 200;
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    return res.end(JSON.stringify({
      ok: true,
      token,
      expiresAt
    }));
  }

  // Other methods
  res.statusCode = 405;
  res.setHeader('Content-Type', 'application/json; charset=utf-8');
  return res.end(JSON.stringify({ ok: false, error: 'Метод не поддерживается' }));
};
