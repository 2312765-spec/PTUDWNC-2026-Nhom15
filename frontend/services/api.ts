/**
 * API Service Client for Culinary Blog (.NET 10 / Express RESTful API)
 * Kết nối giao diện .tsx với hệ thống backend API (/api/v1/*)
 * Tuân thủ đặc tả SRS v1.0.0, chuẩn RFC 7807 Problem Details và IMemoryCache
 */

import { Recipe, Category, Role, RecipeFilterParams, RecipeStatus } from '../types';

export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

export interface ApiRequestLog {
  id: string;
  method: string;
  endpoint: string;
  status: number;
  durationMs: number;
  timestamp: string;
  cacheHeader?: string;
  requestBody?: any;
  responseBody?: any;
}

// Global API Activity listeners for UI Monitoring
type LogListener = (log: ApiRequestLog) => void;
const logListeners = new Set<LogListener>();

export function subscribeApiLogs(listener: LogListener) {
  logListeners.add(listener);
  return () => {
    logListeners.delete(listener);
  };
}

export const apiLogs: ApiRequestLog[] = [];

function notifyLog(log: ApiRequestLog) {
  apiLogs.unshift(log);
  if (apiLogs.length > 50) apiLogs.pop();
  logListeners.forEach((fn) => {
    try {
      fn(log);
    } catch {
      // Ignore listener error
    }
  });
}

// Helper to construct headers with User authentication & context
function getHeaders(role?: Role, userId?: string): HeadersInit {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  };

  if (role) {
    headers['x-user-role'] = role;
    if (role === 'Admin') {
      headers['Authorization'] = 'Bearer mock-dotnet10-admin-jwt';
    } else if (role === 'Author') {
      headers['Authorization'] = 'Bearer mock-dotnet10-author-jwt';
    }
  }

  if (userId) {
    headers['x-user-id'] = userId;
  }

  return headers;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CategoryDetailResponse {
  category: Category;
  recipes: PagedResult<Recipe>;
}

export interface CacheEntryInfo {
  key: string;
  ttlRemainingSeconds: number;
  expiresAt: string;
}

