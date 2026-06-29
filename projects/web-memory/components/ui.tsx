import type { ButtonHTMLAttributes } from 'react';

// Shared button primitives used by both the side panel and the in-page (shadow-root) UI,
// so every action looks and behaves consistently (padding, radius, hover/active/focus).

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';
type Size = 'sm' | 'md';

const BTN_BASE =
  'inline-flex select-none items-center justify-center gap-1.5 rounded-lg font-medium ' +
  'transition-all duration-150 active:scale-[0.98] focus-visible:outline-none ' +
  'focus-visible:ring-2 focus-visible:ring-offset-1 disabled:pointer-events-none disabled:opacity-50';

const BTN_VARIANTS: Record<Variant, string> = {
  primary: 'bg-accent-600 text-white shadow-sm hover:bg-accent-700 focus-visible:ring-accent-500',
  secondary:
    'border border-slate-200 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50 focus-visible:ring-accent-400',
  ghost: 'text-slate-600 hover:bg-slate-100 hover:text-slate-800 focus-visible:ring-slate-300',
  danger:
    'border border-slate-200 bg-white text-red-600 hover:border-red-200 hover:bg-red-50 focus-visible:ring-red-400',
};

const BTN_SIZES: Record<Size, string> = {
  sm: 'h-7 px-2.5 text-xs',
  md: 'h-9 px-3.5 text-[13px]',
};

export function Button({
  variant = 'secondary',
  size = 'md',
  className = '',
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant; size?: Size }) {
  return (
    <button className={`${BTN_BASE} ${BTN_VARIANTS[variant]} ${BTN_SIZES[size]} ${className}`} {...rest} />
  );
}

type IconVariant = 'default' | 'danger' | 'active';

const ICON_VARIANTS: Record<IconVariant, string> = {
  default: 'text-slate-400 hover:bg-slate-100 hover:text-slate-700 focus-visible:ring-slate-300',
  danger: 'text-slate-400 hover:bg-red-50 hover:text-red-600 focus-visible:ring-red-300',
  active: 'text-amber-500 hover:bg-amber-50 hover:text-amber-600 focus-visible:ring-amber-300',
};

export function IconButton({
  variant = 'default',
  className = '',
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: IconVariant }) {
  return (
    <button
      className={`inline-flex h-7 w-7 items-center justify-center rounded-md transition-all duration-150 active:scale-90 focus-visible:outline-none focus-visible:ring-2 ${ICON_VARIANTS[variant]} ${className}`}
      {...rest}
    />
  );
}
