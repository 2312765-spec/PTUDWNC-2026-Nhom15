import { ApiError } from '@/lib/categories';
import { getRecipes, searchRecipes, toRecipeListQuery } from '@/lib/recipes';

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
