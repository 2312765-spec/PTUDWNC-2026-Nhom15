import Link from 'next/link';
import { cn } from '@/lib/utils';

/** D97 — icon niêu đất (cùng hình với app/icon.svg). Tô bằng `fill-*`; mặc định màu chữ hiện tại. */
export function PotIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className={cn('fill-current', className)}>
      <path d="M3.5 12.5h17c0 4.7-3.8 8-8.5 8s-8.5-3.3-8.5-8z" />
      <path d="M5.5 11c.6-3 3.3-5 6.5-5s5.9 2 6.5 5z" />
      <circle cx="12" cy="4.2" r="1.6" />
      <rect x="1.2" y="13" width="2.8" height="2" rx="1" />
      <rect x="20" y="13" width="2.8" height="2" rx="1" />
    </svg>
  );
}

/** D97 — logo niêu đất + tên trang (thanh bên). */
export function SiteLogo() {
  return (
    <Link href="/" className="inline-flex items-center gap-2.5 self-start text-ink">
      <PotIcon className="size-9 fill-brand-600" />
      <span className="font-display text-2xl font-extrabold leading-none">Culinary Blog</span>
    </Link>
  );
}
