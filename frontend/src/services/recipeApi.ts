import type {
  CreateRecipeUploadUrlPayload,
  FavoriteRecipe,
  FavoriteRecipePayload,
  ImportedRecipeDraft,
  ImportRecipeFromImagePayload,
  ImportRecipeFromUrlPayload,
  Recipe,
  RecipePayload,
  RecipeUploadUrlResponse,
  UpdateRecipePayload
} from '../types';
import { apiDelete, apiGet, apiPost, apiPut } from './api';

export async function listRecipes(): Promise<Recipe[]> {
  return await apiGet<Recipe[]>('/recipes');
}

export async function getRecipe(recipeId: string): Promise<Recipe> {
  return await apiGet<Recipe>(`/recipes/${recipeId}`);
}

export async function createRecipe(payload: RecipePayload): Promise<Recipe> {
  return await apiPost<Recipe, RecipePayload>('/recipes', payload);
}

export async function updateRecipe(recipeId: string, payload: UpdateRecipePayload): Promise<Recipe> {
  return await apiPut<Recipe, UpdateRecipePayload>(`/recipes/${recipeId}`, payload);
}

export async function deleteRecipe(recipeId: string): Promise<void> {
  await apiDelete(`/recipes/${recipeId}`);
}

export async function importRecipeFromUrl(payload: ImportRecipeFromUrlPayload): Promise<ImportedRecipeDraft> {
  return await apiPost<ImportedRecipeDraft, ImportRecipeFromUrlPayload>('/recipes/import-from-url', payload);
}

export async function importRecipeFromImage(
  recipeId: string,
  payload: ImportRecipeFromImagePayload = {}
): Promise<ImportedRecipeDraft> {
  return await apiPost<ImportedRecipeDraft, ImportRecipeFromImagePayload>(`/recipes/${recipeId}/import-from-image`, payload);
}

export async function createRecipeUploadUrl(
  recipeId: string,
  payload: CreateRecipeUploadUrlPayload
): Promise<RecipeUploadUrlResponse> {
  return await apiPost<RecipeUploadUrlResponse, CreateRecipeUploadUrlPayload>(`/recipes/${recipeId}/upload-url`, payload);
}

export async function uploadRecipeImage(uploadUrl: string, file: File): Promise<void> {
  const response = await fetch(uploadUrl, {
    method: 'PUT',
    headers: {
      'Content-Type': file.type
    },
    body: file
  });

  if (!response.ok) {
    let responseBody = '';
    try {
      responseBody = (await response.text()).trim();
    } catch {
      responseBody = '';
    }

    const bodyHint = responseBody.length > 0
      ? ` Response: ${responseBody.slice(0, 180)}`
      : '';

    throw new Error(
      `Image upload failed (${response.status} ${response.statusText}). ` +
      'Please retry with a JPG/PNG/WEBP image under 10MB.' +
      bodyHint
    );
  }
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
