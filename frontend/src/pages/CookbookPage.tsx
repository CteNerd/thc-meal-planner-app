import { Card } from '../components/ui/Card';
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { getApiErrorMessage } from '../services/api';
import { addFavoriteRecipe, deleteRecipe, listFavoriteRecipes, listRecipes, removeFavoriteRecipe } from '../services/recipeApi';
import type { Recipe } from '../types';
import { Input } from '../components/ui/Input';
import { Button } from '../components/ui/Button';

export function CookbookPage() {
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [favoriteRecipeIds, setFavoriteRecipeIds] = useState<Set<string>>(new Set());
  const [searchQuery, setSearchQuery] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [favoritesOnly, setFavoritesOnly] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setIsLoading(true);
        const [recipesResponse, favoritesResponse] = await Promise.all([listRecipes(), listFavoriteRecipes()]);

        if (!active) {
          return;
        }

        setRecipes(recipesResponse);
        setFavoriteRecipeIds(new Set(favoritesResponse.map((favorite) => favorite.recipeId)));
      } catch (err) {
        if (!active) {
          return;
        }

        setError(getApiErrorMessage(err, 'Unable to load cookbook recipes.'));
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

  const categories = useMemo(() => {
    const values = new Set<string>();
    recipes.forEach((recipe) => values.add(recipe.category));

    return ['all', ...Array.from(values).sort((a, b) => a.localeCompare(b))];
  }, [recipes]);

  const filteredRecipes = useMemo(() => {
    const normalizedSearch = searchQuery.trim().toLowerCase();

    return recipes.filter((recipe) => {
      const matchesCategory = categoryFilter === 'all' || recipe.category === categoryFilter;
      if (!matchesCategory) {
        return false;
      }

      if (favoritesOnly && !favoriteRecipeIds.has(recipe.recipeId)) {
        return false;
      }

      if (!normalizedSearch) {
        return true;
      }

      const searchableFields = [
        recipe.name,
        recipe.description ?? '',
        ...recipe.ingredients.map((ingredient) => ingredient.name),
        ...recipe.tags
      ]
        .join(' ')
        .toLowerCase();

      return searchableFields.includes(normalizedSearch);
    });
  }, [categoryFilter, favoriteRecipeIds, favoritesOnly, recipes, searchQuery]);

  async function toggleFavorite(recipeId: string) {
    try {
      setIsBusy(true);
      setError(null);

      if (favoriteRecipeIds.has(recipeId)) {
        await removeFavoriteRecipe(recipeId);
        setFavoriteRecipeIds((current) => {
          const next = new Set(current);
          next.delete(recipeId);
          return next;
        });
        return;
      }

      await addFavoriteRecipe(recipeId, {});
      setFavoriteRecipeIds((current) => new Set(current).add(recipeId));
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to update favorite.'));
    } finally {
      setIsBusy(false);
    }
  }

  async function deleteRecipeFromCookbook(recipeId: string) {
    const recipe = recipes.find((entry) => entry.recipeId === recipeId);
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
      await deleteRecipe(recipeId);
      setRecipes((current) => current.filter((entry) => entry.recipeId !== recipeId));
      setFavoriteRecipeIds((current) => {
        const next = new Set(current);
        next.delete(recipeId);
        return next;
      });
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to delete recipe.'));
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <Card>
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h2 className="text-2xl font-semibold text-slate-900">Cookbook</h2>
          <p className="mt-3 text-sm text-slate-600">Browse family recipes, search quickly, and save favorites for future planning.</p>
        </div>
        <Link to="/cookbook/new">
          <Button type="button">Add recipe</Button>
        </Link>
      </div>

      {isLoading ? (
        <p className="mt-4 text-sm text-slate-600">Loading recipes...</p>
      ) : (
        <div className="mt-5 space-y-4">
          <div className="grid gap-3 md:grid-cols-[2fr_1fr]">
            <Input
              value={searchQuery}
              onChange={(event) => setSearchQuery(event.target.value)}
              placeholder="Search by name, ingredient, or tag"
              aria-label="Search recipes"
            />
            <select
              value={categoryFilter}
              onChange={(event) => setCategoryFilter(event.target.value)}
              aria-label="Filter by category"
              className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-sky-400 focus:ring-4 focus:ring-sky-100"
            >
              {categories.map((category) => (
                <option key={category} value={category}>
                  {category === 'all' ? 'All categories' : category}
                </option>
              ))}
            </select>
          </div>

          <div className="flex flex-wrap gap-2">
            <Button type="button" variant={favoritesOnly ? 'secondary' : 'ghost'} onClick={() => setFavoritesOnly((current) => !current)}>
              {favoritesOnly ? 'Showing favorites' : 'Favorites only'}
            </Button>
          </div>

          {error ? <p className="text-sm text-red-700">{error}</p> : null}

          {filteredRecipes.length === 0 ? (
            <p className="text-sm text-slate-600">No recipes match your current filters.</p>
          ) : (
            <ul className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {filteredRecipes.map((recipe) => {
                const isFavorite = favoriteRecipeIds.has(recipe.recipeId);
                const totalMinutes = (recipe.prepTimeMinutes ?? 0) + (recipe.cookTimeMinutes ?? 0);

                return (
                  <li key={recipe.recipeId} className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
                    <div className="flex items-start justify-between gap-2">
                      <div>
                        <Link to={`/cookbook/${recipe.recipeId}`} className="text-base font-semibold text-slate-900 underline-offset-4 hover:underline">
                          {recipe.name}
                        </Link>
                        <p className="mt-1 text-xs uppercase tracking-wide text-slate-500">{recipe.category}</p>
                      </div>
                      <Button
                        type="button"
                        variant={isFavorite ? 'secondary' : 'ghost'}
                        onClick={() => void toggleFavorite(recipe.recipeId)}
                        disabled={isBusy}
                      >
                        {isFavorite ? 'Favorited' : 'Favorite'}
                      </Button>
                    </div>

                    {recipe.description ? <p className="mt-3 text-sm text-slate-600">{recipe.description}</p> : null}

                    <div className="mt-3 flex flex-wrap gap-2 text-xs text-slate-500">
                      <span>{recipe.ingredients.length} ingredients</span>
                      <span>{recipe.instructions.length} steps</span>
                      {totalMinutes > 0 ? <span>{totalMinutes} min total</span> : null}
                    </div>

                    {recipe.tags.length > 0 ? (
                      <div className="mt-3 flex flex-wrap gap-2">
                        {recipe.tags.slice(0, 3).map((tag) => (
                          <span key={tag} className="rounded-full bg-slate-100 px-3 py-1 text-xs font-medium text-slate-700">
                            {tag}
                          </span>
                        ))}
                      </div>
                    ) : null}

                    <div className="mt-4 flex items-center justify-between gap-3">
                      <Button
                        type="button"
                        variant="ghost"
                        onClick={() => void deleteRecipeFromCookbook(recipe.recipeId)}
                        disabled={isBusy}
                      >
                        Delete recipe
                      </Button>
                      <Link to={`/cookbook/${recipe.recipeId}`} className="text-sm font-semibold text-sky-700 underline underline-offset-4">
                        View recipe
                      </Link>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}
    </Card>
  );
}