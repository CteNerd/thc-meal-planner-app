import type { FavoriteRecipe, FavoriteRecipePayload, Recipe } from '../types';
import { apiDelete, apiGet, apiPost } from './api';

export async function listRecipes(): Promise<Recipe[]> {
  return await apiGet<Recipe[]>('/recipes');
}

export async function listFavoriteRecipes(category?: string): Promise<FavoriteRecipe[]> {
  const query = category ? `?category=${encodeURIComponent(category)}` : '';
  return await apiGet<FavoriteRecipe[]>(`/recipes/favorites${query}`);
}

export async function addFavoriteRecipe(
  recipeId: string,
  payload: FavoriteRecipePayload = {}
): Promise<FavoriteRecipe> {
  return await apiPost<FavoriteRecipe, FavoriteRecipePayload>(`/recipes/${recipeId}/favorite`, payload);
}

export async function removeFavoriteRecipe(recipeId: string): Promise<void> {
  await apiDelete(`/recipes/${recipeId}/favorite`);
}
