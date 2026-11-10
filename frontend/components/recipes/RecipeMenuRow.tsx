import Image from 'next/image';
import Link from 'next/link';
import { CARD_IMAGE_QUALITY } from '@/components/recipes/RecipeCard';
import { difficultyLabel, formatMinutes } from '@/lib/recipe-format';
import type { RecipeSummaryDto } from '@/lib/types';

/**
 * D97 — một dòng của "Thực đơn mới" (trang chủ): số thứ tự, tên món, đường chấm dẫn tới tổng thời gian, dòng phụ
 * độ khó · tác giả, ảnh nhỏ cuối dòng. Ảnh lazy — không bao giờ là LCP (LCP là ảnh Món đáng thử).
 */
export function RecipeMenuRow({ recipe, index }: { recipe: RecipeSummaryDto; index: number }) {
  const imageUrl = recipe.featuredImageCardUrl ?? recipe.featuredImageUrl;
  const href = `/recipes/${recipe.slug}`;

  return (
    <li className="flex items-center gap-4 border-b border-border py-4 md:gap-5">
      <span aria-hidden="true" className="w-7 flex-none font-display text-lg font-bold text-ink-muted">
        {String(index + 1).padStart(2, '0')}
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <div className="flex items-baseline gap-2.5">
          <h3 className="min-w-0 font-display text-xl font-extrabold leading-tight md:text-2xl">
            <Link href={href} className="hover:text-accent">
              {recipe.title}
            </Link>
          </h3>
          <span aria-hidden="true" className="hidden min-w-6 flex-1 -translate-y-1.5 border-b-2 border-dotted border-border-strong sm:block" />
          <span className="ml-auto flex-none whitespace-nowrap font-bold text-accent sm:ml-0">
            {formatMinutes(recipe.prepTimeMinutes + recipe.cookTimeMinutes)}
          </span>
        </div>
        <p className="text-sm text-ink-muted">
          {difficultyLabel(recipe.difficulty)}
          {recipe.authorName && ` · ${recipe.authorName}`}
        </p>
      </div>
      <div className="relative size-16 flex-none overflow-hidden rounded-xl bg-surface md:size-[88px]">
        {imageUrl && (
          <Image src={imageUrl} alt="" fill sizes="88px" quality={CARD_IMAGE_QUALITY} loading="lazy" className="object-cover" />
        )}
      </div>
    </li>
  );
}
