import type { NextConfig } from 'next';

const isDev = process.env.NODE_ENV !== 'production';

// Khai báo mảng remotePatterns chuẩn kiểu Next.js
const remotePatterns: NonNullable<NextConfig['images']>['remotePatterns'] = [
  { protocol: 'http', hostname: 'localhost', port: '9000', pathname: '/culinary-blog/**' },
];

// Chỉ thêm placehold.co khi ở môi trường dev/test
if (isDev) {
  remotePatterns.push({
    protocol: 'https',
    hostname: 'placehold.co',
  });
}

const nextConfig: NextConfig = {
  reactStrictMode: true,
  images: {
    remotePatterns,
  },
  poweredByHeader: false,
  logging: {
    serverFunctions: false,
  },
};

export default nextConfig;