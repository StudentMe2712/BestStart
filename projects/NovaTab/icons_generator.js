const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const ICONS_DIR = path.join(__dirname, 'icons');
const ROOT_DIR = __dirname;

if (!fs.existsSync(ICONS_DIR)) {
  fs.mkdirSync(ICONS_DIR, { recursive: true });
}

// 1. Generate SVG icon
const svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128" width="100%" height="100%">
  <defs>
    <linearGradient id="bgGrad" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#4F46E5"/>
      <stop offset="50%" stop-color="#6366F1"/>
      <stop offset="100%" stop-color="#9333EA"/>
    </linearGradient>
    <linearGradient id="accentGrad" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#38BDF8"/>
      <stop offset="100%" stop-color="#818CF8"/>
    </linearGradient>
    <filter id="glow" x="-20%" y="-20%" width="140%" height="140%">
      <feGaussianBlur stdDeviation="3" result="blur"/>
      <feComposite in="SourceGraphic" in2="blur" operator="over"/>
    </filter>
  </defs>
  <rect x="6" y="6" width="116" height="116" rx="26" fill="url(#bgGrad)" stroke="#A855F7" stroke-width="2" stroke-opacity="0.5"/>
  <rect x="8" y="8" width="112" height="56" rx="24" fill="white" fill-opacity="0.08"/>
  <path d="M40 28 C40 25.79 41.79 24 44 24 L84 24 C86.21 24 88 25.79 88 28 L88 98 C88 100.8 84.8 102.5 82.5 100.8 L64 87 L45.5 100.8 C43.2 102.5 40 100.8 40 98 Z" fill="#FFFFFF" fill-opacity="0.95" filter="url(#glow)"/>
  <path d="M48 30 L80 30 C81.1 30 82 30.9 82 32 L82 86 L64 73 L46 86 L46 32 C46 30.9 46.9 30 48 30 Z" fill="url(#bgGrad)"/>
  <g transform="translate(64, 52) scale(1.1)">
    <path d="M0 -18 Q2 -4 16 0 Q2 4 0 18 Q-2 4 -16 0 Q-2 -4 0 -18 Z" fill="#F8FAFC"/>
    <path d="M0 -10 Q1.5 -2.5 9 0 Q1.5 2.5 0 10 Q-1.5 2.5 -9 0 Q-1.5 -2.5 0 -10 Z" transform="rotate(45)" fill="#38BDF8" fill-opacity="0.9"/>
    <circle cx="0" cy="0" r="3" fill="#FFFFFF"/>
  </g>
