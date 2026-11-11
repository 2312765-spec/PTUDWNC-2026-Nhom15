import type { Metadata } from 'next';
import './globals.css';
import { SiteSidebar } from '@/components/layout/SiteSidebar';
import { getSiteUrl, SITE_NAME } from '@/lib/seo/site';
import { Providers } from './providers';

/**
 * D97.2 — KHÔNG dùng web font (NFR-PERF-005). Đo Lighthouse 2026-11-10 trên cùng máy: mọi web font tải ở lần xem đầu
 * (Baloo 2 + Be Vietnam Pro 107–150 KB; chỉ Baloo 2, có/không preload 43–70 KB) làm LCP chậm hơn main ~300–600ms
 * (LCP mọi trang là chữ). Tiêu đề dùng font hệ thống cỡ lớn, đậm — nét riêng nằm ở màu, bố cục, icon.
 */

export const metadata: Metadata = {
  // NFR-SEO-002: canonical / og:url tương đối của các trang con thành tuyệt đối theo gốc này
  metadataBase: new URL(getSiteUrl()),
  title: {
    default: SITE_NAME,
    // NFR-SEO-002: title <= 60 ký tự
    template: `%s | ${SITE_NAME}`,
  },
  description: 'Chia sẻ, khám phá và lưu trữ công thức nấu ăn từ nhiều nền ẩm thực.',
  openGraph: { type: 'website', siteName: SITE_NAME, locale: 'vi_VN' },
  twitter: { card: 'summary_large_image' },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="vi">
      <body className="min-h-screen antialiased">
        {/* NFR-USE-002: skip link cho screen reader và điều hướng bàn phím */}
        <a
          href="#main"
          className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded focus:bg-surface focus:px-4 focus:py-2 focus:shadow"
        >
          Bỏ qua, tới nội dung chính
        </a>
        <Providers>
          {/* D97: thanh bên trái (≥ lg) / dải trên cùng (màn nhỏ) + nội dung. */}
          <div className="lg:flex">
            <SiteSidebar />
            <main id="main" className="min-w-0 flex-1">
              {children}
            </main>
          </div>
        </Providers>
      </body>
    </html>
  );
}
