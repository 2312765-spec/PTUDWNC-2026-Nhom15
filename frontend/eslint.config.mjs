import next from 'eslint-config-next';

/**
 * NFR-MAINT-001 — ESLint + Prettier, không được có lỗi/cảnh báo khi merge.
 * eslint-config-next v16 export sẵn flat config, không cần FlatCompat.
 */
const config = [
  { ignores: ['.next/**', 'node_modules/**', 'next-env.d.ts'] },
  ...next,
];

export default config;
