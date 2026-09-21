import express, { Request, Response } from 'express';
import path from 'path';
import { createServer as createViteServer } from 'vite';
import { MOCK_CATEGORIES, MOCK_RECIPES, MOCK_USERS, unaccent, generateSlug } from './src/data/mockData';
import { Recipe, Category, ApplicationUser, Role, RecipeStatus } from './src/types';

// =========================================================================
// IMemoryCache with 60-minute sliding expiration (SRS FR-CAT-001)
// =========================================================================
interface CacheEntry<T> {
  value: T;
  expiresAt: number;
  slidingMinutes: number;
  lastAccessed: number;
}

class MemoryCache {
  private cache = new Map<string, CacheEntry<any>>();

  get<T>(key: string): { hit: boolean; data?: T; ttlRemainingSeconds?: number } {
    const entry = this.cache.get(key);
    if (!entry) return { hit: false };
    const now = Date.now();
    if (now > entry.expiresAt) {
      this.cache.delete(key);
      return { hit: false };
    }
    // Sliding expiration: reset TTL on each access
    entry.lastAccessed = now;
    entry.expiresAt = now + entry.slidingMinutes * 60 * 1000;
    return { 
      hit: true, 
      data: entry.value,
      ttlRemainingSeconds: Math.round((entry.expiresAt - now) / 1000)
    };
  }

  set<T>(key: string, value: T, slidingMinutes: number = 60) {
    const now = Date.now();
    this.cache.set(key, {
      value,
      slidingMinutes,
      lastAccessed: now,
      expiresAt: now + slidingMinutes * 60 * 1000,
    });
  }

  remove(key: string): boolean {
    return this.cache.delete(key);
  }

  clear() {
    this.cache.clear();
  }

  getEntries() {
    const now = Date.now();
    const result: { key: string; ttlRemainingSeconds: number; expiresAt: string }[] = [];
    for (const [key, entry] of this.cache.entries()) {
      if (now <= entry.expiresAt) {
        result.push({
          key,
          ttlRemainingSeconds: Math.max(0, Math.round((entry.expiresAt - now) / 1000)),
          expiresAt: new Date(entry.expiresAt).toISOString(),
        });
      }
    }
    return result;
  }
}

const memoryCache = new MemoryCache();

// =========================================================================
// In-Memory Database Seed (Sync with types and mock data)
// =========================================================================
let dbCategories: Category[] = JSON.parse(JSON.stringify(MOCK_CATEGORIES));
let dbRecipes: Recipe[] = JSON.parse(JSON.stringify(MOCK_RECIPES));
let dbUsers: ApplicationUser[] = JSON.parse(JSON.stringify(MOCK_USERS));

// Helper to authenticate user from headers (Authorization Bearer / x-user-role)
function getAuthUser(req: Request): { id: string; role: Role; displayName: string; user?: ApplicationUser } {
  const authHeader = req.headers.authorization;
  const roleHeader = (req.headers['x-user-role'] as string) || '';
  const userIdHeader = (req.headers['x-user-id'] as string) || '';

  if (roleHeader === 'Admin' || (authHeader && authHeader.includes('admin'))) {
    const adminUser = dbUsers.find((u) => u.role === 'Admin') || dbUsers[2];
    return { id: adminUser.id, role: 'Admin', displayName: adminUser.displayName, user: adminUser };
  }
  if (roleHeader === 'Author' || (authHeader && authHeader.includes('author')) || (authHeader && authHeader.startsWith('Bearer '))) {
    const authorUser = dbUsers.find((u) => u.id === userIdHeader) || dbUsers[0];
    return { id: authorUser.id, role: 'Author', displayName: authorUser.displayName, user: authorUser };
  }
  return { id: 'guest', role: 'Guest', displayName: 'Khách vãng lai' };
}

// Generate unique slug for category or recipe
function generateUniqueCategorySlug(name: string): string {
  const baseSlug = generateSlug(name);
  let candidate = baseSlug;
  let suffix = 2;
  while (dbCategories.some((c) => c.slug === candidate)) {
    candidate = `${baseSlug}-${suffix}`;
    suffix++;
  }
  return candidate;
}

