import {
  IMAGE_MIME_ERROR_MESSAGE,
  IMAGE_SIZE_ERROR_MESSAGE,
  MAX_IMAGE_SIZE_BYTES,
  validateImageFile,
} from '@/lib/validation/imageFile';

function makeFile(sizeBytes: number, type: string, name = 'test.jpg'): File {
  return new File([new Uint8Array(sizeBytes)], name, { type });
}

describe('validateImageFile — CONS-007 (pre-check client, không thay được server D28)', () => {
  it('file hợp lệ (jpeg, dưới 5MB) → ok', () => {
    const file = makeFile(1024, 'image/jpeg');
    expect(validateImageFile(file)).toEqual({ ok: true });
  });

  it.each(['image/jpeg', 'image/png', 'image/webp', 'image/avif'])('chấp nhận MIME %s', (type) => {
    expect(validateImageFile(makeFile(1024, type))).toEqual({ ok: true });
  });

  it('đúng 5MB (biên) → vẫn ok', () => {
    const file = makeFile(MAX_IMAGE_SIZE_BYTES, 'image/png');
    expect(validateImageFile(file)).toEqual({ ok: true });
  });

  it('vượt quá 5MB dù chỉ 1 byte → FILE_SIZE_EXCEEDED message', () => {
    const file = makeFile(MAX_IMAGE_SIZE_BYTES + 1, 'image/png');
    expect(validateImageFile(file)).toEqual({ ok: false, message: IMAGE_SIZE_ERROR_MESSAGE });
  });

  it('MIME không nằm trong 4 định dạng cho phép → FILE_MIME_INVALID message', () => {
    const file = makeFile(1024, 'application/x-msdownload', 'napkin.exe');
    expect(validateImageFile(file)).toEqual({ ok: false, message: IMAGE_MIME_ERROR_MESSAGE });
  });

  it('size vượt VÀ mime sai cùng lúc → ưu tiên báo lỗi size trước', () => {
    const file = makeFile(MAX_IMAGE_SIZE_BYTES + 1, 'text/plain');
    expect(validateImageFile(file)).toEqual({ ok: false, message: IMAGE_SIZE_ERROR_MESSAGE });
  });
});
