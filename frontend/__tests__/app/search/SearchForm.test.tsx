import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import SearchForm from '@/app/(public)/search/SearchForm';
import { mockPush } from '../../../jest.setup';
import { renderWithProviders } from '../../utils/render';

describe('SearchForm — FR-SRCH-001', () => {
  it('FR-SRCH-001: ô tìm kiếm có nhãn cho trình đọc màn hình (WCAG 2.1 AA)', () => {
    renderWithProviders(<SearchForm initialQuery="pho" />);

    expect(screen.getByRole('searchbox', { name: 'Từ khóa tìm kiếm' })).toHaveValue('pho');
  });

  it('FR-SRCH-001: từ khóa < 2 ký tự → báo lỗi, không đổi URL', async () => {
    renderWithProviders(<SearchForm initialQuery="" />);

    await userEvent.type(screen.getByRole('searchbox'), ' a ');
    await userEvent.click(screen.getByRole('button', { name: 'Tìm kiếm' }));

    expect(screen.getByRole('alert')).toHaveTextContent('ít nhất 2 ký tự');
    expect(mockPush).not.toHaveBeenCalled();
  });

  it('FR-SRCH-001: submit hợp lệ → điều hướng /search?q= (đã trim + encode)', async () => {
    renderWithProviders(<SearchForm initialQuery="" />);

    await userEvent.type(screen.getByRole('searchbox'), '  phở bò ');
    await userEvent.click(screen.getByRole('button', { name: 'Tìm kiếm' }));

    expect(mockPush).toHaveBeenCalledWith(`/search?q=${encodeURIComponent('phở bò')}`);
  });
});
