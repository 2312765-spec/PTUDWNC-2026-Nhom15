//Kiểm tra giới hạn chịu đựng vượt mức
//(lên 150–200 VUs) để quan sát cơ chế xử lý khi quá tải.

import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  stages: [
    { duration: '1m', target: 50 },
    { duration: '2m', target: 100 },
    { duration: '2m', target: 150 },
    { duration: '2m', target: 180 }, // Vượt tải để xác định giới hạn chịu đựng
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    'http_req_duration': ['p(99)<=2000'],
    'http_req_failed': ['rate<0.05'], // Cho phép tỷ lệ lỗi < 5% ở đỉnh tải cực hạn
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/api/v1/recipes?page=1&pageSize=12`);
  check(res, {
    'status is 200': (r) => r.status === 200,
  });

  sleep(0.5);
}