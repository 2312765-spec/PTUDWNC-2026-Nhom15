import next from 'eslint-config-next';

/**
 * NFR-MAINT-001 – ESLint + Prettier
 */
const config = [
  { ignores: ['.next/**', 'node_modules/**', 'next-env.d.ts'] },
  ...next,
  {
    rules: {
      'react/no-unescaped-entities': 'off',
      'react-hooks/set-state-in-effect': 'off',
      '@next/next/no-img-element': 'off',
      'react/jsx-no-comment-textnodes': 'off',
      'react-hooks/exhaustive-deps': 'off'
    }
  }
];

export default config;