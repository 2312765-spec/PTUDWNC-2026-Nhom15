import RecipeCard from '@/components/recipes/RecipeCard';
import { Card, CardBody } from '@/components/ui';
import { ApiError } from '@/lib/categories';
import { SEARCH_MIN_LENGTH, searchRecipes } from '@/lib/recipes';
import SearchPagination from './SearchPagination';

const PAGE_SIZE = 12;

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
            <RecipeCard recipe={recipe} />
          </li>
        ))}
      </ul>

      <SearchPagination q={q} page={data.page} totalPages={data.totalPages} />
    </section>
  );
}
