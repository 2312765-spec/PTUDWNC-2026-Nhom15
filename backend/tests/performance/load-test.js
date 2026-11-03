// Load (NFR-PERF-001 + NFR-PERF-002): tăng lên 100 VU đồng thời, giữ 3 phút.
// Đạt khi các ngưỡng NFR-PERF-001 vẫn giữ được dưới tải 100 người dùng.
import { sleep } from 'k6';
import { CACHED_GET_THRESHOLDS, browse, loadSlugs } from './common.js';

export const options = {
  stages: [
    { duration: '30s', target: 20 },  // khởi động
    { duration: '1m', target: 100 },  // lên 100 VU (NFR-PERF-002)
    { duration: '3m', target: 100 },  // giữ 100 VU
    { duration: '30s', target: 0 },   // hạ tải
  ],
  thresholds: CACHED_GET_THRESHOLDS,
};

export function setup() {
  return loadSlugs();
}

export default function (data) {
  browse(data);
  sleep(1);
}
