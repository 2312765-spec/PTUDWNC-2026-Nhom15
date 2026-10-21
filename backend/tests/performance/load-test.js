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
    http_req_duration: ['p(50)<=150', 'p(95)<=500', 'p(99)<=1000'],
    http_req_failed: ['rate<0.01'], // Lỗi < 1%
  },
};

export default function () {
  group('Guest Navigation Flow', function () {
    // 1. Xem danh mục (Cache Redis hỗ trợ tải cao)
    const resCats = http.get(`${BASE_URL}/api/v1/categories`);
    check(resCats, {
      'categories status 200': (r) => r.status === 200,
    });

    // 2. Lướt danh sách công thức trang 1, trang 2
    const resPage1 = http.get(`${BASE_URL}/api/v1/recipes?page=1&pageSize=12`);
    check(resPage1, {
      'page 1 status 200': (r) => r.status === 200,
    });

    const resPage2 = http.get(`${BASE_URL}/api/v1/recipes?page=2&pageSize=12`);
    check(resPage2, {
      'page 2 status 200': (r) => r.status === 200,
    });

    // 3. Tìm kiếm toàn văn
    const searchTerms = ['pho', 'bo', 'ga', 'salad', 'banh'];
    const term = searchTerms[Math.floor(Math.random() * searchTerms.length)];
    const resSearch = http.get(`${BASE_URL}/api/v1/recipes/search?q=${term}&page=1&pageSize=10`);
    check(resSearch, {
      'search status 200': (r) => r.status === 200,
    });
  });

  sleep(Math.random() * 2 + 1); // Nghỉ ngẫu nhiên 1 - 3 giây mô phỏng người dùng thật
}