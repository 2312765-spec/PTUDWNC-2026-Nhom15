/**
 * NFR-SEO-002 / D52 — gốc URL tuyệt đối của site (canonical, og:url, JSON-LD, robots.txt).
 * `NEXT_PUBLIC_SITE_URL` được nhúng lúc `next build` (Dockerfile nhận qua build arg).
 */
export const SITE_NAME = 'Culinary Blog';

export function getSiteUrl(): string {
  return (process.env.NEXT_PUBLIC_SITE_URL || 'http://localhost:3000').replace(/\/+$/, '');
}

/** Đường dẫn tương đối → URL tuyệt đối theo site; URL đã tuyệt đối (ảnh MinIO — D58) giữ nguyên. */
export function absoluteUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) return path;
  return `${getSiteUrl()}/${path.replace(/^\/+/, '')}`;
}
