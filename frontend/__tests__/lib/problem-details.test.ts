import { toProblemDetails, type ProblemDetails } from '@/lib/problem-details';
import { networkError, problemError } from '../utils/problem';

const problem: ProblemDetails = {
  type: 'VALIDATION_ERROR',
  title: 'Dữ liệu không hợp lệ',
  status: 400,
  errors: { email: ['Email không hợp lệ'] },
};

describe('toProblemDetails (NFR-USE-003)', () => {
  it('NFR-USE-003: AxiosError thật có body RFC 7807 → trả nguyên body', () => {
    expect(toProblemDetails(problemError(problem))).toEqual(problem);
  });

  it('NFR-USE-003: lỗi mạng (không response) → NETWORK_ERROR', () => {
    expect(toProblemDetails(networkError()).type).toBe('NETWORK_ERROR');
  });

  it('NFR-USE-003: lỗi không phải axios dù có response.data → NETWORK_ERROR', () => {
    expect(toProblemDetails({ response: { data: problem } }).type).toBe('NETWORK_ERROR');
    expect(toProblemDetails(new Error('x')).type).toBe('NETWORK_ERROR');
    expect(toProblemDetails(null).type).toBe('NETWORK_ERROR');
  });

  it('NFR-PERF-005/D64: module không import axios (form auth bị prefetch trên trang public)', () => {
    jest.isolateModules(() => {
      jest.doMock('axios', () => {
        throw new Error('problem-details không được import axios');
      });
      expect(() => require('@/lib/problem-details')).not.toThrow();
    });
  });
});
