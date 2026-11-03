// Smoke (NFR-PERF-001): tải nhẹ 5 VU trong 30 giây — kiểm tra script chạy đúng và các ngưỡng
// p50/p95/p99 trước khi chạy load test. Cũng dùng để làm nóng cache trước khi đo NFR-PERF-003.
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, CACHED_GET_THRESHOLDS, browse, loadSlugs } from './common.js';

export const options = {
  vus: 5,
  duration: '30s',
  thresholds: CACHED_GET_THRESHOLDS,
};

export function setup() {
  // /health/ready = PostgreSQL + Redis. /health gồm cả MinIO nên trả 503 khi MinIO tắt.
  const health = http.get(`${BASE_URL}/health/ready`, { tags: { type: 'health' } });
  check(health, { 'health/ready status 200': (r) => r.status === 200 });
  return loadSlugs();
}

export default function (data) {
  browse(data);
  sleep(1);
}
