import type { ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import { Header } from './Header';

const pageHelpByPath: Record<string, { title: string; description: string }> = {
  '/dashboard': {
    title: 'Dashboard Overview',
    description: 'Use this page to quickly confirm meal plan, grocery, cookbook, and chat status before drilling into details.'
  },
  '/meal-plans': {
    title: 'Meal Planning Workflow',
    description:
      'Click "Generate" to build the week from your cookbook. For any slot, click "Suggest" — the AI ranks your existing recipes for that slot AND proposes brand-new meal ideas based on your family profiles (allergies, preferred foods, dietary style). New ideas appear below the swap dropdown with an "Add to cookbook" link. Use the swap dropdown to instantly update a slot with any cookbook recipe.'
  },
  '/cookbook': {
    title: 'Cookbook Management',
    description: 'Browse recipes, mark favorites, and add new entries from URL, photo, or manual form.'
  },
  '/grocery-list': {
    title: 'Grocery Operations',
    description: 'Track list progress, toggle in-stock items, and keep pantry staples aligned with meal-plan demand.'
  },
  '/chat': {
    title: 'Assistant Controls',
    description: 'Ask for planning updates, recipe help, and grocery adjustments. Confirm prompts before destructive actions.'
  },
  '/profile': {
    title: 'Family Profile Settings',
    description: 'Maintain allergy, preference, and household constraints so meal planning and suggestions stay accurate.'
  }
};

export function AppShell({ children }: { children: ReactNode }) {
  const location = useLocation();
  const matchedEntry = Object.entries(pageHelpByPath).find(([path]) => location.pathname.startsWith(path));
  const helper = matchedEntry?.[1];

  return (
    <div className="mx-auto flex min-h-screen w-full max-w-6xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
      <Header />
      {helper ? (
        <section className="rounded-2xl border border-sky-100 bg-sky-50 px-4 py-3 text-sm text-sky-900">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-sky-700">{helper.title}</p>
          <p className="mt-1">{helper.description}</p>
        </section>
      ) : null}
      <main>{children}</main>
    </div>
  );
}
