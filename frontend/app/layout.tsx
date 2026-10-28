import type { Metadata } from 'next';
import './globals.css';
import { SiteHeader } from '@/components/layout/SiteHeader';
import { getSiteUrl, SITE_NAME } from '@/lib/seo/site';
import { Providers } from './providers';

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
          className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded focus:bg-white focus:px-4 focus:py-2 focus:shadow"
        >
          Bỏ qua, tới nội dung chính
        </a>
        <Providers>
          <SiteHeader />
          <main id="main">{children}</main>
        </Providers>
      </body>
    </html>
  );
}
