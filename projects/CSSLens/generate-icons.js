/**
 * CSS Lens - Icon Generator
 * Generates icon16.png, icon48.png, icon128.png using pure Node.js (zlib + fs)
 * Design: High-contrast modern gradient with magnifying lens & color picker glyph
 */

const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

// CRC32 implementation for PNG chunks
function makeCrcTable() {
  const table = [];
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) {
      if (c & 1) {
        c = 0xedb88320 ^ (c >>> 1);
      } else {
        c = c >>> 1;
      }
    }
    table[n] = c >>> 0;
  }
  return table;
}

const crcTable = makeCrcTable();

function crc32(buf) {
  let crc = 0 ^ (-1);
  for (let i = 0; i < buf.length; i++) {
    crc = (crc >>> 8) ^ crcTable[(crc ^ buf[i]) & 0xff];
  }
  return (crc ^ (-1)) >>> 0;
}

function createChunk(type, data) {
  const typeBuf = Buffer.from(type, 'ascii');
  const lengthBuf = Buffer.alloc(4);
  lengthBuf.writeUInt32BE(data.length, 0);

  const toCrc = Buffer.concat([typeBuf, data]);
  const crcVal = crc32(toCrc);
  const crcBuf = Buffer.alloc(4);
  crcBuf.writeUInt32BE(crcVal, 0);

  return Buffer.concat([lengthBuf, toCrc, crcBuf]);
}

function generatePngBuffer(width, height, drawPixel) {
  // Raw RGBA scanlines: (1 byte filter (0) + width * 4 bytes) per row
  const rowLength = 1 + width * 4;
  const rawData = Buffer.alloc(rowLength * height);

  for (let y = 0; y < height; y++) {
    const rowOffset = y * rowLength;
    rawData[rowOffset] = 0; // Filter type 0 (None)

    for (let x = 0; x < width; x++) {
      const pixelOffset = rowOffset + 1 + x * 4;
      const [r, g, b, a] = drawPixel(x, y, width, height);
      rawData[pixelOffset] = Math.max(0, Math.min(255, Math.round(r)));
      rawData[pixelOffset + 1] = Math.max(0, Math.min(255, Math.round(g)));
      rawData[pixelOffset + 2] = Math.max(0, Math.min(255, Math.round(b)));
      rawData[pixelOffset + 3] = Math.max(0, Math.min(255, Math.round(a)));
    }
  }

  const compressedData = zlib.deflateSync(rawData);

  // PNG Header
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);

  // IHDR chunk
  const ihdrData = Buffer.alloc(13);
  ihdrData.writeUInt32BE(width, 0);
  ihdrData.writeUInt32BE(height, 4);
  ihdrData[8] = 8; // bit depth
  ihdrData[9] = 6; // color type: 6 (RGBA)
  ihdrData[10] = 0; // compression
  ihdrData[11] = 0; // filter
  ihdrData[12] = 0; // interlace

  const ihdrChunk = createChunk('IHDR', ihdrData);
  const idatChunk = createChunk('IDAT', compressedData);
  const iendChunk = createChunk('IEND', Buffer.alloc(0));

  return Buffer.concat([signature, ihdrChunk, idatChunk, iendChunk]);
}

/**
 * Pixel drawing logic for CSS Lens icon:
 * - Rounded rect background with sleek indigo-to-cyan gradient
 * - Circular magnifying lens rim with glowing neon stroke
 * - Center crosshair / picker dot with vibrant color
 * - Handle extending down-right
 */
