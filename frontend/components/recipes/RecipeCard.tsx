import Image from 'next/image';
import Link from 'next/link';
import { Badge, Card, CardBody } from '@/components/ui';
import type { RecipeSummaryDto } from '@/lib/types';

const DIFFICULTY_LABEL: Record<string, string> = {
  Easy: 'Dễ',
  Medium: 'Trung bình',
  Hard: 'Khó',
  Expert: 'Rất khó',
};

/** Card công thức dùng chung cho `/recipes` (FR-RCP-001) và `/search` (FR-SRCH-001). */
export default function RecipeCard({ recipe }: { recipe: RecipeSummaryDto }) {
  return (
    <Link
      href={`/recipes/${recipe.slug}`}
      className="block h-full rounded-lg focus-visible:outline-2 focus-visible:outline-brand-600"
    >
      <Card className="h-full overflow-hidden transition hover:shadow-md">
        <div className="relative h-40 w-full bg-surface-subtle">
          {recipe.featuredImageUrl ? (
            <Image
              src={recipe.featuredImageUrl}
              alt={recipe.title}
              fill
              sizes="(min-width: 640px) 50vw, 100vw"
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
              {recipe.prepTimeMinutes + recipe.cookTimeMinutes} phút
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
