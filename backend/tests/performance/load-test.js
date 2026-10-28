/*Tăng dần tải lên ≥ 100 Virtual Users (VUs) đồng thời 
trên cấu hình 2 vCPU / 4GB, duy trì tải ổn định 
mà p95 vẫn ≤ 500ms (đáp ứng NFR-PERF-002 và NFR-PERF-001) */

import http from 'k6/http';
import { check, group, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  stages: [
    { duration: '30s', target: 20 },  // Ramp-up nhẹ lên 20 VUs
    { duration: '1m', target: 100 },   // NFR-PERF-002: Đạt 100 người dùng đồng thời
    { duration: '3m', target: 100 },   // Duy trì tải 100 VUs liên tục trong 3 phút
    { duration: '30s', target: 0 },    // Ramp-down về 0
  ],
  thresholds: {
    // Đảm bảo dưới tải 100 người dùng, hệ thống vẫn giữ chuẩn NFR-PERF-001
    'http_req_duration{type:cached}': ['p(50)<=150', 'p(95)<=500', 'p(99)<=1000'],
    http_req_failed: ['rate<0.01'], // Lỗi < 1%
  },
};

export default function () {
  const catRes = http.get(`${BASE_URL}/api/v1/categories`, { tags: { type: 'cached' } });
  check(catRes, { 'status is 200': (r) => r.status === 200 });

  const recRes = http.get(`${BASE_URL}/api/v1/recipes?page=1&pageSize=10`, { tags: { type: 'cached' } });
  check(recRes, { 'status is 200': (r) => r.status === 200 });

  sleep(1);
}