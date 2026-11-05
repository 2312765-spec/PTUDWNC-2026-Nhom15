// Phần dùng chung của smoke / load / stress (NFR-PERF-001..003).
// Mô phỏng một lượt đọc của khách: danh mục → danh sách → chi tiết → tìm kiếm.
// Cả 4 endpoint đều là GET có cache Redis (D8) nên gắn tag type:cached.
import http from 'k6/http';
import { check } from 'k6';

export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const API = `${BASE_URL}/api/v1`;
const SEARCH_TERMS = ['pho', 'bun', 'ga', 'banh', 'canh', 'mi'];

// NFR-PERF-001: p50 ≤ 150ms (GET có cache), p95 ≤ 500ms, p99 ≤ 1000ms. Lỗi < 1%.
// Ngưỡng theo từng endpoint để summary tách được endpoint nào chậm.
export const CACHED_GET_THRESHOLDS = {
  'http_req_duration{type:cached}': ['p(50)<=150', 'p(95)<=500', 'p(99)<=1000'],
  'http_req_duration{endpoint:categories}': ['p(95)<=500'],
  'http_req_duration{endpoint:recipes}': ['p(95)<=500'],
  'http_req_duration{endpoint:detail}': ['p(95)<=500'],
  'http_req_duration{endpoint:search}': ['p(95)<=500'],
  'http_req_failed{type:cached}': ['rate<0.01'],
};

function pick(items) {
  return items[Math.floor(Math.random() * items.length)];
}

function get(endpoint, url) {
  const res = http.get(url, { tags: { type: 'cached', endpoint } });
  check(res, { [`${endpoint} status 200`]: (r) => r.status === 200 });
  return res;
}

// Lấy slug thật cho trang chi tiết — chạy 1 lần trước khi các VU bắt đầu.
export function loadSlugs() {
  const res = http.get(`${API}/recipes?page=1&pageSize=50`, { tags: { type: 'setup' } });
  const slugs = res.status === 200 ? res.json('items').map((i) => i.slug) : [];
  if (slugs.length === 0) {
    throw new Error(`Không lấy được recipe Published (HTTP ${res.status}). Chạy seed-perf-data.sql trước.`);
  }
  return { slugs };
}

export function browse(data) {
  get('categories', `${API}/categories`);
  get('recipes', `${API}/recipes?page=${1 + Math.floor(Math.random() * 5)}&pageSize=12`);
  get('detail', `${API}/recipes/${pick(data.slugs)}`);
  get('search', `${API}/recipes/search?q=${pick(SEARCH_TERMS)}`);
}
