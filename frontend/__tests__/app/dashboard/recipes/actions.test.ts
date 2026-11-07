/**
 * @jest-environment node
 */
import { updateTag } from 'next/cache';
import { revalidateRecipeAction } from '@/app/dashboard/recipes/actions';
import { auth } from '@/auth';

jest.mock('next/cache', () => ({ updateTag: jest.fn() }));
jest.mock('@/auth', () => ({ auth: jest.fn() }));
const mockUpdateTag = updateTag as jest.MockedFunction<typeof updateTag>;
const mockAuth = auth as unknown as jest.Mock;

beforeEach(() => {
  mockUpdateTag.mockReset();
  mockAuth.mockReset();
});

describe('revalidateRecipeAction — D80 (FR-RCP-005/006/007)', () => {
  it('D80: đã đăng nhập → xóa ngay tag recipes và recipe:{slug}', async () => {
    mockAuth.mockResolvedValue({ user: { id: 'u1' } });

    await revalidateRecipeAction('pho-bo-ha-noi-2');

    expect(mockUpdateTag).toHaveBeenCalledWith('recipes');
    expect(mockUpdateTag).toHaveBeenCalledWith('recipe:pho-bo-ha-noi-2');
    expect(mockUpdateTag).toHaveBeenCalledTimes(2);
  });

  it('D80: chưa đăng nhập → không xóa cache gì', async () => {
    mockAuth.mockResolvedValue(null);

    await revalidateRecipeAction('pho-bo-ha-noi');

    expect(mockUpdateTag).not.toHaveBeenCalled();
  });

  it.each(['', 'Pho Bo', '../x', 'a--b', '-a', 'a'.repeat(211), 'phở'])(
    'D72/D80: slug sai dạng %p → chỉ xóa tag recipes',
    async (slug) => {
      mockAuth.mockResolvedValue({ user: { id: 'u1' } });

      await revalidateRecipeAction(slug);

      expect(mockUpdateTag).toHaveBeenCalledTimes(1);
      expect(mockUpdateTag).toHaveBeenCalledWith('recipes');
    },
  );
});