function drawIconPixel(x, y, size) {
  const cx = size / 2;
  const cy = size / 2;
  const radius = size * 0.44;
  const cornerRadius = size * 0.22;

  // Check rounded rectangle bounds for background
  const dxCorner = Math.max(0, Math.abs(x - cx) - (size / 2 - cornerRadius));
  const dyCorner = Math.max(0, Math.abs(y - cy) - (size / 2 - cornerRadius));
  const distFromCorner = Math.sqrt(dxCorner * dxCorner + dyCorner * dyCorner);

  let bgAlpha = 1.0;
  if (distFromCorner > cornerRadius) {
    bgAlpha = 0.0;
  } else if (distFromCorner > cornerRadius - 1.0) {
    bgAlpha = cornerRadius - distFromCorner;
  }

  if (bgAlpha <= 0) {
    return [0, 0, 0, 0];
  }

  // Base background gradient: Deep navy to electric indigo (#0d1117 -> #1e1b4b -> #312e81)
  const normY = y / size;
  const normX = x / size;
  let bgR = 15 + normY * 25 + normX * 10;
  let bgG = 23 + normY * 15 + normX * 30;
  let bgB = 42 + normY * 70 + normX * 80;

  // Lens geometry:
  // Center of lens is shifted slightly top-left
  const lensCx = size * 0.42;
  const lensCy = size * 0.42;
  const lensRadius = size * 0.26;
  const lensThick = Math.max(1.5, size * 0.07);

  const dLens = Math.sqrt((x - lensCx) ** 2 + (y - lensCy) ** 2);

  // Handle geometry: from bottom-right of lens to bottom-right corner
  const handleStartX = lensCx + lensRadius * 0.707;
  const handleStartY = lensCy + lensRadius * 0.707;
  const handleEndX = size * 0.82;
  const handleEndY = size * 0.82;

  // Distance to handle line segment
  const hx = handleEndX - handleStartX;
  const hy = handleEndY - handleStartY;
  const hLen2 = hx * hx + hy * hy;
  let t = ((x - handleStartX) * hx + (y - handleStartY) * hy) / hLen2;
  t = Math.max(0, Math.min(1, t));
  const projX = handleStartX + t * hx;
  const projY = handleStartY + t * hy;
  const distToHandle = Math.sqrt((x - projX) ** 2 + (y - projY) ** 2);
  const handleThick = Math.max(1.5, size * 0.065);

  let finalR = bgR;
  let finalG = bgG;
  let finalB = bgB;
  let finalA = bgAlpha * 255;

  // Draw Lens Interior glass (subtle cyan tint)
  if (dLens < lensRadius - lensThick / 2) {
    const glassBlend = 0.28;
    finalR = finalR * (1 - glassBlend) + 0 * glassBlend;
    finalG = finalG * (1 - glassBlend) + 210 * glassBlend;
    finalB = finalB * (1 - glassBlend) + 255 * glassBlend;
  }

  // Draw Lens Rim (Vibrant Cyan-Blue Gradient)
  const rimDist = Math.abs(dLens - lensRadius);
  if (rimDist < lensThick) {
    const rimBlend = Math.max(0, 1 - rimDist / lensThick);
    // Cyan to blue gradient
    const rRim = 0 + normX * 40;
    const gRim = 242;
    const bRim = 254;
    finalR = finalR * (1 - rimBlend) + rRim * rimBlend;
    finalG = finalG * (1 - rimBlend) + gRim * rimBlend;
    finalB = finalB * (1 - rimBlend) + bRim * rimBlend;
  }

  // Draw Handle
  if (distToHandle < handleThick && t > 0.05) {
    const hBlend = Math.max(0, 1 - distToHandle / handleThick);
    const hR = 56 + t * 100;
    const hG = 189 + t * 50;
    const hB = 248;
    finalR = finalR * (1 - hBlend) + hR * hBlend;
    finalG = finalG * (1 - hBlend) + hG * hBlend;
    finalB = finalB * (1 - hBlend) + hB * hBlend;
  }

  // Center crosshair / color pipette dot (Glowing Magenta / Purple core)
  const centerDotDist = Math.sqrt((x - lensCx) ** 2 + (y - lensCy) ** 2);
  const dotRadius = Math.max(1.2, size * 0.075);
  if (centerDotDist < dotRadius + 1.0) {
    const dotBlend = Math.max(0, Math.min(1, dotRadius + 1.0 - centerDotDist));
    finalR = finalR * (1 - dotBlend) + 236 * dotBlend; // Pink/Magenta #ec4899
    finalG = finalG * (1 - dotBlend) + 72 * dotBlend;
    finalB = finalB * (1 - dotBlend) + 153 * dotBlend;
  }

  // Subtle CSS angle brackets "< / >" or crosshair markings
  if (size >= 48) {
    // Crosshair ticks
    const tickLen = size * 0.06;
    const tickThick = 1.0;
    const onHorizTick = Math.abs(y - lensCy) < tickThick && Math.abs(x - lensCx) > dotRadius * 1.5 && Math.abs(x - lensCx) < dotRadius * 1.5 + tickLen;
    const onVertTick = Math.abs(x - lensCx) < tickThick && Math.abs(y - lensCy) > dotRadius * 1.5 && Math.abs(y - lensCy) < dotRadius * 1.5 + tickLen;
    if (onHorizTick || onVertTick) {
      finalR = 255;
      finalG = 255;
      finalB = 255;
    }
  }

  return [finalR, finalG, finalB, finalA];
}

function main() {
  const iconsDir = path.join(__dirname, 'icons');
  if (!fs.existsSync(iconsDir)) {
    fs.mkdirSync(iconsDir, { recursive: true });
  }

  const sizes = [16, 48, 128];
  for (const size of sizes) {
    const pngBuffer = generatePngBuffer(size, size, (x, y, w, h) => drawIconPixel(x, y, size));
    const filePath = path.join(iconsDir, `icon${size}.png`);
    fs.writeFileSync(filePath, pngBuffer);
    console.log(`Generated: ${filePath} (${pngBuffer.length} bytes)`);
  }
  console.log('✓ All CSS Lens icons successfully generated!');
}

main();
