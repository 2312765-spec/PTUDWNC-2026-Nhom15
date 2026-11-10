import Image from 'next/image';
import Link from 'next/link';
import { Badge, Card, CardBody } from '@/components/ui';
import { formatMinutes } from '@/lib/recipe-format';
import type { RecipeSummaryDto } from '@/lib/types';

const DIFFICULTY_LABEL: Record<string, string> = {
  Easy: 'Dễ',
  Medium: 'Trung bình',
  Hard: 'Khó',
  Expert: 'Rất khó',
};

/** `sizes` theo lưới 1/2/3 cột tại 768/1200px (D84). Lưới tối đa 2 cột thì truyền `GRID_2_COLS_SIZES`. */
export const GRID_3_COLS_SIZES = '(min-width: 1200px) 33vw, (min-width: 768px) 50vw, 100vw';
export const GRID_2_COLS_SIZES = '(min-width: 768px) 50vw, 100vw';

/**
 * NFR-PERF-005 — ảnh card chỉ cao 160px nên chất lượng 50 vẫn đủ nét, nhẹ hơn mặc định 75. Phải nằm trong
 * `images.qualities` của next.config.ts (Next 16 ép giá trị ngoài danh sách về mức gần nhất).
 */
export const CARD_IMAGE_QUALITY = 50;

interface RecipeCardProps {
  recipe: RecipeSummaryDto;
  /**
   * NFR-PERF-005/D86 — card đầu tiên của lưới (ảnh của nó là LCP trên mobile): preload trong <head>. Các card khác
   * lazy để không tranh băng thông với ảnh LCP.
   */
  isLcp?: boolean;
  sizes?: string;
}

/** Card công thức dùng chung cho `/`, `/recipes` (FR-RCP-001) và `/search` (FR-SRCH-001). */
export default function RecipeCard({ recipe, isLcp = false, sizes = GRID_3_COLS_SIZES }: RecipeCardProps) {
  // FR-JOB-002/D88 — ảnh cắt đúng khung card (nhẹ hơn ~38%, nhìn y hệt); job chưa tạo thì dùng ảnh gốc.
  const imageUrl = recipe.featuredImageCardUrl ?? recipe.featuredImageUrl;

  return (
    <Link
      href={`/recipes/${recipe.slug}`}
      className="block h-full rounded-lg focus-visible:outline-2 focus-visible:outline-brand-600"
    >
      <Card className="h-full overflow-hidden transition hover:shadow-md">
        <div className="relative h-40 w-full bg-surface-subtle">
          {imageUrl ? (
            <Image
              src={imageUrl}
              alt={recipe.title}
              fill
              sizes={sizes}
              quality={CARD_IMAGE_QUALITY}
              // D86: card LCP dùng preload (<link rel="preload"> trong <head>, như ảnh đầu trang chi tiết) — trình
              // duyệt tải ảnh ngay khi đọc head thay vì đợi tới thẻ <img> sau CSS. Next cấm preload đi cùng loading.
              preload={isLcp}
              loading={isLcp ? undefined : 'lazy'}
              className="object-cover"
            />
          ) : (
            <div className="flex h-full items-center justify-center text-xs text-ink-muted">
              Chưa có hình ảnh
            </div>
          )}
        </div>
        <CardBody className="flex flex-col gap-2">
          <div className="flex items-center justify-between gap-2 text-xs">
            <Badge variant="warning">{DIFFICULTY_LABEL[recipe.difficulty] ?? recipe.difficulty}</Badge>
            <span className="text-ink-muted">
              {formatMinutes(recipe.prepTimeMinutes + recipe.cookTimeMinutes)}
            </span>
          </div>
          <h3 className="font-semibold">{recipe.title}</h3>
          {recipe.description && (
            <p className="line-clamp-2 text-sm text-ink-muted">{recipe.description}</p>
          )}
        </CardBody>
      </Card>
    </Link>
  );
}
