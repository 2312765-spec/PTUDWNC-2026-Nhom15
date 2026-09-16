/**
 * Trang chủ — chủ sở hữu: B. FR-RCP-001.
 * SRS mục 5.1: ISR revalidate = 3600.
 */
export const revalidate = 3600;

const modules = [
  { owner: 'A', name: 'Xác thực & Tài khoản', routes: '/auth/login, /auth/register, /profile' },
  { owner: 'B', name: 'Khám phá & Tìm kiếm', routes: '/, /recipes, /categories, /search' },
  { owner: 'C', name: 'Sáng tạo Công thức', routes: '/dashboard/recipes/*' },
  { owner: 'D', name: 'Media & Vận hành', routes: 'upload, gallery, SEO, sitemap' },
];

export default function HomePage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-16">
      <h1 className="text-3xl font-bold text-brand-700">Culinary Blog</h1>
      <p className="mt-2 text-ink-muted">
        Khung dự án đã chạy. Mỗi người mở thư mục của mình và bắt đầu từ Sprint 0.
      </p>

      <ul className="mt-8 space-y-3">
        {modules.map((m) => (
          <li key={m.owner} className="rounded-lg border border-border bg-surface p-4">
            <span className="inline-flex size-7 items-center justify-center rounded-full bg-brand-100 text-sm font-semibold text-brand-700">
              {m.owner}
            </span>
            <span className="ml-3 font-medium">{m.name}</span>
            <p className="mt-1 text-sm text-ink-muted">{m.routes}</p>
          </li>
        ))}
      </ul>

      <p className="mt-8 text-sm text-ink-muted">
        Tài liệu: <code>docs/team-assignment.md</code> · <code>docs/roadmap.md</code> ·{' '}
        <code>docs/decisions.md</code>
      </p>
    </div>
  );
}
