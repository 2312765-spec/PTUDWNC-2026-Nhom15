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

/**
 * jsdom (qua jest-environment-jsdom) chưa hiện thực <dialog> — showModal()/close() không
 * tồn tại, gọi thẳng sẽ throw. components/ui/Dialog.tsx (mọi confirm dialog trong app) dùng
 * thẻ <dialog> gốc nên cần polyfill tối thiểu này để test được, không riêng feature nào.
 */
if (typeof HTMLDialogElement !== 'undefined') {
  if (!HTMLDialogElement.prototype.showModal) {
    HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
  }
  if (!HTMLDialogElement.prototype.close) {
    HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
      this.removeAttribute('open');
      this.dispatchEvent(new Event('close'));
    };
  }
}
