import type { NextConfig } from 'next';

type RemotePattern = NonNullable<NonNullable<NextConfig['images']>['remotePatterns']>[number];

/**
 * CONS-009/D55 — prod: ảnh MinIO đi qua Nginx cùng domain với site (`{site}/culinary-blog/...`).
 * Đọc lúc `next build` (Dockerfile nhận NEXT_PUBLIC_SITE_URL qua build arg).
 */
function siteImagePattern(): RemotePattern[] {
  const siteUrl = process.env.NEXT_PUBLIC_SITE_URL;
  if (!siteUrl) return [];

  const url = new URL(siteUrl);
  return [
    {
      protocol: url.protocol === 'https:' ? 'https' : 'http',
      hostname: url.hostname,
      ...(url.port ? { port: url.port } : {}),
      pathname: '/culinary-blog/**',
    },
  ];
}

const nextConfig: NextConfig = {
  reactStrictMode: true,

  // CONS-009 — frontend/Dockerfile chép .next/standalone sang image runtime
  output: 'standalone',

  // NFR-PERF-005 — ảnh từ MinIO phải khai báo remotePatterns mới dùng được next/image
  images: {
    remotePatterns: [
      { protocol: 'http', hostname: 'localhost', port: '9000', pathname: '/culinary-blog/**' },
      ...siteImagePattern(),
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
