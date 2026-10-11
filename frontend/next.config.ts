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

  // FR-AUTH-001/002 — dev log mặc định in tham số server action, tức in cả mật khẩu
  // của loginAction/registerAction ra terminal. Tắt đi.
  logging: {
    serverFunctions: false,
  },
};

export default nextConfig;