</svg>`;

fs.writeFileSync(path.join(ICONS_DIR, 'icon.svg'), svgContent, 'utf8');
fs.writeFileSync(path.join(ROOT_DIR, 'icon.svg'), svgContent, 'utf8');
console.log('Saved icon.svg');

// 2. Pure Node.js PNG encoder
function crc32(buf) {
  let crc = 0 ^ (-1);
  for (let i = 0; i < buf.length; i++) {
    crc = (crc >>> 8) ^ table[(crc ^ buf[i]) & 0xFF];
  }
  return (crc ^ (-1)) >>> 0;
}
const table = new Uint32Array(256);
for (let i = 0; i < 256; i++) {
  let c = i;
  for (let k = 0; k < 8; k++) {
    c = ((c & 1) ? (0xEDB88320 ^ (c >>> 1)) : (c >>> 1));
  }
  table[i] = c;
}

function makePNG(width, height, rgbaBuffer) {
  const signature = Buffer.from([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
  
  const ihdrData = Buffer.alloc(13);
  ihdrData.writeUInt32BE(width, 0);
  ihdrData.writeUInt32BE(height, 4);
  ihdrData[8] = 8;
  ihdrData[9] = 6;
  ihdrData[10] = 0;
  ihdrData[11] = 0;
  ihdrData[12] = 0;
  
  const ihdrChunk = Buffer.alloc(8 + 13 + 4);
  ihdrChunk.writeUInt32BE(13, 0);
  ihdrChunk.write('IHDR', 4);
  ihdrData.copy(ihdrChunk, 8);
  const ihdrCrc = crc32(Buffer.concat([Buffer.from('IHDR'), ihdrData]));
  ihdrChunk.writeUInt32BE(ihdrCrc, 21);

  const scanlines = Buffer.alloc(height * (1 + width * 4));
  for (let y = 0; y < height; y++) {
    scanlines[y * (1 + width * 4)] = 0;
    rgbaBuffer.copy(scanlines, y * (1 + width * 4) + 1, y * width * 4, (y + 1) * width * 4);
  }

  const compressed = zlib.deflateSync(scanlines, { level: 9 });
  const idatChunk = Buffer.alloc(8 + compressed.length + 4);
  idatChunk.writeUInt32BE(compressed.length, 0);
  idatChunk.write('IDAT', 4);
  compressed.copy(idatChunk, 8);
  const idatCrc = crc32(Buffer.concat([Buffer.from('IDAT'), compressed]));
  idatChunk.writeUInt32BE(idatCrc, 8 + compressed.length);

  const iendChunk = Buffer.alloc(12);
  iendChunk.writeUInt32BE(0, 0);
  iendChunk.write('IEND', 4);
  const iendCrc = crc32(Buffer.from('IEND'));
  iendChunk.writeUInt32BE(iendCrc, 8);

  return Buffer.concat([signature, ihdrChunk, idatChunk, iendChunk]);
}

function pointInRoundedRect(x, y, rx, ry, rw, rh, rad) {
  if (x < rx || x > rx + rw || y < ry || y > ry + rh) return false;
  if (x >= rx + rad && x <= rx + rw - rad) return true;
  if (y >= ry + rad && y <= ry + rh - rad) return true;
  let cx = (x < rx + rad) ? rx + rad : rx + rw - rad;
  let cy = (y < ry + rad) ? ry + rad : ry + rh - rad;
  let dx = x - cx;
  let dy = y - cy;
  return (dx * dx + dy * dy) <= (rad * rad);
}

function pointInBookmark(x, y, bx, by, bw, bh) {
  if (x < bx || x > bx + bw || y < by || y > by + bh) return false;
  const notchHeight = bh * 0.22;
  if (y > by + bh - notchHeight) {
    const midX = bx + bw / 2;
    const normX = Math.abs(x - midX) / (bw / 2);
    const allowedY = (by + bh - notchHeight) + (normX * notchHeight);
    return y <= allowedY;
  }
  return true;
}

function pointInStar(x, y, cx, cy, r) {
  const dx = Math.abs(x - cx);
  const dy = Math.abs(y - cy);
  if (dx + dy > r * 1.5) return false;
  if (dx === 0 && dy === 0) return true;
  return (Math.pow(dx / r, 0.55) + Math.pow(dy / r, 0.55)) <= 1.0;
}

function renderIcon(size) {
  const scale = 4;
  const w = size * scale;
  const h = size * scale;
  const subBuffer = new Uint8ClampedArray(w * h * 4);

  const pad = w * 0.05;
  const rectRad = w * 0.22;

  for (let y = 0; y < h; y++) {
    const normY = y / h;
    for (let x = 0; x < w; x++) {
      const normX = x / w;
      const idx = (y * w + x) * 4;

      if (!pointInRoundedRect(x, y, pad, pad, w - 2 * pad, h - 2 * pad, rectRad)) {
        subBuffer[idx + 0] = 0;
        subBuffer[idx + 1] = 0;
        subBuffer[idx + 2] = 0;
        subBuffer[idx + 3] = 0;
        continue;
      }

      const gradT = (normX + normY) / 2;
      let r = 79 * (1 - gradT) + 147 * gradT;
      let g = 70 * (1 - gradT) + 51 * gradT;
      let b = 229 * (1 - gradT) + 234 * gradT;
      let a = 255;

      if (!pointInRoundedRect(x, y, pad + 2 * scale, pad + 2 * scale, w - 2 * (pad + 2 * scale), h - 2 * (pad + 2 * scale), rectRad - 2 * scale)) {
        r = Math.min(255, r + 40);
        g = Math.min(255, g + 30);
        b = Math.min(255, b + 40);
      }

      const bmW = w * 0.44;
      const bmH = h * 0.62;
      const bmX = (w - bmW) / 2;
      const bmY = h * 0.18;

      if (pointInBookmark(x, y, bmX, bmY, bmW, bmH)) {
        r = 250; g = 250; b = 255;
        const innerPad = Math.max(1.5 * scale, 2);
        const inX = bmX + innerPad;
        const inY = bmY + innerPad;
        const inW = bmW - 2 * innerPad;
        const inH = bmH - innerPad;

        if (pointInBookmark(x, y, inX, inY, inW, inH)) {
          const inT = y / (inY + inH);
          r = 30 * (1 - inT) + 40 * inT;
          g = 27 * (1 - inT) + 35 * inT;
          b = 75 * (1 - inT) + 95 * inT;

          const starCX = w / 2;
          const starCY = bmY + bmH * 0.38;
          const starR = bmW * 0.38;

          if (pointInStar(x, y, starCX, starCY, starR)) {
            const distFromStar = Math.sqrt((x - starCX)**2 + (y - starCY)**2);
            const starGlow = 1 - Math.min(1, distFromStar / starR);
            r = Math.min(255, 240 + 15 * starGlow);
            g = Math.min(255, 245 + 10 * starGlow);
            b = 255;
          } else {
            const d = Math.sqrt((x - starCX)**2 + (y - starCY)**2);
            if (d < starR * 0.2) {
              r = 255; g = 255; b = 255;
            }
          }
        }
      }

      subBuffer[idx + 0] = r;
      subBuffer[idx + 1] = g;
      subBuffer[idx + 2] = b;
      subBuffer[idx + 3] = a;
    }
  }

  const finalBuffer = Buffer.alloc(size * size * 4);
  const s2 = scale * scale;

  for (let py = 0; py < size; py++) {
    for (let px = 0; px < size; px++) {
      let sumR = 0, sumG = 0, sumB = 0, sumA = 0;
      for (let sy = 0; sy < scale; sy++) {
        for (let sx = 0; sx < scale; sx++) {
          const sIdx = ((py * scale + sy) * w + (px * scale + sx)) * 4;
          sumR += subBuffer[sIdx + 0];
          sumG += subBuffer[sIdx + 1];
          sumB += subBuffer[sIdx + 2];
          sumA += subBuffer[sIdx + 3];
        }
      }
      const outIdx = (py * size + px) * 4;
      finalBuffer[outIdx + 0] = Math.round(sumR / s2);
      finalBuffer[outIdx + 1] = Math.round(sumG / s2);
      finalBuffer[outIdx + 2] = Math.round(sumB / s2);
      finalBuffer[outIdx + 3] = Math.round(sumA / s2);
    }
  }

  return makePNG(size, size, finalBuffer);
}

[16, 32, 48, 128].forEach(size => {
  const pngBuf = renderIcon(size);
  const outPath = path.join(ICONS_DIR, `icon${size}.png`);
  fs.writeFileSync(outPath, pngBuf);
  console.log(`Generated ${outPath} (${pngBuf.length} bytes)`);
});
