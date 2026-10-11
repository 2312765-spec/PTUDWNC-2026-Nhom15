import { ApiError } from '@/lib/categories';
import { getLatestRecipes, getRecipeBySlug, getRecipes, searchRecipes, toRecipeListQuery } from '@/lib/recipes';

const mockFetch = jest.fn();

beforeEach(() => {
  mockFetch.mockReset();
  global.fetch = mockFetch as unknown as typeof fetch;
});

const jsonResponse = (status: number, body: unknown) =>
  ({ ok: status >= 200 && status < 300, status, json: async () => body }) as Response;

describe('lib/recipes — FR-SRCH-001', () => {
  it('FR-SRCH-001: gọi /recipes/search với q đã encode, no-store (SRS 5.1 SSR)', async () => {
    const paged = { items: [], totalCount: 0, page: 2, pageSize: 12, totalPages: 0 };
    mockFetch.mockResolvedValue(jsonResponse(200, paged));

    await expect(searchRecipes('phở bò & cá', 2)).resolves.toEqual(paged);

    const [url, init] = mockFetch.mock.calls[0];
    expect(url).toContain('/recipes/search?');
    expect(new URL(url).searchParams.get('q')).toBe('phở bò & cá');
    expect(new URL(url).searchParams.get('page')).toBe('2');
    expect(new URL(url).searchParams.get('pageSize')).toBe('12');
    expect(init).toEqual({ cache: 'no-store' });
  });

  it('NFR-USE-003: lỗi API → ApiError mang Application Error Code từ RFC 7807 type', async () => {
    mockFetch.mockResolvedValue(jsonResponse(400, { type: 'VALIDATION_ERROR' }));

    await expect(searchRecipes('a')).rejects.toEqual(new ApiError(400, 'VALIDATION_ERROR'));
  });

  it('NFR-USE-003: body lỗi không phải JSON → UNKNOWN_ERROR', async () => {
    mockFetch.mockResolvedValue({
      ok: false,
      status: 502,
      json: async () => {
        throw new SyntaxError('bad json');
      },
    } as unknown as Response);

    await expect(searchRecipes('pho')).rejects.toMatchObject({ status: 502, code: 'UNKNOWN_ERROR' });
  });
});

describe('lib/recipes — FR-RCP-001 / FR-SRCH-002/003/004', () => {
  it('FR-SRCH-002/003/004: query bỏ trường rỗng, sort mặc định và page 1', () => {
    expect(
      toRecipeListQuery({ categoryId: '', difficulty: '', sort: '-createdAt', page: 1 }).toString(),
    ).toBe('');
  });

  it('FR-SRCH-002/003/004: query giữ đủ filter (D14 có minServings), sort và page > 1', () => {
    const query = toRecipeListQuery(
      { categoryId: 'c1', difficulty: 'Expert', maxCookTime: '30', minServings: '2', sort: 'title', page: 3 },
      12,
    );

    expect(Object.fromEntries(query)).toEqual({
      categoryId: 'c1',
      difficulty: 'Expert',
      maxCookTime: '30',
      minServings: '2',
      sort: 'title',
      page: '3',
      pageSize: '12',
    });
  });

  it('FR-RCP-001: gọi GET /recipes với filter, no-store (SRS 5.1 SSR)', async () => {
    mockFetch.mockResolvedValue(jsonResponse(200, { items: [] }));

    await getRecipes({ difficulty: 'Easy', page: 2 });

    const [url, init] = mockFetch.mock.calls[0];
    const parsed = new URL(url);
    expect(parsed.pathname).toMatch(/\/recipes$/);
    expect(Object.fromEntries(parsed.searchParams)).toEqual({ difficulty: 'Easy', page: '2', pageSize: '12' });
    expect(init).toEqual({ cache: 'no-store' });
  });

  it('NFR-USE-003/D4: bộ lọc sai → ApiError VALIDATION_ERROR', async () => {
    mockFetch.mockResolvedValue(jsonResponse(400, { type: 'VALIDATION_ERROR' }));

    await expect(getRecipes({ sort: 'servings' })).rejects.toEqual(new ApiError(400, 'VALIDATION_ERROR'));
  });
});

describe('lib/recipes — trang chủ (FR-RCP-001)', () => {
  it('SRS 5.1: getLatestRecipes gọi /recipes mới nhất với pageSize, ISR revalidate 3600 (không no-store)', async () => {
    const paged = { items: [], totalCount: 0, page: 1, pageSize: 6, totalPages: 0 };
    mockFetch.mockResolvedValue(jsonResponse(200, paged));

    await expect(getLatestRecipes(6)).resolves.toEqual(paged);

    const [url, init] = mockFetch.mock.calls[0];
    const parsed = new URL(url);
    expect(parsed.pathname).toMatch(/\/recipes$/);
    expect(parsed.searchParams.get('pageSize')).toBe('6');
    // Sort mặc định của backend là -createdAt (mới nhất) → không gửi, giữ cache key ổn định.
    expect(parsed.searchParams.has('sort')).toBe(false);
    expect(init).toEqual({ next: { revalidate: 3600 } });
  });

  it('NFR-USE-003: getLatestRecipes lỗi API → ApiError', async () => {
    mockFetch.mockResolvedValue(jsonResponse(500, { type: 'INTERNAL_ERROR' }));

    await expect(getLatestRecipes(6)).rejects.toEqual(new ApiError(500, 'INTERNAL_ERROR'));
  });
});

describe('lib/recipes — chi tiết công thức (FR-RCP-002)', () => {
  it('FR-RCP-002: gọi /recipes/{slug} đúng một lần /api/v1 (không lặp), ISR 300 + tag cache', async () => {
    mockFetch.mockResolvedValue(jsonResponse(200, { slug: 'pho-bo' }));

    await expect(getRecipeBySlug('pho-bo')).resolves.toEqual({ slug: 'pho-bo' });

    const [url, init] = mockFetch.mock.calls[0];
    expect(url).not.toContain('/api/v1/api/v1');
    expect(url).toMatch(/\/api\/v1\/recipes\/pho-bo$/);
    expect(init).toEqual({ next: { revalidate: 300, tags: ['recipes', 'recipe:pho-bo'] } });
  });

  it('FR-RCP-002: slug được encode', async () => {
    mockFetch.mockResolvedValue(jsonResponse(200, {}));

    await getRecipeBySlug('pho bo/x');

    expect(mockFetch.mock.calls[0][0]).toMatch(/\/recipes\/pho%20bo%2Fx$/);
  });

  it.each([404, 403])('FR-RCP-002: backend trả %i (không có / Draft người khác) → null để trang trả 404', async (status) => {
    mockFetch.mockResolvedValue(jsonResponse(status, { type: 'RECIPE_NOT_FOUND' }));

    await expect(getRecipeBySlug('pho-bo')).resolves.toBeNull();
  });

  it('NFR-USE-003: backend lỗi 500 → ném ApiError, KHÔNG giả làm "không tìm thấy"', async () => {
    mockFetch.mockResolvedValue(jsonResponse(500, { type: 'INTERNAL_ERROR' }));

    await expect(getRecipeBySlug('pho-bo')).rejects.toEqual(new ApiError(500, 'INTERNAL_ERROR'));
  });

  it('NFR-USE-003: backend không kết nối được → ném lỗi, KHÔNG trả null', async () => {
    mockFetch.mockRejectedValue(new TypeError('fetch failed'));

    await expect(getRecipeBySlug('pho-bo')).rejects.toThrow('fetch failed');
  });
});
