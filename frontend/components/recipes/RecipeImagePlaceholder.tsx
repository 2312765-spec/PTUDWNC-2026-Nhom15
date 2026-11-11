import { PotIcon } from '@/components/layout/SiteLogo';
import { cn } from '@/lib/utils';

/**
 * D97 — 6 tông nền tối ấm (cùng họ màu D97) + màu icon. Chữ cái đầu màu kem (ink #f7ebdd) trên mọi nền ≥ 11:1.
 * Icon chỉ trang trí (aria-hidden), độ mờ 35%.
 */
const TONES = [
  { background: '#3b2418', icon: '#e8693f' }, // cam đất
  { background: '#33290f', icon: '#f2b544' }, // mù tạt
  { background: '#1f2e24', icon: '#6fbf8b' }, // rau thơm
  { background: '#2e1f2b', icon: '#e07a9a' }, // mận
  { background: '#1d2a33', icon: '#7fb2d6' }, // xanh đêm
  { background: '#3a1f1d', icon: '#ff8f80' }, // ớt
] as const;

/** Cùng slug → cùng tông (ổn định giữa server/client và giữa các trang). */
export function placeholderTone(slug: string): (typeof TONES)[number] {
  let hash = 0;
  for (const ch of slug) {
    hash = (hash * 31 + ch.codePointAt(0)!) >>> 0;
  }
  return TONES[hash % TONES.length];
}

const SIZES = {
  thumb: { letter: 'text-3xl', icon: 'size-12 -right-2 -bottom-2' },
  card: { letter: 'text-6xl', icon: 'size-24 -right-3 -bottom-4' },
  hero: { letter: 'text-8xl md:text-9xl', icon: 'size-40 md:size-56 -right-4 -bottom-6' },
} as const;

/**
 * D97 — khung minh họa cho công thức chưa có ảnh (đăng không cần ảnh theo D3): nền theo slug, chữ cái đầu tên món,
 * icon niêu đất mờ. Lấp đầy khung cha (cha đặt kích thước + bo góc). Trình đọc màn hình chỉ nghe "Chưa có hình ảnh".
 */
export function RecipeImagePlaceholder({
  title,
  slug,
  size = 'card',
  className,
}: {
  title: string;
  slug: string;
  size?: keyof typeof SIZES;
  className?: string;
}) {
  const tone = placeholderTone(slug);
  const initial = [...title.trim()][0]?.toLocaleUpperCase('vi') ?? '';
  const s = SIZES[size];

  return (
    <div
      className={cn('relative flex h-full w-full items-center justify-center overflow-hidden', className)}
      style={{
        backgroundColor: tone.background,
        backgroundImage: 'radial-gradient(rgba(247, 235, 221, 0.06) 1px, transparent 1px)',
        backgroundSize: '14px 14px',
      }}
    >
      <span className="sr-only">Chưa có hình ảnh</span>
      <div aria-hidden="true">
        <span style={{ color: tone.icon }}>
          <PotIcon className={cn('absolute opacity-35', s.icon)} />
        </span>
        <span className={cn('relative font-display font-extrabold leading-none text-ink', s.letter)}>{initial}</span>
      </div>
    </div>
  );
}
