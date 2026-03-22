import { LoginForm } from '../components/auth/LoginForm';

export function LoginPage() {
  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-10 sm:px-6">
      <div className="grid w-full max-w-6xl gap-8 lg:grid-cols-[1.1fr_0.9fr] lg:items-center">
        <section className="space-y-6">
          <p className="text-xs uppercase tracking-[0.3em] text-slate-500">Family Meal Operations</p>
          <h1 className="max-w-xl text-4xl font-bold tracking-tight text-slate-900 sm:text-5xl">
            Plan the week, protect dietary constraints, and keep the grocery list in sync.
          </h1>
          <p className="max-w-xl text-base text-slate-600 sm:text-lg">
            This frontend foundation gives us routing, Tailwind, tests, and a placeholder auth flow so the real Cognito integration can slot in cleanly.
          </p>
          <div className="grid gap-4 sm:grid-cols-3">
            <Feature label="Meal plans" value="Weekly view" />
            <Feature label="Cookbook" value="Recipe import" />
            <Feature label="Grocery" value="Live sync" />
          </div>
        </section>
        <div className="flex justify-center lg:justify-end">
          <LoginForm />
        </div>
      </div>
    </main>
  );
}

function Feature({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[24px] bg-white/80 p-4 shadow-[0_10px_30px_rgba(15,23,42,0.06)] ring-1 ring-slate-200/80 backdrop-blur-sm">
      <p className="text-xs uppercase tracking-[0.18em] text-slate-500">{label}</p>
      <p className="mt-2 text-lg font-semibold text-slate-900">{value}</p>
    </div>
  );
}
