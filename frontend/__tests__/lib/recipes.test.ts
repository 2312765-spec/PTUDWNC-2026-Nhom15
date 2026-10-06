import { ApiError } from '@/lib/categories';
import { searchRecipes } from '@/lib/recipes';

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
