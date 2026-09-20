import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/** Gộp class Tailwind, xử lý xung đột. Dùng trong mọi component UI. */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
