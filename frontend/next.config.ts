import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  reactStrictMode: true,

  // NFR-PERF-005 — ảnh từ MinIO phải khai báo remotePatterns mới dùng được next/image
  images: {
    remotePatterns: [
      { protocol: 'http', hostname: 'localhost', port: '9000', pathname: '/culinary-blog/**' },
      // TODO(S8 — D): thêm domain MinIO production
    ],
  },

  // NFR-SEC-005
  poweredByHeader: false,
};

export default nextConfig;
