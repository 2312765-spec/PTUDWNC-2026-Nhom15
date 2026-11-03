/**
 * @jest-environment node
 */
import { loadOgBackground, OG_IMAGE_SIZE, recipeOgImagePath } from '@/lib/seo/ogImage';

const JPEG = Uint8Array.from([0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10]);
const PNG = Uint8Array.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
const WEBP = Uint8Array.from([0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50]);

function respond(body: Uint8Array, init: ResponseInit = {}) {
  return jest.spyOn(global, 'fetch').mockResolvedValue(new Response(body, { status: 200, ...init }));
}

afterEach(() => {
  jest.restoreAllMocks();
});

describe('lib/seo/ogImage — hằng số og:image', () => {
  it('NFR-SEO-002: kích thước 1200×630', () => {
    expect(OG_IMAGE_SIZE).toEqual({ width: 1200, height: 630 });
  });

  it('D61.1: đường dẫn cố định /recipes/{slug}/og-image (không hậu tố hash theo build)', () => {
    expect(recipeOgImagePath('pho-bo')).toBe('/recipes/pho-bo/og-image');
  });
});

/**
 * D61.1 — ảnh nền của og:image. Satori (`next/og`) chỉ đọc chắc chắn JPEG/PNG; mọi trường hợp khác
 * trả `null` để opengraph-image.tsx vẽ thẻ chữ, không làm hỏng ảnh OG.
 */
describe('lib/seo/ogImage — loadOgBackground', () => {
  it('D61.1: ảnh JPEG → data URL image/jpeg (nhận diện bằng magic bytes)', async () => {
    respond(JPEG, { headers: { 'Content-Type': 'application/octet-stream' } });

    const result = await loadOgBackground('https://culinary.example/a.jpg');

    expect(result).toBe(`data:image/jpeg;base64,${Buffer.from(JPEG).toString('base64')}`);
  });

  it('D61.1: ảnh PNG → data URL image/png', async () => {
    respond(PNG);

    await expect(loadOgBackground('https://culinary.example/a.png')).resolves.toMatch(/^data:image\/png;base64,/);
  });

  it('D61.1: WebP (Satori không hỗ trợ chắc chắn) → null, dù header nói image/jpeg', async () => {
    respond(WEBP, { headers: { 'Content-Type': 'image/jpeg' } });

    await expect(loadOgBackground('https://culinary.example/a.webp')).resolves.toBeNull();
  });

  it('D61.1: HTTP lỗi (404) → null', async () => {
    respond(JPEG, { status: 404 });

    await expect(loadOgBackground('https://culinary.example/a.jpg')).resolves.toBeNull();
  });

  it('D61.1: lỗi mạng → null, không ném lỗi', async () => {
    jest.spyOn(global, 'fetch').mockRejectedValue(new TypeError('fetch failed'));

    await expect(loadOgBackground('https://culinary.example/a.jpg')).resolves.toBeNull();
  });

  it('D61.1: không có URL → null, không gọi mạng', async () => {
    const fetchSpy = jest.spyOn(global, 'fetch');

    await expect(loadOgBackground(undefined)).resolves.toBeNull();
    expect(fetchSpy).not.toHaveBeenCalled();
  });
});
