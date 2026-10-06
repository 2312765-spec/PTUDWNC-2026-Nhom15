import Image from 'next/image';
import Link from 'next/link';
import { Badge, Card, CardBody } from '@/components/ui';
import { ApiError } from '@/lib/categories';
import { SEARCH_MIN_LENGTH, searchRecipes } from '@/lib/recipes';
import SearchPagination from './SearchPagination';

const PAGE_SIZE = 12;

const DIFFICULTY_LABEL: Record<string, string> = {
  Easy: 'Dễ',
  Medium: 'Trung bình',
  Hard: 'Khó',
  Expert: 'Rất khó',
};

/** FR-SRCH-001 — Server Component async: gọi API rồi render kết quả (hoặc lỗi theo error code). */
export default async function SearchResults({ q, page }: { q: string; page: number }) {
  let data;
  try {
    data = await searchRecipes(q, page, PAGE_SIZE);
  } catch (err) {
    // NFR-USE-003: xử lý theo Application Error Code, không theo message của server.
    const message =
      err instanceof ApiError && err.code === 'VALIDATION_ERROR'
        ? `Từ khóa tìm kiếm phải có ít nhất ${SEARCH_MIN_LENGTH} ký tự.`
        : 'Không tìm kiếm được lúc này. Vui lòng thử lại sau.';
    return (
      <Card>
        <CardBody role="alert" className="py-8 text-center text-ink-muted">
          {message}
        </CardBody>
      </Card>
    );
  }

  if (data.items.length === 0) {
    return (
      <Card>
        <CardBody role="status" className="py-10 text-center text-ink-muted">
          Không tìm thấy công thức nào cho &quot;{q}&quot;. Thử từ khóa ngắn hơn hoặc gõ không dấu.
        </CardBody>
      </Card>
    );
  }

  return (
    <section aria-label="Kết quả tìm kiếm" className="space-y-4">
      <p role="status" className="text-sm text-ink-muted">
        Tìm thấy <strong className="text-ink">{data.totalCount}</strong> kết quả cho &quot;{q}&quot;
      </p>

      <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {data.items.map((recipe) => (
          <li key={recipe.id}>
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
                    <Badge variant="warning">
                      {DIFFICULTY_LABEL[recipe.difficulty] ?? recipe.difficulty}
                    </Badge>
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
          </li>
        ))}
      </ul>

      <SearchPagination q={q} page={data.page} totalPages={data.totalPages} />
    </section>
  );
}
