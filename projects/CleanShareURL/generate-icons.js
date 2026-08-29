const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

function createPng(width, height, drawFn) {
  // RGBA buffer with filter byte per row
  const rowBytes = width * 4;
  const rawBuffer = Buffer.alloc((rowBytes + 1) * height);
  
  for (let y = 0; y < height; y++) {
    const rowOffset = y * (rowBytes + 1);
    rawBuffer[rowOffset] = 0; // Filter: None
    
    for (let x = 0; x < width; x++) {
      const pxOffset = rowOffset + 1 + x * 4;
      const [r, g, b, a] = drawFn(x, y, width, height);
      rawBuffer[pxOffset] = r;
      rawBuffer[pxOffset + 1] = g;
      rawBuffer[pxOffset + 2] = b;
      rawBuffer[pxOffset + 3] = a;
    }
  }

  const compressedData = zlib.deflateSync(rawBuffer);

  // PNG Signature
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);

  // Helper to make chunk
  function makeChunk(type, data) {
    const typeBuf = Buffer.from(type, 'ascii');
    const len = data.length;
    const lenBuf = Buffer.alloc(4);
    lenBuf.writeUInt32BE(len, 0);

    const crcBuf = Buffer.alloc(4);
    const crc = crc32(Buffer.concat([typeBuf, data]));
    crcBuf.writeUInt32BE(crc >>> 0, 0);

    return Buffer.concat([lenBuf, typeBuf, data, crcBuf]);
  }

  // IHDR chunk
  const ihdrData = Buffer.alloc(13);
  ihdrData.writeUInt32BE(width, 0);
  ihdrData.writeUInt32BE(height, 4);
  ihdrData.writeUInt8(8, 8); // 8-bit depth
  ihdrData.writeUInt8(6, 9); // Color type 6 (RGBA)
  ihdrData.writeUInt8(0, 10); // Compression method
  ihdrData.writeUInt8(0, 11); // Filter method
  ihdrData.writeUInt8(0, 12); // Interlace method
  const ihdrChunk = makeChunk('IHDR', ihdrData);

  // IDAT chunk
  const idatChunk = makeChunk('IDAT', compressedData);

  // IEND chunk
  const iendChunk = makeChunk('IEND', Buffer.alloc(0));

  return Buffer.concat([signature, ihdrChunk, idatChunk, iendChunk]);
}

// Simple CRC32 implementation
function crc32(buf) {
  let crc = 0 ^ (-1);
  for (let i = 0; i < buf.length; i++) {
    crc = (crc >>> 8) ^ crcTable[(crc ^ buf[i]) & 0xFF];
  }
  return (crc ^ (-1)) >>> 0;
}

const crcTable = new Uint32Array(256);
for (let i = 0; i < 256; i++) {
  let c = i;
  for (let k = 0; k < 8; k++) {
    c = ((c & 1) ? (0xEDB88320 ^ (c >>> 1)) : (c >>> 1));
  }
  crcTable[i] = c >>> 0;
}

// Drawing logic: Emerald Green shield / clean link gradient with sparkle
function drawIcon(x, y, w, h) {
  const cx = w / 2;
  const cy = h / 2;
  const r = (w / 2) - (w * 0.04);
  const dist = Math.hypot(x - cx + 0.5, y - cy + 0.5);

  if (dist > r) {
    return [0, 0, 0, 0]; // transparent outside circle
  }

  // Emerald/Teal gradient (#059669 to #0d9488)
  const grad = (x + y) / (w + h);
  const bgR = Math.round(5 * (1 - grad) + 13 * grad);
  const bgG = Math.round(150 * (1 - grad) + 148 * grad);
  const bgB = Math.round(105 * (1 - grad) + 136 * grad);

  const normX = (x / w) * 100;
  const normY = (y / h) * 100;

  // Clean Sparkle / Star in center (4-point star)
  const dx = Math.abs(normX - 50);
  const dy = Math.abs(normY - 50);

  // Sparkle shape formula: dx^0.5 + dy^0.5 < threshold
  const starDist = Math.pow(dx, 0.6) + Math.pow(dy, 0.6);
  if (starDist < 5.2) {
    return [255, 255, 255, 255]; // Pure white star in center
  }

  // Minor sparkles top-right
  const dx2 = Math.abs(normX - 74);
  const dy2 = Math.abs(normY - 26);
  const starDist2 = Math.pow(dx2, 0.6) + Math.pow(dy2, 0.6);
  if (starDist2 < 3.0) {
    return [255, 255, 255, 230];
  }

  return [bgR, bgG, bgB, 255];
}

const iconsDir = path.join(__dirname, 'icons');
if (!fs.existsSync(iconsDir)) {
  fs.mkdirSync(iconsDir, { recursive: true });
}

[16, 48, 128].forEach(size => {
  const png = createPng(size, size, drawIcon);
  fs.writeFileSync(path.join(iconsDir, `icon${size}.png`), png);
  console.log(`Generated icon${size}.png (${size}x${size})`);
});
