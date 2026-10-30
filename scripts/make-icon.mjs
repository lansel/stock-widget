// 一次性开发工具：重新生成 src\app.ico（node scripts/make-icon.mjs）
// 产物已提交仓库，日常构建（build.ps1）不需要 Node。
// 图案：深色圆角底板（挂件底色）+ 红绿蜡烛线（红涨绿跌，上升趋势）
import { deflateSync } from "node:zlib";
import { writeFileSync } from "node:fs";

const S = 256;
const px = new Uint8Array(S * S * 4);

const BG = [32, 32, 36];
const RED = [230, 70, 70];
const GREEN = [50, 190, 120];

function put(x, y, c, a) {
  const i = (y * S + x) * 4;
  const sa = a / 255;
  px[i] = Math.round(px[i] * (1 - sa) + c[0] * sa);
  px[i + 1] = Math.round(px[i + 1] * (1 - sa) + c[1] * sa);
  px[i + 2] = Math.round(px[i + 2] * (1 - sa) + c[2] * sa);
  px[i + 3] = Math.min(255, Math.round(px[i + 3] + a * (1 - px[i + 3] / 255)));
}

function insideRR(x, y, x0, y0, x1, y1, r) {
  if (x < x0 || x > x1 || y < y0 || y > y1) return false;
  const rx = x < x0 + r ? x0 + r : x > x1 - r ? x1 - r : x;
  const ry = y < y0 + r ? y0 + r : y > y1 - r ? y1 - r : y;
  const dx = x - rx, dy = y - ry;
  return dx * dx + dy * dy <= r * r;
}

// 半开区间矩形 [x0,x1)×[y0,y1)，2×2 超采样抗锯齿；rr 非空时按圆角矩形裁剪
function fill(x0, y0, x1, y1, c, rr) {
  for (let y = Math.max(0, Math.floor(y0)); y < Math.min(S, Math.ceil(y1)); y++) {
    for (let x = Math.max(0, Math.floor(x0)); x < Math.min(S, Math.ceil(x1)); x++) {
      let cov = 0;
      for (const [ox, oy] of [[0.25, 0.25], [0.75, 0.25], [0.25, 0.75], [0.75, 0.75]]) {
        const sx = x + ox, sy = y + oy;
        if (sx >= x0 && sx < x1 && sy >= y0 && sy < y1)
          if (!rr || insideRR(sx, sy, rr[0], rr[1], rr[2], rr[3], rr[4])) cov += 0.25;
      }
      if (cov > 0) put(x, y, c, Math.round(cov * 255));
    }
  }
}

// 背景：圆角矩形底板
fill(8, 8, 248, 248, BG, [8, 8, 248, 248, 56]);
// 蜡烛线：红涨(升) 绿跌(降) 红(升)，左低右高
fill(67, 122, 73, 200, RED);   // 蜡烛1 影线
fill(58, 142, 82, 182, RED);   // 蜡烛1 实体
fill(125, 82, 131, 164, GREEN); // 蜡烛2 影线
fill(116, 96, 140, 146, GREEN); // 蜡烛2 实体
fill(183, 42, 189, 136, RED);  // 蜡烛3 影线
fill(174, 56, 198, 118, RED);  // 蜡烛3 实体

// 区域平均缩放到 n×n
function downscale(n) {
  const out = Buffer.alloc(n * n * 4);
  const step = S / n;
  for (let ty = 0; ty < n; ty++) {
    for (let tx = 0; tx < n; tx++) {
      let r = 0, g = 0, b = 0, a = 0, wsum = 0;
      const sx0 = tx * step, sx1 = (tx + 1) * step, sy0 = ty * step, sy1 = (ty + 1) * step;
      for (let sy = Math.floor(sy0); sy < Math.ceil(sy1); sy++) {
        for (let sx = Math.floor(sx0); sx < Math.ceil(sx1); sx++) {
          const wx = Math.min(sx1, sx + 1) - Math.max(sx0, sx);
          const wy = Math.min(sy1, sy + 1) - Math.max(sy0, sy);
          if (wx <= 0 || wy <= 0) continue;
          const w = wx * wy, i = (sy * S + sx) * 4;
          r += px[i] * w; g += px[i + 1] * w; b += px[i + 2] * w; a += px[i + 3] * w;
          wsum += w;
        }
      }
      const o = (ty * n + tx) * 4;
      out[o] = Math.round(r / wsum); out[o + 1] = Math.round(g / wsum);
      out[o + 2] = Math.round(b / wsum); out[o + 3] = Math.round(a / wsum);
    }
  }
  return out;
}

// ---- 最小 PNG 编码（RGBA，无滤波）----
const CRC = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();
function crc32(buf) {
  let c = 0xffffffff;
  for (const b of buf) c = CRC[(c ^ b) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}
function chunk(type, data) {
  const out = Buffer.alloc(8 + data.length + 4);
  out.writeUInt32BE(data.length, 0);
  out.write(type, 4, "ascii");
  data.copy(out, 8);
  out.writeUInt32BE(crc32(out.subarray(4, 8 + data.length)), 8 + data.length);
  return out;
}
function encodePNG(size, rgba) {
  const raw = Buffer.alloc((size * 4 + 1) * size);
  for (let y = 0; y < size; y++)
    raw.set(rgba.subarray(y * size * 4, (y + 1) * size * 4), y * (size * 4 + 1) + 1);
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0); ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8; ihdr[9] = 6; // 8bit RGBA
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", ihdr),
    chunk("IDAT", deflateSync(raw, { level: 9 })),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

// ---- ICO 组装（Vista+ PNG 帧）----
const sizes = [16, 24, 32, 48, 64, 256];
const frames = sizes.map((n) => ({ n, png: encodePNG(n, downscale(n)) }));
const count = frames.length;
const dir = Buffer.alloc(6);
dir.writeUInt16LE(1, 2); dir.writeUInt16LE(count, 4);
const entries = Buffer.alloc(16 * count);
let offset = 6 + 16 * count;
frames.forEach((f, i) => {
  const e = entries.subarray(i * 16, i * 16 + 16);
  e[0] = f.n === 256 ? 0 : f.n; e[1] = f.n === 256 ? 0 : f.n;
  e.writeUInt16LE(1, 4); e.writeUInt16LE(32, 6);
  e.writeUInt32LE(f.png.length, 8); e.writeUInt32LE(offset, 12);
  offset += f.png.length;
});
const ico = Buffer.concat([dir, entries, ...frames.map((f) => f.png)]);
writeFileSync("src/app.ico", ico);
writeFileSync(process.env.TEMP + "\\icon_preview.png", encodePNG(S, px));
console.log("src/app.ico written:", ico.length, "bytes,", count, "frames");
console.log("preview:", process.env.TEMP + "\\icon_preview.png");
