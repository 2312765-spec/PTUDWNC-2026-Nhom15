import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ImageUploadDropzone } from '@/components/upload/ImageUploadDropzone';

function makeFile(sizeBytes: number, type: string, name = 'pho.jpg'): File {
  return new File([new Uint8Array(sizeBytes)], name, { type });
}

describe('ImageUploadDropzone', () => {
  it('chọn file hợp lệ qua input → gọi onFilesSelected, không hiện lỗi', async () => {
    const onFilesSelected = jest.fn();
    render(<ImageUploadDropzone onFilesSelected={onFilesSelected} />);
    const file = makeFile(1024, 'image/jpeg');

    const input = screen.getByLabelText(/chọn ảnh/i, { selector: 'input' });
    await userEvent.upload(input, file);

    expect(onFilesSelected).toHaveBeenCalledWith([file]);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('bấm nút "Chọn ảnh từ máy" mở được input file ẩn', async () => {
    render(<ImageUploadDropzone onFilesSelected={jest.fn()} />);
    const input = screen.getByLabelText(/chọn ảnh/i, { selector: 'input' }) as HTMLInputElement;
    const clickSpy = jest.spyOn(input, 'click');

    await userEvent.click(screen.getByRole('button', { name: /chọn ảnh từ máy/i }));

    expect(clickSpy).toHaveBeenCalled();
  });

  it('CONS-007: file > 5MB → KHÔNG gọi onFilesSelected, hiện đúng thông báo', async () => {
    const onFilesSelected = jest.fn();
    render(<ImageUploadDropzone onFilesSelected={onFilesSelected} />);
    const file = makeFile(6 * 1024 * 1024, 'image/jpeg', 'big.jpg');

    const input = screen.getByLabelText(/chọn ảnh/i, { selector: 'input' });
    await userEvent.upload(input, file);

    expect(onFilesSelected).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toHaveTextContent('Kích thước file vượt quá giới hạn 5MB.');
  });

  it('D28: MIME không hỗ trợ → KHÔNG gọi onFilesSelected, hiện đúng thông báo', async () => {
    // applyAccept: false — accept="image/*" trên <input> chỉ là gợi ý cho hộp thoại OS,
    // không phải rào chắn thật (có thể chọn "All Files"); validateImageFile mới là chốt chặn.
    const user = userEvent.setup({ applyAccept: false });
    const onFilesSelected = jest.fn();
    render(<ImageUploadDropzone onFilesSelected={onFilesSelected} />);
    const file = makeFile(1024, 'application/x-msdownload', 'napkin.exe');

    const input = screen.getByLabelText(/chọn ảnh/i, { selector: 'input' });
    await user.upload(input, file);

    expect(onFilesSelected).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toHaveTextContent(
      'File không đúng định dạng. Chỉ chấp nhận JPG, PNG, WebP, AVIF.',
    );
  });

  it('kéo-thả file hợp lệ vào dropzone → gọi onFilesSelected', () => {
    const onFilesSelected = jest.fn();
    render(<ImageUploadDropzone onFilesSelected={onFilesSelected} />);
    const file = makeFile(1024, 'image/png', 'goi-cuon.png');

    const dropzone = screen.getByTestId('image-dropzone');
    const dataTransfer = { files: [file] };
    // dispatchEvent trực tiếp vì jsdom không mô phỏng DataTransfer đầy đủ cho drop event.
    dropzone.dispatchEvent(
      Object.assign(new Event('drop', { bubbles: true, cancelable: true }), { dataTransfer }),
    );

    expect(onFilesSelected).toHaveBeenCalledWith([file]);
  });

  it('disabled=true → input và nút bị vô hiệu hoá', () => {
    render(<ImageUploadDropzone onFilesSelected={jest.fn()} disabled />);
    expect(screen.getByRole('button', { name: /chọn ảnh từ máy/i })).toBeDisabled();
    expect(screen.getByLabelText(/chọn ảnh/i, { selector: 'input' })).toBeDisabled();
  });
});
