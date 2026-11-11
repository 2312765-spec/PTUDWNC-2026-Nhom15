/** Nhãn tiếng Việt cho `RecipeDifficulty` của API. */
export const DIFFICULTY_LABEL: Record<string, string> = {
  Easy: 'Dễ',
  Medium: 'Trung bình',
  Hard: 'Khó',
  Expert: 'Rất khó',
};

export function difficultyLabel(difficulty: string): string {
  return DIFFICULTY_LABEL[difficulty] ?? difficulty;
}

/** D97 — "45 phút", "1 giờ", "3 giờ 40 phút". */
export function formatMinutes(total: number): string {
  if (total < 60) {
    return `${total} phút`;
  }
  const hours = Math.floor(total / 60);
  const minutes = total % 60;
  return minutes === 0 ? `${hours} giờ` : `${hours} giờ ${minutes} phút`;
}
