import type { Config } from 'jest';
import nextJest from 'next/jest.js';

/** NFR-MAINT-002 — Jest + Testing Library cho frontend. */
const createJestConfig = nextJest({ dir: './' });

const config: Config = {
  testEnvironment: 'jsdom',
  setupFilesAfterEnv: ['<rootDir>/jest.setup.ts'],
  moduleNameMapper: { '^@/(.*)$': '<rootDir>/$1' },
  testMatch: ['<rootDir>/__tests__/**/*.test.{ts,tsx}'],
  collectCoverageFrom: ['lib/**/*.{ts,tsx}', 'app/**/*.{ts,tsx}', 'components/**/*.{ts,tsx}'],
};

/** Auth.js v5 và các dependency của nó chỉ phát hành ESM — phải cho SWC biên dịch. */
const esmPackages = ['next-auth', '@auth', 'jose', 'oauth4webapi', 'preact', 'preact-render-to-string', '@panva'];

export default async function jestConfig(): Promise<Config> {
  const resolved = await createJestConfig(config)();
  return {
    ...resolved,
    transformIgnorePatterns: [
      `/node_modules/(?!(${esmPackages.join('|')})/)`,
      '^.+\\.module\\.(css|sass|scss)$',
    ],
  };
}
