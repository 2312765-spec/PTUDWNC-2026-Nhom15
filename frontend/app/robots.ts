import type { MetadataRoute } from 'next';
import { getSiteUrl } from '@/lib/seo/site';

/**
 * NFR-SEO-003 — robots.txt: cho phép mọi crawler, khai báo sitemap.
 * D51: đây là cách duy nhất báo Google về sitemap (endpoint ping đã ngừng).
 * D52: sitemap.xml do Hangfire job (FR-JOB-003) sinh lên MinIO, Nginx phục vụ tại /sitemap.xml.
 */
export default function robots(): MetadataRoute.Robots {
  return {
    rules: [{ userAgent: '*', allow: '/', disallow: ['/dashboard/'] }],
    sitemap: `${getSiteUrl()}/sitemap.xml`,
  };
}
