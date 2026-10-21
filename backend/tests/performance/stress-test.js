//Kiểm tra giới hạn chịu đựng vượt mức
//(lên 150–200 VUs) để quan sát cơ chế xử lý khi quá tải.

import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  stages: [
    { duration: '30s', target: 50 },
    { duration: '1m', target: 120 },
    { duration: '1m', target: 180 },  // Vượt ngưỡng thiết kế 100 VUs
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.05'], // Cho phép tỉ lệ lỗi chấp nhận được dưới stress < 5%
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/api/v1/recipes?page=1&pageSize=10`);
  check(res, {
    'status is 200 or 429': (r) => r.status === 200 || r.status === 429,
  });
  sleep(0.5);
}