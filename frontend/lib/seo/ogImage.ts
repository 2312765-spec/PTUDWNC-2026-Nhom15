import { SITE_NAME } from './site';

/** NFR-SEO-002 — kích thước og:image theo SRS §4.7. */
export const OG_IMAGE_SIZE = { width: 1200, height: 630 };
export const OG_IMAGE_ALT = `Ảnh xem trước công thức trên ${SITE_NAME}`;

/**
 * D61.1 — URL cố định của og:image (Route Handler `app/(public)/recipes/[slug]/og-image/route.tsx`).
 * Không dùng file `opengraph-image.tsx`: route đó có hậu tố hash đổi theo mỗi bản build
 * (`/opengraph-image-b3yc0w`) nên JSON-LD không trỏ tới được.
 */
export function recipeOgImagePath(slug: string): string {
  return `/recipes/${slug}/og-image`;
}

/** Ảnh nền quá lâu thì bỏ — og:image phải trả về kịp cho crawler, thẻ chữ vẫn hợp lệ. */
const FETCH_TIMEOUT_MS = 5000;

/** Magic bytes của hai định dạng Satori (`next/og`) đọc chắc chắn. WebP/AVIF → thẻ chữ (D61.1). */
function sniffMime(bytes: Uint8Array): string | null {
  if (bytes[0] === 0xff && bytes[1] === 0xd8 && bytes[2] === 0xff) return 'image/jpeg';
  if (bytes[0] === 0x89 && bytes[1] === 0x50 && bytes[2] === 0x4e && bytes[3] === 0x47) return 'image/png';
  return null;
}

/**
 * D61.1 — tải ảnh chính làm nền og:image, trả data URL. Không tin Content-Type header (cùng nguyên tắc
 * CONS-007). Mọi lỗi (không có URL, HTTP lỗi, mạng, định dạng không hỗ trợ) → `null`, không ném.
 */
export async function loadOgBackground(url: string | undefined): Promise<string | null> {
  if (!url) return null;
  try {
    const res = await fetch(url, { signal: AbortSignal.timeout(FETCH_TIMEOUT_MS) });
    if (!res.ok) return null;
    const bytes = new Uint8Array(await res.arrayBuffer());
    const mime = sniffMime(bytes);
    return mime ? `data:${mime};base64,${Buffer.from(bytes).toString('base64')}` : null;
  } catch {
    return null;
  }
}
