import { ImageResponse } from 'next/og';
import { getRecipeBySlug } from '@/lib/recipes';
import { loadOgBackground, OG_IMAGE_SIZE } from '@/lib/seo/ogImage';
import { SITE_NAME } from '@/lib/seo/site';
import type { RecipeDetailDto } from '@/lib/types';

/**
 * NFR-SEO-002 / D61.1 — og:image 1200×630 cho `/recipes/{slug}`: ảnh chính crop cover + tên món.
 * Không có ảnh hoặc ảnh không đọc được → thẻ chữ. Không bao giờ ném lỗi: crawler nhận ảnh hợp lệ.
 * Font mặc định của `next/og` (Geist) có đủ glyph tiếng Việt.
 */
export const revalidate = 300; // cùng chu kỳ ISR với trang chi tiết (SRS 5.1)

const BRAND = '#ea580c'; // orange-600 — màu chủ đạo của site
/** Satori không hỗ trợ `inset` — phủ kín khung bằng top/left + kích thước tường minh. */
const FILL = { position: 'absolute', top: 0, left: 0, width: '100%', height: '100%' } as const;

async function findRecipe(slug: string): Promise<RecipeDetailDto | null> {
  try {
    return await getRecipeBySlug(slug);
  } catch {
    return null;
  }
}

function primaryImageUrl(recipe: RecipeDetailDto): string | undefined {
  return (recipe.images.find((image) => image.isPrimary) ?? recipe.images[0])?.originalUrl;
}

export async function GET(_request: Request, { params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const recipe = await findRecipe(slug);
  const background = recipe ? await loadOgBackground(primaryImageUrl(recipe)) : null;

  return new ImageResponse(
    (
      <div
        style={{
          width: '100%',
          height: '100%',
          display: 'flex',
          position: 'relative',
          background: background ? '#000' : `linear-gradient(135deg, ${BRAND}, #9a3412)`,
          color: '#fff',
        }}
      >
        {background && (
          // eslint-disable-next-line @next/next/no-img-element, jsx-a11y/alt-text -- Satori chỉ hiểu <img>
          <img
            src={background}
            width={OG_IMAGE_SIZE.width}
            height={OG_IMAGE_SIZE.height}
            style={{ ...FILL, objectFit: 'cover' }}
          />
        )}
        <div
          style={{
            ...FILL,
            display: 'flex',
            flexDirection: 'column',
            justifyContent: 'flex-end',
            padding: '56px 64px',
            background: background ? 'linear-gradient(to top, rgba(0,0,0,0.85), rgba(0,0,0,0) 65%)' : 'transparent',
          }}
        >
          {recipe ? (
            <div style={{ display: 'flex', fontSize: recipe.title.length > 40 ? 56 : 72, lineHeight: 1.15 }}>
              {recipe.title}
            </div>
          ) : (
            <div style={{ display: 'flex', fontSize: 40, opacity: 0.9 }}>Công thức nấu ăn Việt</div>
          )}
          <div style={{ display: 'flex', marginTop: 24, fontSize: 32, color: background ? '#fdba74' : '#fff' }}>
            {SITE_NAME}
          </div>
        </div>
      </div>
    ),
    { ...OG_IMAGE_SIZE },
  );
}
