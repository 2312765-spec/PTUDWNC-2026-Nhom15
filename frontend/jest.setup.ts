import '@testing-library/jest-dom';

/** Router của App Router không tồn tại ngoài Next runtime — mỗi test đọc lại qua mockPush. */
export const mockPush = jest.fn();

jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush, replace: jest.fn(), back: jest.fn(), prefetch: jest.fn() }),
  usePathname: () => '/',
  useSearchParams: () => new URLSearchParams(),
  redirect: jest.fn(),
}));

beforeEach(() => {
  mockPush.mockReset();
});
