import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Card } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { getApiErrorMessage } from '../services/api';
import { addFavoriteRecipe, deleteRecipe, getRecipe, listFavoriteRecipes, removeFavoriteRecipe } from '../services/recipeApi';
import type { Recipe } from '../types';

export function RecipeDetailPage() {
  const { recipeId = '' } = useParams();
  const navigate = useNavigate();
  const [recipe, setRecipe] = useState<Recipe | null>(null);
  const [isFavorite, setIsFavorite] = useState(false);
  const [checkedIngredients, setCheckedIngredients] = useState<Set<number>>(new Set());
  const [isLoading, setIsLoading] = useState(true);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setIsLoading(true);
        const [recipeResponse, favoritesResponse] = await Promise.all([getRecipe(recipeId), listFavoriteRecipes()]);

        if (!active) {
          return;
        }

        setRecipe(recipeResponse);
        setIsFavorite(favoritesResponse.some((favorite) => favorite.recipeId === recipeId));
      } catch (err) {
        if (active) {
          setError(getApiErrorMessage(err, 'Unable to load recipe details.'));
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
  }, [recipeId]);

  const totalMinutes = useMemo(() => {
    return (recipe?.prepTimeMinutes ?? 0) + (recipe?.cookTimeMinutes ?? 0);
  }, [recipe]);

  async function toggleFavorite() {
    if (!recipe) {
      return;
    }

    try {
      setIsBusy(true);
      setError(null);
      if (isFavorite) {
        await removeFavoriteRecipe(recipe.recipeId);
        setIsFavorite(false);
      } else {
        await addFavoriteRecipe(recipe.recipeId, {});
        setIsFavorite(true);
      }
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to update favorite.'));
    } finally {
      setIsBusy(false);
    }
  }

  async function deleteCurrentRecipe() {
    if (!recipe) {
      return;
    }

    const confirmed = window.confirm(`Delete "${recipe.name}"? This cannot be undone.`);
    if (!confirmed) {
      return;
    }

    try {
      setIsBusy(true);
      setError(null);
      await deleteRecipe(recipe.recipeId);
      navigate('/cookbook');
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to delete recipe.'));
    } finally {
      setIsBusy(false);
    }
  }

  function toggleIngredient(index: number) {
    setCheckedIngredients((current) => {
      const next = new Set(current);
      if (next.has(index)) {
        next.delete(index);
      } else {
        next.add(index);
      }

      return next;
    });
  }

  return (
    <Card>
      {isLoading ? (
        <p className="text-sm text-slate-600">Loading recipe...</p>
      ) : error ? (
        <div className="space-y-3">
          <p className="text-sm text-red-700">{error}</p>
          <Link to="/cookbook" className="text-sm font-semibold text-sky-700 underline underline-offset-4">
            Return to cookbook
          </Link>
        </div>
      ) : recipe ? (
        <div className="space-y-6">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="space-y-2">
              <Link to="/cookbook" className="text-xs font-semibold uppercase tracking-[0.2em] text-sky-700">
                Back to cookbook
              </Link>
              <h2 className="text-3xl font-semibold text-slate-900">{recipe.name}</h2>
              <div className="flex flex-wrap gap-2 text-sm text-slate-500">
                <span>{recipe.category}</span>
                {recipe.cuisine ? <span>{recipe.cuisine}</span> : null}
                {totalMinutes > 0 ? <span>{totalMinutes} min</span> : null}
                {recipe.servings ? <span>{recipe.servings} servings</span> : null}
              </div>
            </div>

            <div className="flex flex-wrap gap-2">
              <Button type="button" variant={isFavorite ? 'secondary' : 'ghost'} onClick={() => void toggleFavorite()} disabled={isBusy}>
                {isFavorite ? 'Favorited' : 'Add to favorites'}
              </Button>
              <Link to={`/cookbook/${recipe.recipeId}/edit`}>
                <Button type="button">Edit recipe</Button>
              </Link>
              <Button type="button" variant="ghost" onClick={() => void deleteCurrentRecipe()} disabled={isBusy}>
                Delete recipe
              </Button>
            </div>
          </div>

          {recipe.imageKey ? (
            <div className="overflow-hidden rounded-3xl bg-slate-100">
              <img src={`/images/${recipe.imageKey}`} alt={recipe.name} className="h-72 w-full object-cover" />
            </div>
          ) : null}

          {recipe.description ? <p className="text-sm leading-6 text-slate-700">{recipe.description}</p> : null}

          {recipe.tags.length > 0 ? (
            <div className="flex flex-wrap gap-2">
              {recipe.tags.map((tag) => (
                <span key={tag} className="rounded-full bg-sky-50 px-3 py-1 text-xs font-semibold text-sky-800">
                  {tag}
                </span>
              ))}
            </div>
          ) : null}

          <div className="grid gap-6 lg:grid-cols-[1.2fr_0.8fr]">
            <section className="space-y-4">
              <h3 className="text-lg font-semibold text-slate-900">Ingredients</h3>
              <ul className="space-y-2">
                {recipe.ingredients.map((ingredient, index) => (
                  <li key={`${ingredient.name}-${index}`} className="flex items-start gap-3 rounded-2xl border border-slate-200 px-4 py-3">
                    <input
                      type="checkbox"
                      checked={checkedIngredients.has(index)}
                      onChange={() => toggleIngredient(index)}
                      aria-label={`Mark ${ingredient.name} prepared`}
                      className="mt-1 h-4 w-4 rounded border-slate-300"
                    />
                    <div className="text-sm text-slate-700">
                      <p className={checkedIngredients.has(index) ? 'line-through text-slate-400' : 'font-medium'}>
                        {[ingredient.quantity, ingredient.unit, ingredient.name].filter(Boolean).join(' ')}
                      </p>
                      {ingredient.section ? <p className="text-xs text-slate-500">{ingredient.section}</p> : null}
                      {ingredient.notes ? <p className="text-xs text-slate-500">{ingredient.notes}</p> : null}
                    </div>
                  </li>
                ))}
              </ul>
            </section>

            <div className="space-y-6">
              <section className="space-y-4">
                <h3 className="text-lg font-semibold text-slate-900">Instructions</h3>
                <ol className="space-y-3">
                  {recipe.instructions.map((instruction, index) => (
                    <li key={`${instruction}-${index}`} className="rounded-2xl border border-slate-200 px-4 py-3 text-sm leading-6 text-slate-700">
                      <span className="mr-2 font-semibold text-slate-900">{index + 1}.</span>
                      {instruction}
                    </li>
                  ))}
                </ol>
              </section>

              {recipe.nutrition ? (
                <section className="rounded-3xl bg-slate-50 p-5">
                  <h3 className="text-lg font-semibold text-slate-900">Nutrition</h3>
                  <div className="mt-4 grid grid-cols-2 gap-3 text-sm text-slate-700">
                    {Object.entries(recipe.nutrition).map(([key, value]) => (
                      <div key={key} className="rounded-2xl bg-white px-4 py-3">
                        <p className="text-xs uppercase tracking-wide text-slate-500">{key}</p>
                        <p className="mt-1 font-semibold text-slate-900">{value ?? 'n/a'}</p>
                      </div>
                    ))}
                  </div>
                </section>
              ) : null}

              {recipe.variations || recipe.storageInfo || recipe.sourceUrl ? (
                <section className="space-y-3 rounded-3xl bg-amber-50 p-5 text-sm text-slate-700">
                  <h3 className="text-lg font-semibold text-slate-900">Notes</h3>
                  {recipe.variations ? <p><span className="font-semibold">Variations:</span> {recipe.variations}</p> : null}
                  {recipe.storageInfo ? <p><span className="font-semibold">Storage:</span> {recipe.storageInfo}</p> : null}
                  {recipe.sourceUrl ? (
                    <p>
                      <span className="font-semibold">Source:</span>{' '}
                      <a href={recipe.sourceUrl} target="_blank" rel="noreferrer" className="text-sky-700 underline underline-offset-4">
                        {recipe.sourceUrl}
                      </a>
                    </p>
                  ) : null}
                </section>
              ) : null}
            </div>
          </div>
        </div>
      ) : null}
    </Card>
  );
}