function generateUniqueRecipeSlug(title: string): string {
  const baseSlug = generateSlug(title);
  let candidate = baseSlug;
  let suffix = 2;
  while (dbRecipes.some((r) => r.slug === candidate)) {
    candidate = `${baseSlug}-${suffix}`;
    suffix++;
  }
  return candidate;
}

// =========================================================================
// Start Express Server
// =========================================================================
async function startServer() {
  const app = express();
  const PORT = 3000;

  app.use(express.json());

  // -----------------------------------------------------------------------
  // DIAGNOSTICS & SYSTEM HEALTH
  // -----------------------------------------------------------------------
  app.get('/api/health', (req, res) => {
    res.json({
      status: 'Healthy',
      timestamp: new Date().toISOString(),
      version: '1.0.0 (ASP.NET Core .NET 10 Clean Architecture API Bridge)',
      uptimeSeconds: Math.round(process.uptime()),
      database: {
        status: 'Healthy',
        latencyMs: 3,
        totalCategories: dbCategories.length,
        totalRecipes: dbRecipes.length,
      },
      cache: {
        status: 'Healthy',
        provider: 'IMemoryCache (Sliding 60m)',
        activeKeys: memoryCache.getEntries().length,
        entries: memoryCache.getEntries(),
      },
    });
  });

  app.get('/api/v1/stats', (req, res) => {
    const totalRecipes = dbRecipes.length;
    const publishedCount = dbRecipes.filter((r) => r.status === 'Published').length;
    const draftCount = dbRecipes.filter((r) => r.status === 'Draft').length;
    const archivedCount = dbRecipes.filter((r) => r.status === 'Archived').length;
    const totalCategories = dbCategories.length;
    const totalViews = dbRecipes.reduce((sum, r) => sum + (r.viewCount || 0), 0);
    const totalLikes = dbRecipes.reduce((sum, r) => sum + (r.likeCount || 0), 0);

    res.json({
      totalRecipes,
      publishedCount,
      draftCount,
      archivedCount,
      totalCategories,
      totalViews,
      totalLikes,
    });
  });

  // -----------------------------------------------------------------------
  // FR-CAT-001: Xem Danh Sách Tất Cả Danh Mục
  // GET /api/v1/categories
  // Sắp xếp Name tăng dần, kèm số lượng Published recipes. Cache 60m sliding.
  // -----------------------------------------------------------------------
  app.get('/api/v1/categories', (req: Request, res: Response) => {
    const cacheKey = 'categories:all';
    const cached = memoryCache.get<Category[]>(cacheKey);

    if (cached.hit && cached.data) {
      res.setHeader('X-Cache', 'HIT');
      res.setHeader('X-Cache-TTL-Remaining', `${cached.ttlRemainingSeconds}s`);
      return res.status(200).json(cached.data);
    }

    // Cache Miss -> Tính toán và nạp vào cache
    const categoryDtos = dbCategories
      .map((cat) => {
        const publishedCount = dbRecipes.filter(
          (r) => r.categoryId === cat.id && r.status === 'Published'
        ).length;
        return {
          ...cat,
          recipeCount: publishedCount,
        };
      })
      .sort((a, b) => a.name.localeCompare(b.name, 'vi', { sensitivity: 'base' }));

    memoryCache.set(cacheKey, categoryDtos, 60);

    res.setHeader('X-Cache', 'MISS');
    res.setHeader('X-Cache-TTL-Remaining', '3600s');
    return res.status(200).json(categoryDtos);
  });

  // -----------------------------------------------------------------------
  // FR-CAT-002: Xem Chi Tiết Danh Mục & Phân Trang Recipes
  // GET /api/v1/categories/:slug?page=1&pageSize=12
  // Guest: Published, Author: Published + own drafts, Admin: All
  // -----------------------------------------------------------------------
  app.get('/api/v1/categories/:slug', (req: Request, res: Response) => {
    const { slug } = req.params;
    const page = Math.max(1, parseInt(req.query.page as string) || 1);
    const pageSize = Math.max(1, Math.min(50, parseInt(req.query.pageSize as string) || 12));

    const category = dbCategories.find((c) => c.slug === slug);
    if (!category) {
      return res.status(404).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.4',
        title: 'Not Found',
        status: 404,
        detail: `Không tìm thấy danh mục với slug '${slug}'.`,
        instance: `/api/v1/categories/${slug}`,
      });
    }

    const currentUser = getAuthUser(req);

    const filteredRecipes = dbRecipes.filter((r) => {
      if (r.categoryId !== category.id) return false;
      if (currentUser.role === 'Admin') return true;
      if (currentUser.role === 'Author') {
        return r.status === 'Published' || r.authorId === currentUser.id;
      }
      return r.status === 'Published';
    });

    const totalCount = filteredRecipes.length;
    const totalPages = Math.ceil(totalCount / pageSize) || 1;
    const skip = (page - 1) * pageSize;
    const paginatedItems = filteredRecipes.slice(skip, skip + pageSize);

    const publishedCount = dbRecipes.filter(
      (r) => r.categoryId === category.id && r.status === 'Published'
    ).length;

    const categoryDto: Category = {
      ...category,
      recipeCount: publishedCount,
    };

    return res.status(200).json({
      category: categoryDto,
      recipes: {
        items: paginatedItems,
        totalCount,
        page,
        pageSize,
        totalPages,
      },
    });
  });

  // -----------------------------------------------------------------------
  // FR-CAT-003: Tạo Danh Mục Mới [Admin Only]
  // POST /api/v1/categories
  // -----------------------------------------------------------------------
  app.post('/api/v1/categories', (req: Request, res: Response) => {
    const currentUser = getAuthUser(req);
    if (currentUser.role !== 'Admin') {
      return res.status(403).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.3',
        title: 'Forbidden',
        status: 403,
        detail: 'Yêu cầu quyền Quản trị viên (Admin) để tạo danh mục mới.',
        instance: '/api/v1/categories',
      });
    }

    const { name, description, imageUrl } = req.body || {};

    if (!name || typeof name !== 'string' || name.trim().length < 2 || name.trim().length > 50) {
      return res.status(422).json({
        type: 'https://tools.ietf.org/html/rfc4918#section-11.2',
        title: 'Unprocessable Entity',
        status: 422,
        detail: 'Tên danh mục phải có độ dài từ 2 đến 50 ký tự (FR-CAT-003).',
        errors: { name: ['Tên danh mục phải từ 2 đến 50 ký tự.'] },
        instance: '/api/v1/categories',
      });
    }

    const htmlTagRegex = /<[^>]*>/g;
    if (htmlTagRegex.test(name) || (description && htmlTagRegex.test(description))) {
      return res.status(422).json({
        type: 'https://tools.ietf.org/html/rfc4918#section-11.2',
        title: 'Unprocessable Entity',
        status: 422,
        detail: 'Tên danh mục và mô tả không được chứa mã HTML.',
        errors: { name: ['Không được chứa thẻ HTML.'] },
        instance: '/api/v1/categories',
      });
    }

    const trimmedName = name.trim();
    const existingName = dbCategories.find(
      (c) => c.name.toLowerCase() === trimmedName.toLowerCase()
    );
    if (existingName) {
      return res.status(409).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.8',
        title: 'Conflict',
        status: 409,
        detail: `Tên danh mục '${trimmedName}' đã tồn tại trong hệ thống.`,
        instance: '/api/v1/categories',
      });
    }

    const slug = generateUniqueCategorySlug(trimmedName);
    const newCategory: Category = {
      id: `cat-${Date.now()}`,
      name: trimmedName,
      slug,
      description: description ? description.trim() : `Danh mục ${trimmedName} món ăn chọn lọc.`,
      imageUrl: imageUrl && imageUrl.trim() ? imageUrl.trim() : 'https://images.unsplash.com/photo-1546069901-ba9599a7e63c?auto=format&fit=crop&w=1200&q=80',
      recipeCount: 0,
      orderIndex: dbCategories.length + 1,
    };

    dbCategories.push(newCategory);
    memoryCache.remove('categories:all');

    res.setHeader('Location', `/api/v1/categories/${newCategory.slug}`);
    return res.status(201).json(newCategory);
  });

  // -----------------------------------------------------------------------
  // Admin Cập Nhật Danh Mục
  // PUT /api/v1/categories/:id
  // -----------------------------------------------------------------------
  app.put('/api/v1/categories/:id', (req: Request, res: Response) => {
    const currentUser = getAuthUser(req);
    if (currentUser.role !== 'Admin') {
      return res.status(403).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.3',
        title: 'Forbidden',
        status: 403,
        detail: 'Chỉ Quản trị viên mới có thể cập nhật danh mục.',
      });
    }

    const { id } = req.params;
    const catIndex = dbCategories.findIndex((c) => c.id === id);
    if (catIndex === -1) {
      return res.status(404).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.4',
        title: 'Not Found',
        status: 404,
        detail: 'Không tìm thấy danh mục.',
      });
    }

    const { name, description, imageUrl, orderIndex } = req.body;
    if (name) dbCategories[catIndex].name = name.trim();
    if (description !== undefined) dbCategories[catIndex].description = description.trim();
    if (imageUrl) dbCategories[catIndex].imageUrl = imageUrl.trim();
    if (orderIndex !== undefined) dbCategories[catIndex].orderIndex = Number(orderIndex);

    memoryCache.remove('categories:all');
    return res.status(200).json(dbCategories[catIndex]);
  });

  // -----------------------------------------------------------------------
  // Admin Xóa Danh Mục
  // DELETE /api/v1/categories/:id
  // -----------------------------------------------------------------------
  app.delete('/api/v1/categories/:id', (req: Request, res: Response) => {
    const currentUser = getAuthUser(req);
    if (currentUser.role !== 'Admin') {
      return res.status(403).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.3',
        title: 'Forbidden',
        status: 403,
        detail: 'Chỉ Quản trị viên mới có thể xóa danh mục.',
      });
    }

    const { id } = req.params;
    dbCategories = dbCategories.filter((c) => c.id !== id);
    memoryCache.remove('categories:all');
    return res.status(200).json({ success: true, message: 'Đã xóa danh mục thành công.' });
  });

  // -----------------------------------------------------------------------
  // RECIPES API (FR-RCP-001, FR-RCP-002)
  // GET /api/v1/recipes
  // Hỗ trợ tìm kiếm, lọc theo category, difficulty, maxCookTime, sắp xếp & phân trang
  // -----------------------------------------------------------------------
  app.get('/api/v1/recipes', (req: Request, res: Response) => {
    const currentUser = getAuthUser(req);

    const {
      searchTerm,
      categoryId,
      difficulty,
      maxCookTime,
      sort,
      status,
      page = '1',
      pageSize = '12',
    } = req.query;

    const pageNum = Math.max(1, parseInt(page as string) || 1);
    const limitNum = Math.max(1, Math.min(100, parseInt(pageSize as string) || 12));

    let filtered = dbRecipes.filter((r) => {
      // Role visibility
      if (currentUser.role === 'Admin') {
        // Admin sees all
      } else if (currentUser.role === 'Author') {
        // Author sees published + own
        if (r.status !== 'Published' && r.authorId !== currentUser.id) return false;
      } else {
        // Guest only sees published
        if (r.status !== 'Published') return false;
      }

      // Explicit status filter
      if (status && r.status !== status) return false;

      // Category filter (match ID or slug)
      if (categoryId && categoryId !== 'all') {
        const catObj = dbCategories.find((c) => c.id === categoryId || c.slug === categoryId);
        if (!catObj || r.categoryId !== catObj.id) return false;
      }

      // Difficulty
      if (difficulty && difficulty !== 'All') {
        if (r.difficulty !== difficulty) return false;
      }

      // Max cook time
      if (maxCookTime) {
        const maxMins = parseInt(maxCookTime as string);
        if (!isNaN(maxMins) && maxMins > 0 && r.cookTimeMinutes > maxMins) return false;
      }

      // Search term
      if (searchTerm && (searchTerm as string).trim()) {
        const q = unaccent((searchTerm as string).trim());
        const titleMatch = unaccent(r.title).includes(q);
        const descMatch = unaccent(r.description).includes(q);
        const ingMatch = r.ingredients?.some((ing) => unaccent(ing.name).includes(q));
        if (!titleMatch && !descMatch && !ingMatch) return false;
      }

      return true;
    });

    // Sorting
    filtered.sort((a, b) => {
      if (sort === 'createdAt') {
        return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
      }
      if (sort === 'title') {
        return a.title.localeCompare(b.title, 'vi');
      }
      if (sort === 'cookTime') {
        return a.cookTimeMinutes - b.cookTimeMinutes;
      }
      if (sort === 'views') {
        return (b.viewCount || 0) - (a.viewCount || 0);
      }
      // default: -createdAt
      return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
    });

    const totalCount = filtered.length;
    const totalPages = Math.ceil(totalCount / limitNum) || 1;
    const skip = (pageNum - 1) * limitNum;
    const items = filtered.slice(skip, skip + limitNum);

    // Enrich items with author & category details
    const enrichedItems = items.map((r) => {
      const cat = dbCategories.find((c) => c.id === r.categoryId);
      const author = dbUsers.find((u) => u.id === r.authorId) || r.author;
      return {
        ...r,
        category: cat,
        author,
      };
    });

    return res.status(200).json({
      items: enrichedItems,
      totalCount,
      page: pageNum,
      pageSize: limitNum,
      totalPages,
    });
  });

  // -----------------------------------------------------------------------
  // GET /api/v1/recipes/:slugOrId
  // Xem chi tiết công thức, tự động tăng viewCount
  // -----------------------------------------------------------------------
  app.get('/api/v1/recipes/:slugOrId', (req: Request, res: Response) => {
    const { slugOrId } = req.params;
    const recipe = dbRecipes.find((r) => r.slug === slugOrId || r.id === slugOrId);

    if (!recipe) {
      return res.status(404).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.4',
        title: 'Not Found',
        status: 404,
        detail: `Không tìm thấy công thức với định danh '${slugOrId}'.`,
        instance: `/api/v1/recipes/${slugOrId}`,
      });
    }

    const currentUser = getAuthUser(req);

    // Check visibility for non-published recipes
    if (recipe.status !== 'Published') {
      const isOwner = currentUser.id === recipe.authorId || currentUser.role === 'Admin';
      if (!isOwner) {
        return res.status(403).json({
          type: 'https://tools.ietf.org/html/rfc7231#section-6.5.3',
          title: 'Forbidden',
          status: 403,
          detail: 'Công thức này đang ở trạng thái bản nháp hoặc đã lưu trữ. Chỉ tác giả mới có quyền xem.',
        });
      }
    }

    // Increment viewCount
    recipe.viewCount = (recipe.viewCount || 0) + 1;

    const cat = dbCategories.find((c) => c.id === recipe.categoryId);
    const author = dbUsers.find((u) => u.id === recipe.authorId) || recipe.author;

    return res.status(200).json({
      ...recipe,
      category: cat,
      author,
    });
  });

  // -----------------------------------------------------------------------
  // POST /api/v1/recipes
  // Tạo công thức mới (Yêu cầu vai trò Author hoặc Admin)
  // -----------------------------------------------------------------------
  app.post('/api/v1/recipes', (req: Request, res: Response) => {
    const currentUser = getAuthUser(req);
    if (currentUser.role === 'Guest') {
      return res.status(403).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.3',
        title: 'Forbidden',
        status: 403,
        detail: 'Bạn cần đăng nhập để tạo công thức.',
      });
    }

    const data = req.body || {};
    if (!data.title || typeof data.title !== 'string' || data.title.trim().length < 5) {
      return res.status(422).json({
        type: 'https://tools.ietf.org/html/rfc4918#section-11.2',
        title: 'Unprocessable Entity',
        status: 422,
        detail: 'Tiêu đề công thức phải từ 5 ký tự trở lên.',
        errors: { title: ['Tiêu đề phải từ 5 ký tự trở lên.'] },
      });
    }

    if (!data.categoryId) {
      return res.status(422).json({
        type: 'https://tools.ietf.org/html/rfc4918#section-11.2',
        title: 'Unprocessable Entity',
        status: 422,
        detail: 'Vui lòng chọn danh mục cho công thức.',
        errors: { categoryId: ['Danh mục không được để trống.'] },
      });
    }

    const now = new Date().toISOString();
    const slug = generateUniqueRecipeSlug(data.title.trim());
    const newId = `recipe-${Date.now()}`;

    const newRecipe: Recipe = {
      id: newId,
      title: data.title.trim(),
      slug,
      description: data.description?.trim() || '',
      instructions: data.instructions?.trim() || '',
      prepTimeMinutes: Number(data.prepTimeMinutes) || 15,
      cookTimeMinutes: Number(data.cookTimeMinutes) || 30,
      servings: Number(data.servings) || 4,
      difficulty: data.difficulty || 'Medium',
      status: data.status || 'Draft',
      categoryId: data.categoryId,
      authorId: currentUser.id,
      author: currentUser.user,
      createdAt: now,
      updatedAt: now,
      publishedAt: data.status === 'Published' ? now : undefined,
      viewCount: 0,
      likeCount: 0,
      nutrition: data.nutrition || { calories: 350 },
      steps: data.steps || [],
      ingredients: data.ingredients || [],
      images: data.images && data.images.length > 0 ? data.images : [
        {
          id: `img-${Date.now()}`,
          recipeId: newId,
          originalUrl: 'https://images.unsplash.com/photo-1498837167922-ddd27525d352?auto=format&fit=crop&w=1200&q=80',
          isPrimary: true,
          orderIndex: 1,
        }
      ],
    };

    dbRecipes.unshift(newRecipe);
    memoryCache.remove('categories:all');

    res.setHeader('Location', `/api/v1/recipes/${newRecipe.slug}`);
    return res.status(201).json(newRecipe);
  });

  // -----------------------------------------------------------------------
  // PUT /api/v1/recipes/:id
  // Cập nhật công thức (Owner hoặc Admin)
  // -----------------------------------------------------------------------
  app.put('/api/v1/recipes/:id', (req: Request, res: Response) => {
    const { id } = req.params;
    const recipeIndex = dbRecipes.findIndex((r) => r.id === id);

    if (recipeIndex === -1) {
      return res.status(404).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.4',
        title: 'Not Found',
        status: 404,
        detail: 'Không tìm thấy công thức cần cập nhật.',
      });
    }

    const currentUser = getAuthUser(req);
    const existing = dbRecipes[recipeIndex];

    if (currentUser.role !== 'Admin' && existing.authorId !== currentUser.id) {
      return res.status(403).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.3',
        title: 'Forbidden',
        status: 403,
        detail: 'Bạn không có quyền chỉnh sửa công thức này.',
      });
    }

    const data = req.body;
    const now = new Date().toISOString();

    const updatedRecipe: Recipe = {
      ...existing,
      ...data,
      id: existing.id, // Immutable
      authorId: existing.authorId, // Immutable
      updatedAt: now,
      slug: data.title && data.title !== existing.title ? generateUniqueRecipeSlug(data.title) : existing.slug,
    };

    dbRecipes[recipeIndex] = updatedRecipe;
    memoryCache.remove('categories:all');

    return res.status(200).json(updatedRecipe);
  });

  // -----------------------------------------------------------------------
  // DELETE /api/v1/recipes/:id
  // Xóa công thức (Owner hoặc Admin)
  // -----------------------------------------------------------------------
  app.delete('/api/v1/recipes/:id', (req: Request, res: Response) => {
    const { id } = req.params;
    const recipe = dbRecipes.find((r) => r.id === id);

    if (!recipe) {
      return res.status(404).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.4',
        title: 'Not Found',
        status: 404,
        detail: 'Không tìm thấy công thức cần xóa.',
      });
    }

    const currentUser = getAuthUser(req);
    if (currentUser.role !== 'Admin' && recipe.authorId !== currentUser.id) {
      return res.status(403).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.3',
        title: 'Forbidden',
        status: 403,
        detail: 'Bạn không có quyền xóa công thức này.',
      });
    }

    dbRecipes = dbRecipes.filter((r) => r.id !== id);
    memoryCache.remove('categories:all');

    return res.status(200).json({ success: true, message: 'Đã xóa công thức thành công.' });
  });

  // -----------------------------------------------------------------------
  // PATCH /api/v1/recipes/:id/status
  // Đổi trạng thái: Published, Draft, Archived
  // Quy tắc nghiệp vụ: Khi Published, bắt buộc phải có ít nhất 1 bước và 1 nguyên liệu!
  // -----------------------------------------------------------------------
  app.patch('/api/v1/recipes/:id/status', (req: Request, res: Response) => {
    const { id } = req.params;
    const { status } = req.body as { status: RecipeStatus };

    const recipeIndex = dbRecipes.findIndex((r) => r.id === id);
    if (recipeIndex === -1) {
      return res.status(404).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.4',
        title: 'Not Found',
        status: 404,
        detail: 'Không tìm thấy công thức.',
      });
    }

    const recipe = dbRecipes[recipeIndex];
    const currentUser = getAuthUser(req);

    if (currentUser.role !== 'Admin' && recipe.authorId !== currentUser.id) {
      return res.status(403).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.3',
        title: 'Forbidden',
        status: 403,
        detail: 'Bạn không có quyền đổi trạng thái công thức này.',
      });
    }

    if (status === 'Published') {
      if (!recipe.steps || recipe.steps.length === 0) {
        return res.status(422).json({
          type: 'https://tools.ietf.org/html/rfc4918#section-11.2',
          title: 'Unprocessable Entity',
          status: 422,
          detail: 'Quy tắc xuất bản: Công thức bắt buộc phải có ít nhất 1 bước thực hiện.',
          errors: { steps: ['Cần ít nhất 1 bước hướng dẫn.'] },
        });
      }
      if (!recipe.ingredients || recipe.ingredients.length === 0) {
        return res.status(422).json({
          type: 'https://tools.ietf.org/html/rfc4918#section-11.2',
          title: 'Unprocessable Entity',
          status: 422,
          detail: 'Quy tắc xuất bản: Công thức bắt buộc phải có ít nhất 1 nguyên liệu.',
          errors: { ingredients: ['Cần ít nhất 1 nguyên liệu.'] },
        });
      }
    }

    const now = new Date().toISOString();
    recipe.status = status;
    recipe.updatedAt = now;
    if (status === 'Published' && !recipe.publishedAt) {
      recipe.publishedAt = now;
    }

    dbRecipes[recipeIndex] = recipe;
    memoryCache.remove('categories:all');

    return res.status(200).json(recipe);
  });

  // -----------------------------------------------------------------------
  // POST /api/v1/recipes/:id/like
  // Toggle like
  // -----------------------------------------------------------------------
  app.post('/api/v1/recipes/:id/like', (req: Request, res: Response) => {
    const { id } = req.params;
    const recipe = dbRecipes.find((r) => r.id === id);

    if (!recipe) {
      return res.status(404).json({
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.4',
        title: 'Not Found',
        status: 404,
        detail: 'Không tìm thấy công thức.',
      });
    }

    recipe.likeCount = (recipe.likeCount || 0) + 1;
    return res.status(200).json({ id: recipe.id, likeCount: recipe.likeCount });
  });

  // -----------------------------------------------------------------------
  // Cache Status & Invalidate helper endpoints
  // -----------------------------------------------------------------------
  app.get('/api/v1/cache/status', (req: Request, res: Response) => {
    res.json({
      cacheKeys: memoryCache.getEntries(),
    });
  });

  app.post('/api/v1/cache/invalidate', (req: Request, res: Response) => {
    const key = req.body?.key || 'categories:all';
    const removed = memoryCache.remove(key);
    res.json({ success: true, removedKey: key, wasCached: removed });
  });

  // -----------------------------------------------------------------------
  // Vite middleware for development
  // -----------------------------------------------------------------------
  if (process.env.NODE_ENV !== 'production') {
    const vite = await createViteServer({
      server: { middlewareMode: true },
      appType: 'spa',
    });
    app.use(vite.middlewares);
  } else {
    const distPath = path.join(process.cwd(), 'dist');
    app.use(express.static(distPath));
    app.get('*', (req, res) => {
      res.sendFile(path.join(distPath, 'index.html'));
    });
  }

  app.listen(PORT, '0.0.0.0', () => {
    console.log(`Culinary Blog Server running on port ${PORT}`);
  });
}

startServer();
