import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ImageDeleteDialog } from '@/components/upload/ImageDeleteDialog';

describe('ImageDeleteDialog', () => {
  // components/ui/Dialog.tsx (dùng <dialog> gốc) luôn render title/description/children
  // vào DOM — chỉ showModal()/close() bật/tắt hiển thị qua thuộc tính `open` của <dialog>,
  // nên test trạng thái đóng/mở phải kiểm `open`, không kiểm text có/không trong DOM.
  it('open=false → thẻ <dialog> chưa có thuộc tính open', () => {
    render(<ImageDeleteDialog open={false} onCancel={jest.fn()} onConfirm={jest.fn()} />);
    expect(document.querySelector('dialog')).not.toHaveAttribute('open');
  });

  it('open=true → thẻ <dialog> có thuộc tính open, hiện tiêu đề và 2 nút Huỷ/Xoá ảnh', () => {
    render(<ImageDeleteDialog open onCancel={jest.fn()} onConfirm={jest.fn()} />);
    expect(document.querySelector('dialog')).toHaveAttribute('open');
    expect(screen.getByText('Xoá ảnh này?')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Huỷ' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /xoá ảnh/i })).toBeInTheDocument();
  });

  it('bấm "Huỷ" → gọi onCancel, không gọi onConfirm', async () => {
    const onCancel = jest.fn();
    const onConfirm = jest.fn();
    render(<ImageDeleteDialog open onCancel={onCancel} onConfirm={onConfirm} />);

    await userEvent.click(screen.getByRole('button', { name: 'Huỷ' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('bấm "Xoá ảnh" → gọi onConfirm', async () => {
    const onConfirm = jest.fn();
    render(<ImageDeleteDialog open onCancel={jest.fn()} onConfirm={onConfirm} />);

    await userEvent.click(screen.getByRole('button', { name: /xoá ảnh/i }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('isDeleting=true → nút "Xoá ảnh" ở trạng thái loading, bị disable', () => {
    render(<ImageDeleteDialog open isDeleting onCancel={jest.fn()} onConfirm={jest.fn()} />);
    expect(screen.getByRole('button', { name: /xoá ảnh/i })).toBeDisabled();
  });
});