export const api = {
  /**
   * FR-CAT-001: Lấy danh sách tất cả danh mục
   * Có áp dụng IMemoryCache TTL 60 phút trên backend
   */
  async getCategories(forceRefresh = false): Promise<{
    data: Category[];
    cacheStatus: 'HIT' | 'MISS' | 'NONE';
    ttlRemaining?: string;
  }> {
    const startTime = performance.now();
    if (forceRefresh) {
      await api.invalidateCache('categories:all');
    }

    const res = await fetch('/api/v1/categories', {
      headers: getHeaders(),
    });

    const durationMs = Math.round(performance.now() - startTime);
    const cacheStatus = (res.headers.get('X-Cache') as 'HIT' | 'MISS') || 'NONE';
    const ttlRemaining = res.headers.get('X-Cache-TTL-Remaining') || undefined;

    const data = await res.json();

    notifyLog({
      id: Math.random().toString(36).substring(2, 9),
      method: 'GET',
      endpoint: '/api/v1/categories',
      status: res.status,
      durationMs,
      timestamp: new Date().toLocaleTimeString(),
      cacheHeader: cacheStatus,
      responseBody: Array.isArray(data) ? `Array(${data.length} danh mục)` : data,
    });

    if (!res.ok) {
      throw data as ProblemDetails;
    }

    return {
      data,
      cacheStatus,
      ttlRemaining,
    };
  },

  /**
   * FR-CAT-002: Lấy chi tiết danh mục theo Slug + Phân trang và Phân quyền
   */
  async getCategoryBySlug(
    slug: string,
    page = 1,
    pageSize = 12,
    role?: Role,
    userId?: string
  ): Promise<CategoryDetailResponse> {
    const startTime = performance.now();
    const endpoint = `/api/v1/categories/${slug}?page=${page}&pageSize=${pageSize}`;

    const res = await fetch(endpoint, {
      headers: getHeaders(role, userId),
    });

    const durationMs = Math.round(performance.now() - startTime);
    const data = await res.json();

    notifyLog({
      id: Math.random().toString(36).substring(2, 9),
      method: 'GET',
      endpoint,
      status: res.status,
      durationMs,
      timestamp: new Date().toLocaleTimeString(),
      responseBody: res.ok ? `Category: ${data.category?.name}` : data,
    });

    if (!res.ok) {
      throw data as ProblemDetails;
    }

    return data;
  },

  /**
   * FR-CAT-003: Tạo danh mục mới [Admin Only]
   * Tự động sinh slug, kiểm tra trùng lặp và xóa cache
   */
  async createCategory(
    payload: { name: string; description?: string; imageUrl?: string },
    role: Role = 'Admin',
    userId?: string
  ): Promise<{ category: Category; location: string }> {
    const startTime = performance.now();
    const endpoint = '/api/v1/categories';

    const res = await fetch(endpoint, {
      method: 'POST',
      headers: getHeaders(role, userId),
      body: JSON.stringify(payload),
    });

    const durationMs = Math.round(performance.now() - startTime);
    const location = res.headers.get('Location') || `/api/v1/categories`;
    const data = await res.json();

    notifyLog({
      id: Math.random().toString(36).substring(2, 9),
      method: 'POST',
      endpoint,
      status: res.status,
      durationMs,
      timestamp: new Date().toLocaleTimeString(),
      requestBody: payload,
      responseBody: data,
    });

    if (!res.ok) {
      throw data as ProblemDetails;
    }

    return { category: data, location };
  },

  /**
   * Lấy danh sách công thức với bộ lọc đa tiêu chí
   */
  async getRecipes(
    params: RecipeFilterParams = {},
    role?: Role,
    userId?: string
  ): Promise<PagedResult<Recipe>> {
    const startTime = performance.now();
    const query = new URLSearchParams();

    if (params.searchTerm) query.append('searchTerm', params.searchTerm);
    if (params.categoryId && params.categoryId !== 'all') query.append('categoryId', params.categoryId);
    if (params.difficulty && params.difficulty !== 'All') query.append('difficulty', params.difficulty);
    if (params.maxCookTime) query.append('maxCookTime', params.maxCookTime.toString());
    if (params.sort) query.append('sort', params.sort);
    if (params.page) query.append('page', params.page.toString());
    if (params.pageSize) query.append('pageSize', params.pageSize.toString());

    const endpoint = `/api/v1/recipes${query.toString() ? `?${query.toString()}` : ''}`;
    const res = await fetch(endpoint, {
      headers: getHeaders(role, userId),
    });

    const durationMs = Math.round(performance.now() - startTime);
    const data = await res.json();

    notifyLog({
      id: Math.random().toString(36).substring(2, 9),
      method: 'GET',
      endpoint,
      status: res.status,
      durationMs,
      timestamp: new Date().toLocaleTimeString(),
      responseBody: `Recipes total: ${data.totalCount}`,
    });

    if (!res.ok) {
      throw data as ProblemDetails;
    }

    return data;
  },

  /**
   * Lấy chi tiết công thức theo Slug hoặc Id
   */
  async getRecipeBySlugOrId(
    slugOrId: string,
    role?: Role,
    userId?: string
  ): Promise<Recipe> {
    const startTime = performance.now();
    const endpoint = `/api/v1/recipes/${slugOrId}`;

    const res = await fetch(endpoint, {
      headers: getHeaders(role, userId),
    });

    const durationMs = Math.round(performance.now() - startTime);
    const data = await res.json();

    notifyLog({
      id: Math.random().toString(36).substring(2, 9),
      method: 'GET',
      endpoint,
      status: res.status,
      durationMs,
      timestamp: new Date().toLocaleTimeString(),
      responseBody: res.ok ? `Recipe: ${data.title}` : data,
    });

    if (!res.ok) {
      throw data as ProblemDetails;
    }

    return data;
  },

  /**
   * Tạo công thức mới
   */
  async createRecipe(
    payload: Partial<Recipe>,
    role: Role,
    userId: string
  ): Promise<Recipe> {
    const startTime = performance.now();
    const endpoint = '/api/v1/recipes';

    const res = await fetch(endpoint, {
      method: 'POST',
      headers: getHeaders(role, userId),
      body: JSON.stringify(payload),
    });

    const durationMs = Math.round(performance.now() - startTime);
    const data = await res.json();

    notifyLog({
      id: Math.random().toString(36).substring(2, 9),
      method: 'POST',
      endpoint,
      status: res.status,
      durationMs,
      timestamp: new Date().toLocaleTimeString(),
      requestBody: { title: payload.title, categoryId: payload.categoryId },
      responseBody: data,
    });

    if (!res.ok) {
      throw data as ProblemDetails;
    }

    return data;
  },

  /**
   * Cập nhật công thức
   */
  async updateRecipe(
    id: string,
    payload: Partial<Recipe>,
    role: Role,
    userId: string
  ): Promise<Recipe> {
    const startTime = performance.now();
    const endpoint = `/api/v1/recipes/${id}`;

    const res = await fetch(endpoint, {
      method: 'PUT',
      headers: getHeaders(role, userId),
      body: JSON.stringify(payload),
    });

    const durationMs = Math.round(performance.now() - startTime);
    const data = await res.json();

    notifyLog({
      id: Math.random().toString(36).substring(2, 9),
      method: 'PUT',
      endpoint,
      status: res.status,
      durationMs,
      timestamp: new Date().toLocaleTimeString(),
      responseBody: data,
    });

    if (!res.ok) {
      throw data as ProblemDetails;
    }

    return data;
  },

  /**
   * Xóa công thức
   */
  async deleteRecipe(id: string, role: Role, userId: string): Promise<{ success: boolean }> {
    const startTime = performance.now();
    const endpoint = `/api/v1/recipes/${id}`;

    const res = await fetch(endpoint, {
      method: 'DELETE',
      headers: getHeaders(role, userId),
    });

    const durationMs = Math.round(performance.now() - startTime);
    const data = await res.json();

    notifyLog({
      id: Math.random().toString(36).substring(2, 9),
      method: 'DELETE',
      endpoint,
      status: res.status,
      durationMs,
      timestamp: new Date().toLocaleTimeString(),
      responseBody: data,
    });

    if (!res.ok) {
      throw data as ProblemDetails;
    }

    return data;
  },

  /**
   * Thay đổi trạng thái xuất bản (Published, Draft, Archived)
   */
  async updateRecipeStatus(
    id: string,
    status: RecipeStatus,
    role: Role,
    userId: string
  ): Promise<Recipe> {
    const startTime = performance.now();
    const endpoint = `/api/v1/recipes/${id}/status`;

    const res = await fetch(endpoint, {
      method: 'PATCH',
      headers: getHeaders(role, userId),
      body: JSON.stringify({ status }),
    });

    const durationMs = Math.round(performance.now() - startTime);
    const data = await res.json();

    notifyLog({
      id: Math.random().toString(36).substring(2, 9),
      method: 'PATCH',
      endpoint,
      status: res.status,
      durationMs,
      timestamp: new Date().toLocaleTimeString(),
      requestBody: { status },
      responseBody: data,
    });

    if (!res.ok) {
      throw data as ProblemDetails;
    }

    return data;
  },

  /**
   * Thả tim (Like) công thức
   */
  async toggleLikeRecipe(id: string): Promise<{ likeCount: number }> {
    const startTime = performance.now();
    const endpoint = `/api/v1/recipes/${id}/like`;

    const res = await fetch(endpoint, {
      method: 'POST',
      headers: getHeaders(),
    });

    const durationMs = Math.round(performance.now() - startTime);
    const data = await res.json();

    notifyLog({
      id: Math.random().toString(36).substring(2, 9),
      method: 'POST',
      endpoint,
      status: res.status,
      durationMs,
      timestamp: new Date().toLocaleTimeString(),
      responseBody: data,
    });

    if (!res.ok) {
      throw data as ProblemDetails;
    }

    return data;
  },

  /**
   * Kiểm tra tình trạng sức khỏe hệ thống (Health Check)
   */
  async getHealth(): Promise<any> {
    const res = await fetch('/api/health');
    return await res.json();
  },

  /**
   * Thống kê tổng quan (Dashboard metrics)
   */
  async getStats(): Promise<{
    totalRecipes: number;
    publishedCount: number;
    draftCount: number;
    archivedCount: number;
    totalCategories: number;
    totalViews: number;
    totalLikes: number;
  }> {
    const res = await fetch('/api/v1/stats');
    return await res.json();
  },

  /**
   * Kiểm tra trạng thái IMemoryCache
   */
  async getCacheStatus(): Promise<{ cacheKeys: CacheEntryInfo[] }> {
    const res = await fetch('/api/v1/cache/status');
    return await res.json();
  },

  /**
   * Xóa một key trong IMemoryCache
   */
  async invalidateCache(key = 'categories:all'): Promise<boolean> {
    const res = await fetch('/api/v1/cache/invalidate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ key }),
    });
    const data = await res.json();
    return data.success;
  },
};
