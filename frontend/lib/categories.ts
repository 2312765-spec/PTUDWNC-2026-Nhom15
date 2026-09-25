const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000/api/v1";

export interface CategoryDto {
  id: string;
  name: string;
  slug: string;
  description: string;
  recipeCount: number;
}

export interface RecipeItemDto {
  id: string;
  title: string;
  slug: string;
  summary: string;
  cookingTimeMinutes: number;
  difficulty: "Easy" | "Medium" | "Hard";
  status: "Published" | "Draft";
  authorName: string;
  publishedAt: string;
}

export interface CategoryDetailResponseDto {
  id: string;
  name: string;
  slug: string;
  description: string;
  recipeCount: number;
  recipes: {
    items: RecipeItemDto[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
  };
}

export interface CreateCategoryRequest {
  name: string;
  description?: string;
}

export interface UpdateCategoryRequest {
  name: string;
  description?: string;
}

// FR-CAT-001: Lấy tất cả danh mục (Cache 30 phút ở Backend)
export async function getCategories(): Promise<CategoryDto[]> {
  const res = await fetch(`${API_BASE_URL}/categories`, {
    next: { revalidate: 60 },
  });
  if (!res.ok) throw new Error("Không thể tải danh sách danh mục");
  return res.json();
}

// FR-CAT-002: Lấy chi tiết danh mục theo Slug + Phân trang recipes
export async function getCategoryBySlug(
  slug: string,
  page = 1,
  pageSize = 12
): Promise<CategoryDetailResponseDto> {
  const res = await fetch(
    `${API_BASE_URL}/categories/${slug}?page=${page}&pageSize=${pageSize}`,
    { cache: "no-store" }
  );
  if (!res.ok) throw new Error(`Không tìm thấy danh mục: ${slug}`);
  return res.json();
}

// FR-CAT-003: [Admin] Tạo danh mục mới
export async function createCategory(
  data: CreateCategoryRequest,
  token?: string
): Promise<CategoryDto> {
  const res = await fetch(`${API_BASE_URL}/categories`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(data),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || "Tạo danh mục thất bại");
  }
  return res.json();
}

// FR-CAT-004: [Admin] Cập nhật danh mục (Slug KHÔNG đổi khi đổi Name)
export async function updateCategory(
  id: string,
  data: UpdateCategoryRequest,
  token?: string
): Promise<CategoryDto> {
  const res = await fetch(`${API_BASE_URL}/categories/${id}`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(data),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || "Cập nhật danh mục thất bại");
  }
  return res.json();
}