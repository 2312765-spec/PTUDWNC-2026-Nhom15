// Stress (NFR-PERF-002): vượt mức 100 VU lên 180 VU để tìm giới hạn chịu tải.
// Ngưỡng nới hơn load test — mục tiêu là quan sát điểm bắt đầu suy giảm, không phải đạt SLA.
import { sleep } from 'k6';
import { browse, loadSlugs } from './common.js';

export const options = {
  stages: [
    { duration: '1m', target: 50 },
    { duration: '2m', target: 100 },
    { duration: '2m', target: 150 },
    { duration: '2m', target: 180 },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    'http_req_duration{type:cached}': ['p(99)<=2000'],
    'http_req_failed{type:cached}': ['rate<0.05'],
  },
};

export function setup() {
  return loadSlugs();
}

export default function (data) {
  browse(data);
  sleep(0.5);
}
