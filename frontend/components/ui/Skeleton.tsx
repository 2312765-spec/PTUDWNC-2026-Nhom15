import { type HTMLAttributes } from 'react';
import { cn } from '@/lib/utils';

export interface SkeletonProps extends HTMLAttributes<HTMLDivElement> {
  circle?: boolean;
}

/** NFR-USE-004 — placeholder loading, không bao giờ để màn hình trắng khi chờ dữ liệu. */
export function Skeleton({ className, circle = false, ...props }: SkeletonProps) {
  return (
    <div
      aria-hidden="true"
      className={cn(
        'animate-pulse bg-border',
        circle ? 'rounded-full' : 'rounded-md',
        className,
      )}
      {...props}
    />
  );
}
