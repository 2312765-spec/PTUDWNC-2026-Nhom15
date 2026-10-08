import Link from 'next/link';
import RecipeCard from '@/components/recipes/RecipeCard';
import { Card, CardBody } from '@/components/ui';
import { ApiError } from '@/lib/categories';
import { getRecipes, type RecipeListFilters } from '@/lib/recipes';
import RecipesPagination from './RecipesPagination';

const PAGE_SIZE = 12;

/** FR-RCP-001 — Server Component async: gọi API rồi render danh sách (hoặc lỗi theo error code). */
export default async function RecipeResults({ filters }: { filters: RecipeListFilters }) {
  let data;
  try {
    data = await getRecipes(filters, PAGE_SIZE);
  } catch (err) {
    // NFR-USE-003: xử lý theo Application Error Code; D4: tham số sai → 400 VALIDATION_ERROR.
    const message =
      err instanceof ApiError && err.code === 'VALIDATION_ERROR'
        ? 'Bộ lọc không hợp lệ. Vui lòng kiểm tra lại các giá trị đã nhập.'
        : 'Không tải được danh sách công thức. Vui lòng thử lại sau.';
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
        <CardBody role="status" className="flex flex-col items-center gap-3 py-10 text-center text-ink-muted">
          <p>Không có công thức nào khớp với bộ lọc.</p>
          <Link href="/recipes" className="text-sm font-medium text-brand-700 hover:underline">
            Xem tất cả công thức
          </Link>
        </CardBody>
      </Card>
    );
  }

  return (
    <section aria-label="Danh sách công thức" className="space-y-4">
      <p role="status" className="text-sm text-ink-muted">
        Có <strong className="text-ink">{data.totalCount}</strong> công thức
      </p>

      <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {data.items.map((recipe) => (
          <li key={recipe.id}>
            <RecipeCard recipe={recipe} />
          </li>
        ))}
      </ul>

      <RecipesPagination filters={filters} page={data.page} totalPages={data.totalPages} />
    </section>
  );
}
