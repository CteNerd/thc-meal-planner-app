import type { InputHTMLAttributes } from 'react';

type InputProps = InputHTMLAttributes<HTMLInputElement> & {
  hasError?: boolean;
};

export function Input({ hasError, ...props }: InputProps) {
  const borderClasses = hasError
    ? 'border-red-400 focus:border-red-500 focus:ring-red-100'
    : 'border-slate-200 focus:border-sky-400 focus:ring-sky-100';

  return (
    <input
      className={`w-full rounded-2xl border bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:ring-4 ${borderClasses}`}
      {...props}
    />
  );
}
