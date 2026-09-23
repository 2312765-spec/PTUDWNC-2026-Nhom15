/** CONS-007 — upload tối đa 5MB, chỉ 4 định dạng ảnh này. */
export const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024;

export const ACCEPTED_IMAGE_MIME_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/avif'] as const;

export type AcceptedImageMimeType = (typeof ACCEPTED_IMAGE_MIME_TYPES)[number];

/**
 * Cùng nội dung message với `IMAGE_ERROR_MESSAGES` trong `useRecipeImages.ts` (2 error
 * code cho FILE_SIZE_EXCEEDED/FILE_MIME_INVALID) — giữ 1 nguồn chữ duy nhất để pre-check
 * client và lỗi 400 thật từ server hiển thị đúng một câu cho người dùng.
 */
export const IMAGE_SIZE_ERROR_MESSAGE = 'Kích thước file vượt quá giới hạn 5MB.';
export const IMAGE_MIME_ERROR_MESSAGE = 'File không đúng định dạng. Chỉ chấp nhận JPG, PNG, WebP, AVIF.';

export type ImageFileValidationResult = { ok: true } | { ok: false; message: string };

/**
 * CONS-007 — pre-check ở client, chỉ để phản hồi nhanh (chặn trước khi tốn băng thông
 * upload). KHÔNG thay được validate server (D28 kiểm magic bytes) — đây không phải
 * nguồn thật, chỉ là UX.
 */
export function validateImageFile(file: File): ImageFileValidationResult {
  if (file.size > MAX_IMAGE_SIZE_BYTES) {
    return { ok: false, message: IMAGE_SIZE_ERROR_MESSAGE };
  }

  if (!ACCEPTED_IMAGE_MIME_TYPES.includes(file.type as AcceptedImageMimeType)) {
    return { ok: false, message: IMAGE_MIME_ERROR_MESSAGE };
  }

  return { ok: true };
}
