// 模拟 B 端：连接 A 端 TCP 45556，收帧校验（PNG 无损签名 / 尺寸 / 帧率）
const net = require('net');
const PORT = Number(process.argv[2] || 45556);

const c = net.connect(PORT, '127.0.0.1', () => console.log('connected'));
let buf = Buffer.alloc(0);
let frames = 0, first = null, bytes = 0, errs = 0;
const started = Date.now();

c.on('data', (d) => {
  buf = Buffer.concat([buf, d]);
  while (buf.length >= 13) {
    const fmt = buf[0];
    const w = buf.readUInt32BE(1), h = buf.readUInt32BE(5), len = buf.readUInt32BE(9);
    if (len <= 0 || len > 64 * 1024 * 1024) { errs++; buf = buf.slice(1); continue; }
    if (buf.length < 13 + len) break;
    const img = buf.slice(13, 13 + len);
    buf = buf.slice(13 + len);
    frames++; bytes += img.length;
    if (!first) first = { fmt: fmt === 0 ? 'PNG' : 'JPEG', w, h, magic: img.slice(0, 4).toString('hex') };
  }
});

setTimeout(() => {
  const secs = (Date.now() - started) / 1000;
  const ok = first && first.magic === '89504e47';
  console.log(JSON.stringify({
    ok, frames, fps: (frames / secs).toFixed(1),
    first, avgBytes: Math.round(bytes / Math.max(1, frames)), errs
  }));
  c.destroy();
  process.exit(ok ? 0 : 1);
}, 6000);
