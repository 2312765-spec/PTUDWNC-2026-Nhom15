import type { Metadata } from 'next';
import { Baloo_2 } from 'next/font/google';
import './globals.css';
import { SiteSidebar } from '@/components/layout/SiteSidebar';
import { getSiteUrl, SITE_NAME } from '@/lib/seo/site';
import { Providers } from './providers';

/**
 * D97.2 — chỉ tiêu đề dùng web font: Baloo 2 (variable font, một file cho mọi độ đậm), tự host lúc build (next/font).
 * Chữ nội dung dùng font hệ thống — không tải gì.
 *
 * NFR-PERF-005: LCP của mọi trang là CHỮ; với `display: 'swap'` Lighthouse tính LCP ở lần vẽ lại bằng web font
 * (đo 2026-11-10: render delay 3.3–3.9s, 9–12 file font / trang, LCP trung vị 3.7–4.2s). `display: 'optional'`: font
 * tới kịp trong ~100ms thì dùng, không thì giữ font dự phòng cho lần tải đó (không đổi giữa chừng → không đẩy LCP,
 * không CLS); lần xem sau font đã nằm trong cache.
 */
const headingFont = Baloo_2({
  subsets: ['latin', 'vietnamese'],
  display: 'optional',
  variable: '--font-heading',
});

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
    <html lang="vi" className={headingFont.variable}>
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
