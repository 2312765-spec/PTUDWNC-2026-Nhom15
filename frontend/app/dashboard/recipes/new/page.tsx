'use client';

import { RecipeWizard } from '@/components/recipes/editor/RecipeWizard';

/** FR-RCP-003 — tạo công thức mới bằng wizard nhiều bước. SRS 5.1: /dashboard/* render CSR. */
export default function NewRecipePage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <header className="border-b border-border pb-6">
        <h1 className="text-2xl font-bold text-brand-700">Tạo công thức mới</h1>
        <p className="mt-1 text-sm text-ink-muted">
          Điền thông tin theo từng bước. Công thức được lưu nháp — bạn xuất bản sau khi hoàn thiện.
        </p>
      </header>
      <div className="mt-6">
        <RecipeWizard />
      </div>
    </div>
  );
}
