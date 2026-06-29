/** Parse an #rrggbb color into an rgba() string at the given alpha. Falls back to a soft
 *  amber tint for anything unparseable. Used for highlight tints and list color chips. */
export function hexToRgba(hex: string, alpha: number): string {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return `rgba(255,243,163,${alpha})`;
  const n = parseInt(m[1], 16);
  return `rgba(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255},${alpha})`;
}
