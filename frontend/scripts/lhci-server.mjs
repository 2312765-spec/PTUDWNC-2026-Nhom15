// NFR-PERF-005/D86 — server cho Lighthouse CI (`startServerCommand` trong lighthouserc.json).
// Bật `next start`, làm nóng cache rồi mới in READY_LINE cho lhci bắt đầu đo:
//  - gọi từng URL sẽ đo (dựng trang SSR + cache Redis của API),
//  - gọi mọi ảnh `/_next/image` có trên trang (bộ tối ưu ảnh lưu kết quả vào .next/cache/images).
// Production luôn có cache nóng; không làm nóng thì lần đo đầu tính cả thời gian chuyển đổi ảnh lần đầu.
import { spawn } from 'node:child_process';
import { readFileSync } from 'node:fs';

const READY_LINE = 'LHCI server ready';
const BASE = 'http://localhost:3000';
const ACCEPT_IMAGE = 'image/avif,image/webp,image/apng,image/*,*/*;q=0.8'; // như Chrome — cache theo định dạng

const config = JSON.parse(readFileSync(new URL('../lighthouserc.json', import.meta.url), 'utf8'));
// Đo thử URL khác ở máy local: LHCI_WARM_URLS="http://localhost:3000/a,http://localhost:3000/b".
const urls = process.env.LHCI_WARM_URLS?.split(',') ?? config.ci.collect.url;

const server = spawn('npx', ['next', 'start', '-p', '3000'], { stdio: 'inherit', shell: true });
const stop = () => server.kill();
process.on('SIGTERM', stop);
process.on('SIGINT', stop);
server.on('exit', (code) => process.exit(code ?? 0));

async function waitForServer() {
  for (let i = 0; i < 120; i++) {
    try {
      if ((await fetch(BASE)).ok) return;
    } catch {
      // chưa nghe cổng
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error('next start không lên sau 60 giây');
}

/** Lấy mọi URL `/_next/image` trong src và srcset của HTML. */
function imageUrls(html) {
  const found = new Set();
  for (const match of html.matchAll(/(?:src|srcSet|srcset)="([^"]+)"/g)) {
    for (const candidate of match[1].split(',')) {
      const url = candidate.trim().split(/\s+/)[0].replaceAll('&amp;', '&');
      if (url.startsWith('/_next/image')) found.add(url);
    }
  }
  return [...found];
}

async function warm() {
  for (const url of urls) {
    const html = await (await fetch(url)).text();
    const images = imageUrls(html);
    await Promise.all(images.map((src) => fetch(BASE + src, { headers: { Accept: ACCEPT_IMAGE } })));
    console.log(`[lhci-server] làm nóng ${url.replace(BASE, '')}: ${images.length} ảnh`);
  }
}

try {
  await waitForServer();
  await warm();
} catch (error) {
  console.error(`[lhci-server] ${error}`);
  stop();
  process.exit(1);
}
console.log(READY_LINE);
