import { Card } from '../components/ui/Card';

const cards = [
  {
    title: 'This Week',
    body: 'Meal plan summary placeholder for the current week with Mon-Sun coverage.'
  },
  {
    title: 'Grocery List',
    body: 'Item counts, completion progress, and quick links will live here.'
  },
  {
    title: 'Quick Actions',
    body: 'Generate new plan, add recipe, and open chat shortcuts.'
  },
  {
    title: 'Recent Chat',
    body: 'Latest chatbot interaction preview will be shown here.'
  }
];

export function DashboardPage() {
  return (
    <div className="space-y-6">
      <section className="space-y-2">
        <p className="text-xs uppercase tracking-[0.24em] text-slate-500">Dashboard</p>
        <h2 className="text-3xl font-bold text-slate-900">Foundation shell is ready</h2>
        <p className="max-w-2xl text-sm text-slate-600">
          Routing, auth placeholders, and design tokens are in place. Next steps are Cognito wiring, API integration, and the feature-specific pages.
        </p>
      </section>

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {cards.map((card) => (
          <Card key={card.title} className="min-h-52">
            <div className="space-y-3">
              <h3 className="text-lg font-semibold text-slate-900">{card.title}</h3>
              <p className="text-sm leading-6 text-slate-600">{card.body}</p>
            </div>
          </Card>
        ))}
      </section>
    </div>
  );
}
