import type { Metadata } from 'next';
import './globals.css';
import { Providers } from './providers';

export const metadata: Metadata = {
  title: {
    default: 'Culinary Blog',
    // NFR-SEO-002: title <= 60 ký tự
    template: '%s | Culinary Blog',
  },
  description: 'Chia sẻ, khám phá và lưu trữ công thức nấu ăn từ nhiều nền ẩm thực.',
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
          <main id="main">{children}</main>
        </Providers>
      </body>
    </html>
  );
}
