import { useEffect, useMemo, useState } from 'react';
import { Button } from '../components/ui/Button';
import { Card } from '../components/ui/Card';
import { ApiError, getApiErrorMessage } from '../services/api';
import { generateGroceryList } from '../services/groceryListApi';
import {
  generateMealPlan,
  getCurrentMealPlan,
  getMealPlanHistory,
  getMealSwapSuggestions,
  updateMealPlan
} from '../services/mealPlanApi';
import { listRecipes } from '../services/recipeApi';
import type { MealPlan, MealSlot, MealSwapSuggestion, Recipe } from '../types';

const DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
const MEAL_TYPES = ['breakfast', 'lunch', 'dinner'];

export function MealPlansPage() {
  const [activeTab, setActiveTab] = useState<'current' | 'history'>('current');
  const [currentPlan, setCurrentPlan] = useState<MealPlan | null>(null);
  const [historyPlans, setHistoryPlans] = useState<MealPlan[]>([]);
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [suggestionsBySlot, setSuggestionsBySlot] = useState<Record<string, MealSwapSuggestion[]>>({});
  const [isLoading, setIsLoading] = useState(true);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setIsLoading(true);
        setError(null);

        const [planResponse, historyResponse, recipesResponse] = await Promise.all([
          getCurrentMealPlan(),
          getMealPlanHistory(),
          listRecipes()
        ]);

        if (!active) {
          return;
        }

        setCurrentPlan(planResponse);
        setHistoryPlans(historyResponse);
        setRecipes(recipesResponse);
      } catch (err) {
        if (!active) {
          return;
        }

        // No current plan is a valid state; still show history and recipes.
        if (err instanceof ApiError && err.status === 404) {
          try {
            const [historyResponse, recipesResponse] = await Promise.all([getMealPlanHistory(), listRecipes()]);
            if (!active) {
              return;
            }

            setCurrentPlan(null);
            setHistoryPlans(historyResponse);
            setRecipes(recipesResponse);
          } catch (loadErr) {
            if (active) {
              setError(getApiErrorMessage(loadErr, 'Unable to load meal planning data.'));
            }
          }
        } else {
          setError(getApiErrorMessage(err, 'Unable to load meal planning data.'));
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

  const slotMap = useMemo(() => {
    const map = new Map<string, MealSlot>();
    for (const slot of currentPlan?.meals ?? []) {
      map.set(toSlotKey(slot.day, slot.mealType), slot);
    }
    return map;
  }, [currentPlan]);

  const recipesByMealType = useMemo(() => {
    return {
      breakfast: recipes.filter((recipe) => matchesMealType(recipe, 'breakfast')),
      lunch: recipes.filter((recipe) => matchesMealType(recipe, 'lunch')),
      dinner: recipes.filter((recipe) => matchesMealType(recipe, 'dinner'))
    };
  }, [recipes]);

  async function handleGenerateCurrentWeekPlan() {
    try {
      setIsBusy(true);
      setError(null);
      const weekStartDate = getCurrentWeekMondayIso();

      const generated = await generateMealPlan({
        weekStartDate,
        replaceExisting: true
      });

      setCurrentPlan(generated);
      setHistoryPlans((current) => [generated, ...current.filter((plan) => plan.weekStartDate !== generated.weekStartDate)]);
      setActiveTab('current');

      try {
        await generateGroceryList({
          weekStartDate,
          clearExisting: false
        });
      } catch {
        // Keep meal-plan success even if grocery sync fails; backend also enforces reactivity.
      }
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to generate meal plan.'));
    } finally {
      setIsBusy(false);
    }
  }

  async function handleSwapSlot(day: string, mealType: string, recipeId: string) {
    if (!currentPlan || !recipeId) {
      return;
    }

    try {
      setIsBusy(true);
      setError(null);
      const updated = await updateMealPlan(currentPlan.weekStartDate, {
        meals: [{ day, mealType, recipeId }]
      });

      setCurrentPlan(updated);
      setHistoryPlans((current) => current.map((plan) => (plan.weekStartDate === updated.weekStartDate ? updated : plan)));

      try {
        await generateGroceryList({
          weekStartDate: currentPlan.weekStartDate,
          clearExisting: false
        });
      } catch {
        // Keep meal-plan success even if grocery sync fails; backend also enforces reactivity.
      }
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to swap meal slot.'));
    } finally {
      setIsBusy(false);
    }
  }

  async function handleSuggestSlot(day: string, mealType: string) {
    if (!currentPlan) {
      return;
    }

    try {
      setIsBusy(true);
      setError(null);
      const suggestions = await getMealSwapSuggestions(currentPlan.weekStartDate, day, mealType, 5);
      setSuggestionsBySlot((current) => ({
        ...current,
        [toSlotKey(day, mealType)]: suggestions
      }));
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to load swap suggestions.'));
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <Card>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-2xl font-semibold text-slate-900">Meal Plans</h2>
          <p className="mt-2 text-sm text-slate-600">
            Build the week in one place, swap slots quickly, and keep nutritional and quality scoring in view.
          </p>
        </div>
        <Button type="button" onClick={handleGenerateCurrentWeekPlan} disabled={isBusy || isLoading}>
          {isBusy ? 'Working...' : 'Generate This Week'}
        </Button>
      </div>

      <div className="mt-5 flex flex-wrap gap-2">
        <Button
          type="button"
          variant={activeTab === 'current' ? 'primary' : 'ghost'}
          onClick={() => setActiveTab('current')}
          disabled={isBusy}
        >
          Current Plan
        </Button>
        <Button
          type="button"
          variant={activeTab === 'history' ? 'primary' : 'ghost'}
          onClick={() => setActiveTab('history')}
          disabled={isBusy}
        >
          History
        </Button>
      </div>

      {isLoading ? (
        <p className="mt-4 text-sm text-slate-600">Loading meal plans...</p>
      ) : (
        <>
          {error ? <p className="mt-4 text-sm text-red-700">{error}</p> : null}

          {activeTab === 'current' ? (
            <div className="mt-5 space-y-5">
              {!currentPlan ? (
                <div className="rounded-2xl border border-dashed border-slate-300 bg-white/70 p-5 text-sm text-slate-600">
                  No active weekly plan yet. Generate one for this week to get started.
                </div>
              ) : (
                <>
                  <div className="grid gap-3 md:grid-cols-3">
                    <div className="rounded-2xl bg-slate-50 p-4 ring-1 ring-slate-200">
                      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Week</p>
                      <p className="mt-1 text-sm font-medium text-slate-900">{currentPlan.weekStartDate}</p>
                    </div>
                    <div className="rounded-2xl bg-slate-50 p-4 ring-1 ring-slate-200">
                      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Quality</p>
                      <p className="mt-1 text-sm font-medium text-slate-900">
                        {currentPlan.qualityScore?.grade ?? '-'} ({currentPlan.qualityScore?.overall ?? 0}/100)
                      </p>
                    </div>
                    <div className="rounded-2xl bg-slate-50 p-4 ring-1 ring-slate-200">
                      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Generated By</p>
                      <p className="mt-1 text-sm font-medium text-slate-900">{currentPlan.generatedBy}</p>
                    </div>
                  </div>

                  <div className="overflow-x-auto rounded-2xl ring-1 ring-slate-200">
                    <table className="min-w-[900px] w-full divide-y divide-slate-200 bg-white">
                      <thead className="bg-slate-50">
                        <tr>
                          <th className="px-3 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-600">Meal</th>
                          {DAYS.map((day) => (
                            <th key={day} className="px-3 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-600">
                              {day}
                            </th>
                          ))}
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-200">
                        {MEAL_TYPES.map((mealType) => (
                          <tr key={mealType}>
                            <td className="px-3 py-3 text-sm font-semibold capitalize text-slate-700">{mealType}</td>
                            {DAYS.map((day) => {
                              const slot = slotMap.get(toSlotKey(day, mealType));
                              const candidates = recipesByMealType[mealType as keyof typeof recipesByMealType];
                              const allSuggestions = suggestionsBySlot[toSlotKey(day, mealType)] ?? [];
                              const cookbookSuggestions = allSuggestions.filter((s) => !s.isAiSuggestion);
                              const aiIdeas = allSuggestions.filter((s) => s.isAiSuggestion);
                              const optionPool = cookbookSuggestions.length > 0
                                ? cookbookSuggestions
                                : candidates.map((candidate) => ({
                                    recipeId: candidate.recipeId,
                                    recipeName: candidate.name,
                                    constraintSafe: true,
                                    score: 0,
                                    notes: [] as string[]
                                  }));

                              return (
                                <td key={`${mealType}-${day}`} className="px-3 py-3 align-top">
                                  <div className="space-y-2">
                                    <p className="text-sm font-medium text-slate-900">{slot?.recipeName ?? 'Unassigned'}</p>
                                    <p className="text-xs text-slate-500">
                                      {slot?.prepTime ?? 0}m prep, {slot?.cookTime ?? 0}m cook
                                    </p>
                                    <p className="text-xs text-slate-500">{slot?.nutritionalInfo?.calories ?? 0} kcal</p>
                                    <Button
                                      type="button"
                                      variant="ghost"
                                      className="w-full"
                                      aria-label={`${day} ${mealType} suggest`}
                                      disabled={isBusy || !currentPlan}
                                      onClick={() => {
                                        void handleSuggestSlot(day, mealType);
                                      }}
                                    >
                                      Suggest
                                    </Button>
                                    <select
                                      aria-label={`${day} ${mealType} swap`}
                                      value={slot?.recipeId ?? ''}
                                      onChange={(event) => {
                                        void handleSwapSlot(day, mealType, event.target.value);
                                      }}
                                      className="w-full rounded-xl border border-slate-200 bg-white px-2 py-2 text-xs text-slate-800 outline-none transition focus:border-sky-400 focus:ring-4 focus:ring-sky-100"
                                      disabled={isBusy || optionPool.length === 0}
                                    >
                                      <option value="" disabled>
                                        {optionPool.length === 0 ? 'No recipes available' : 'Swap recipe'}
                                      </option>
                                      {optionPool.map((recipe) => (
                                        <option key={recipe.recipeId} value={recipe.recipeId}>
                                          {recipe.recipeName}{recipe.constraintSafe ? '' : ' (constraint warning)'}
                                        </option>
                                      ))}
                                    </select>
                                    {aiIdeas.length > 0 && (
                                      <details className="rounded-xl border border-violet-200 bg-violet-50 px-3 py-2 text-xs">
                                        <summary className="cursor-pointer font-semibold text-violet-700">
                                          ✨ {aiIdeas.length} new idea{aiIdeas.length !== 1 ? 's' : ''} from AI
                                        </summary>
                                        <ul className="mt-2 space-y-2">
                                          {aiIdeas.map((idea, index) => (
                                            <li key={index} className="space-y-1">
                                              <p className="font-medium text-slate-800">{idea.recipeName}</p>
                                              {idea.aiReason && <p className="text-slate-600">{idea.aiReason}</p>}
                                              <a
                                                href={`/cookbook/new?name=${encodeURIComponent(idea.recipeName)}`}
                                                className="inline-block rounded-lg bg-violet-100 px-2 py-1 text-violet-700 hover:bg-violet-200"
                                              >
                                                + Add to cookbook
                                              </a>
                                            </li>
                                          ))}
                                        </ul>
                                      </details>
                                    )}
                                  </div>
                                </td>
                              );
                            })}
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>

                  <div className="grid gap-3 md:grid-cols-4">
                    <NutritionTile label="Calories/day" value={currentPlan.nutritionalSummary?.dailyAverages?.calories} />
                    <NutritionTile label="Protein/day" value={currentPlan.nutritionalSummary?.dailyAverages?.protein} unit="g" />
                    <NutritionTile label="Carbs/day" value={currentPlan.nutritionalSummary?.dailyAverages?.carbohydrates} unit="g" />
                    <NutritionTile label="Fat/day" value={currentPlan.nutritionalSummary?.dailyAverages?.fat} unit="g" />
                  </div>
                </>
              )}
            </div>
          ) : (
            <div className="mt-5 space-y-3">
              {historyPlans.length === 0 ? (
                <p className="text-sm text-slate-600">No historical meal plans yet.</p>
              ) : (
                historyPlans.map((plan) => (
                  <div key={plan.weekStartDate} className="flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-slate-50 p-4 ring-1 ring-slate-200">
                    <div>
                      <p className="text-sm font-semibold text-slate-900">Week of {plan.weekStartDate}</p>
                      <p className="text-xs text-slate-600">
                        {plan.meals.length} slots, generated by {plan.generatedBy}
                      </p>
                    </div>
                    <div className="text-right">
                      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Quality</p>
                      <p className="text-sm font-medium text-slate-900">
                        {plan.qualityScore?.grade ?? '-'} ({plan.qualityScore?.overall ?? 0}/100)
                      </p>
                    </div>
                  </div>
                ))
              )}
            </div>
          )}
        </>
      )}
    </Card>
  );
}

function NutritionTile({ label, value, unit = '' }: { label: string; value?: number; unit?: string }) {
  return (
    <div className="rounded-2xl bg-sky-50 p-4 ring-1 ring-sky-100">
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-600">{label}</p>
      <p className="mt-1 text-lg font-semibold text-slate-900">
        {value ?? 0}
        {unit}
      </p>
    </div>
  );
}

function matchesMealType(recipe: Recipe, mealType: string): boolean {
  if (!recipe.category) {
    return true;
  }

  return recipe.category.toLowerCase() === mealType.toLowerCase();
}

function toSlotKey(day: string, mealType: string): string {
  return `${day.toLowerCase()}:${mealType.toLowerCase()}`;
}

function getCurrentWeekMondayIso(): string {
  const today = new Date();
  const dayOfWeek = today.getDay(); // Sunday 0 ... Saturday 6
  const offsetToMonday = dayOfWeek === 0 ? -6 : 1 - dayOfWeek;
  const monday = new Date(today);
  monday.setDate(today.getDate() + offsetToMonday);

  const year = monday.getFullYear();
  const month = String(monday.getMonth() + 1).padStart(2, '0');
  const day = String(monday.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}