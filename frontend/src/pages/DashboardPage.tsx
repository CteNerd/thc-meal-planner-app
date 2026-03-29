import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Card } from '../components/ui/Card';
import { getApiErrorMessage } from '../services/api';
import { getChatHistory } from '../services/chatApi';
import { getCurrentGroceryList } from '../services/groceryListApi';
import { getCurrentMealPlan } from '../services/mealPlanApi';
import { listRecipes } from '../services/recipeApi';
import type { ChatHistoryResponse, GroceryList, MealPlan, Recipe } from '../types';

type DashboardState = {
  mealPlan: MealPlan | null;
  groceryList: GroceryList | null;
  recipes: Recipe[];
  chatHistory: ChatHistoryResponse | null;
};

const initialState: DashboardState = {
  mealPlan: null,
  groceryList: null,
  recipes: [],
  chatHistory: null
};

const quickActions = [
  { label: 'Open Meal Plans', href: '/meal-plans' },
  { label: 'Review Grocery List', href: '/grocery-list' },
  { label: 'Browse Cookbook', href: '/cookbook' },
  { label: 'Ask Chat Assistant', href: '/chat' }
];

export function DashboardPage() {
  const [state, setState] = useState<DashboardState>(initialState);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setIsLoading(true);
        setError(null);

        const [mealPlanResult, groceryResult, recipeResult, chatResult] = await Promise.allSettled([
          getCurrentMealPlan(),
          getCurrentGroceryList(),
          listRecipes(),
          getChatHistory(undefined, 5)
        ]);

        if (!active) {
          return;
        }

        setState({
          mealPlan: mealPlanResult.status === 'fulfilled' ? mealPlanResult.value : null,
          groceryList: groceryResult.status === 'fulfilled' ? groceryResult.value : null,
          recipes: recipeResult.status === 'fulfilled' ? recipeResult.value : [],
          chatHistory: chatResult.status === 'fulfilled' ? chatResult.value : null
        });

        const failingResult = [mealPlanResult, groceryResult, recipeResult, chatResult].find(
          (result) => result.status === 'rejected'
        );

        if (failingResult?.status === 'rejected') {
          setError(getApiErrorMessage(failingResult.reason, 'Some dashboard data could not be loaded.'));
        }
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    }

    void load();

    return () => {
      active = false;
    };
  }, []);

  const mealCount = state.mealPlan?.meals.length ?? 0;
  const groceryProgress = state.groceryList?.progress;
  const latestChatSnippet = useMemo(() => {
    const latestMessage = state.chatHistory?.messages.at(-1);
    if (!latestMessage) {
      return 'No chat messages yet. Start a conversation to generate plans and recipes.';
    }

    return latestMessage.content.length > 140
      ? `${latestMessage.content.slice(0, 137)}...`
      : latestMessage.content;
  }, [state.chatHistory?.messages]);

  return (
    <div className="space-y-6">
      <section className="space-y-2">
        <p className="text-xs uppercase tracking-[0.24em] text-slate-500">Dashboard</p>
        <h2 className="text-3xl font-bold text-slate-900">This week at a glance</h2>
        <p className="max-w-2xl text-sm text-slate-600">
          Track planning progress, grocery execution, and conversation momentum from one place.
        </p>
      </section>

      {isLoading ? <p className="text-sm text-slate-500">Loading dashboard summary...</p> : null}
      {error ? <p className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">{error}</p> : null}

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Card className="min-h-52">
          <div className="space-y-3">
            <h3 className="text-lg font-semibold text-slate-900">This Week</h3>
            <p className="text-sm leading-6 text-slate-600">
              {state.mealPlan
                ? `${mealCount} planned meal slots in week ${state.mealPlan.weekStartDate}.`
                : 'No active meal plan yet. Generate a new plan to start the week.'}
            </p>
          </div>
        </Card>

        <Card className="min-h-52">
          <div className="space-y-3">
            <h3 className="text-lg font-semibold text-slate-900">Grocery Progress</h3>
            <p className="text-sm leading-6 text-slate-600">
              {state.groceryList && groceryProgress
                ? `${groceryProgress.completed}/${groceryProgress.total} items completed (${groceryProgress.percentage.toFixed(0)}%).`
                : 'No active grocery list yet. Generate one from your meal plan.'}
            </p>
          </div>
        </Card>

        <Card className="min-h-52">
          <div className="space-y-3">
            <h3 className="text-lg font-semibold text-slate-900">Cookbook</h3>
            <p className="text-sm leading-6 text-slate-600">
              {state.recipes.length > 0
                ? `${state.recipes.length} recipes available for planning and swaps.`
                : 'No recipes found yet. Add recipes to unlock richer planning.'}
            </p>
          </div>
        </Card>

        <Card className="min-h-52">
          <div className="space-y-3">
            <h3 className="text-lg font-semibold text-slate-900">Recent Chat</h3>
            <p className="text-sm leading-6 text-slate-600">{latestChatSnippet}</p>
          </div>
        </Card>
      </section>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {quickActions.map((action) => (
          <Link
            key={action.href}
            to={action.href}
            className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-semibold text-slate-800 shadow-sm transition hover:border-slate-300 hover:bg-slate-50"
          >
            {action.label}
          </Link>
        ))}
      </section>
    </div>
  );
}
