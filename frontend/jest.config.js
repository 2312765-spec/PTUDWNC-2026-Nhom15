const nextJest = require('next/jest');

const createJestConfig = nextJest({ dir: './' });

/** CLAUDE.md muc 2 — Frontend test: Jest + Testing Library, thu muc `__tests__/`. */
const config = {
  testEnvironment: 'jest-environment-jsdom',
  setupFilesAfterEnv: ['<rootDir>/jest.setup.ts'],
  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/$1',
  },
  testPathIgnorePatterns: ['<rootDir>/.next/', '<rootDir>/node_modules/'],
};

module.exports = createJestConfig(config);
