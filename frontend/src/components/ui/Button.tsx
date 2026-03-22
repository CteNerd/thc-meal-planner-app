import type { ButtonHTMLAttributes, ReactNode } from 'react';

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  children: ReactNode;
  variant?: 'primary' | 'secondary' | 'ghost';
};

const variants: Record<NonNullable<ButtonProps['variant']>, string> = {
  primary: 'bg-sky-400 text-slate-950 hover:bg-sky-300',
  secondary: 'bg-amber-300 text-slate-900 hover:bg-amber-200',
  ghost: 'bg-white/70 text-slate-700 ring-1 ring-slate-200 hover:bg-white'
};

export function Button({ children, className = '', variant = 'primary', ...props }: ButtonProps) {
  return (
    <button
      className={[
        'inline-flex items-center justify-center rounded-full px-4 py-2 text-sm font-semibold transition',
        'disabled:cursor-not-allowed disabled:opacity-50',
        variants[variant],
        className
      ].join(' ')}
      {...props}
    >
      {children}
    </button>
  );
}
