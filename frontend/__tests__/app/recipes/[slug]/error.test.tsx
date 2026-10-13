import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import RecipeDetailError from '@/app/(public)/recipes/[slug]/error';

describe('/recipes/[slug] error.tsx — NFR-USE-003', () => {
  it('NFR-USE-003: báo lỗi tải công thức (không phải "không tìm thấy") và cho thử lại', async () => {
    const reset = jest.fn();
    jest.spyOn(console, 'error').mockImplementation(() => {});

    render(<RecipeDetailError error={new Error('fetch failed')} reset={reset} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Không tải được công thức');
    expect(screen.queryByText(/Không tìm thấy/)).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(reset).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('link', { name: 'Về danh sách công thức' })).toHaveAttribute('href', '/recipes');
  });
});
