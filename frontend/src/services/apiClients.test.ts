import {
  getCurrentMealPlan,
  getMealPlanByWeek,
  getMealPlanHistory,
  createMealPlan,
  generateMealPlan,
  updateMealPlan,
  deleteMealPlan,
  getMealSwapSuggestions
} from './mealPlanApi';
import {
  getCurrentGroceryList,
  generateGroceryList,
  toggleGroceryItem,
  addGroceryItem,
  setGroceryItemInStock,
  removeGroceryItem,
  pollGroceryList,
  getPantryStaples,
  replacePantryStaples,
  addPantryStapleItem,
  deletePantryStapleItem
} from './groceryListApi';
import { sendChatMessage, getChatHistory } from './chatApi';
import {
  listRecipes,
  getRecipe,
  createRecipe,
  updateRecipe,
  deleteRecipe,
  importRecipeFromUrl,
  createRecipeUploadUrl,
  listFavoriteRecipes,
  addFavoriteRecipe,
  removeFavoriteRecipe
} from './recipeApi';
import {
  getProfile,
  updateProfile,
  listDependents,
  createDependent,
  updateDependent,
  deleteDependent
} from './profileApi';
import { apiDelete, apiFetch, apiGet, apiPost, apiPut } from './api';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('./api', () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiDelete: vi.fn(),
  apiFetch: vi.fn(),
  ApiError: class ApiError extends Error {
    status: number;
    payload: unknown;

    constructor(status: number, message: string, payload: unknown) {
      super(message);
      this.status = status;
      this.payload = payload;
    }
  }
}));

const mockedApiGet = vi.mocked(apiGet);
const mockedApiPost = vi.mocked(apiPost);
const mockedApiPut = vi.mocked(apiPut);
const mockedApiDelete = vi.mocked(apiDelete);
const mockedApiFetch = vi.mocked(apiFetch);

describe('service API clients', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('routes meal plan requests to expected endpoints', async () => {
    mockedApiGet.mockResolvedValue({});
    mockedApiPost.mockResolvedValue({});
    mockedApiPut.mockResolvedValue({});

    await getCurrentMealPlan();
    await getMealPlanByWeek('2026-03-30');
    await getMealPlanHistory();
    await createMealPlan({ weekStartDate: '2026-03-30', meals: [] });
    await generateMealPlan({ weekStartDate: '2026-03-30' });
    await updateMealPlan('2026-03-30', { status: 'draft' });
    await deleteMealPlan('2026-03-30');
    await getMealSwapSuggestions('2026-03-30', 'Monday', 'Dinner', 3);

    expect(mockedApiGet).toHaveBeenCalledWith('/meal-plans/current');
    expect(mockedApiGet).toHaveBeenCalledWith('/meal-plans/2026-03-30');
    expect(mockedApiGet).toHaveBeenCalledWith('/meal-plans/history');
    expect(mockedApiPost).toHaveBeenCalledWith('/meal-plans', { weekStartDate: '2026-03-30', meals: [] });
    expect(mockedApiPost).toHaveBeenCalledWith('/meal-plans/generate', { weekStartDate: '2026-03-30' });
    expect(mockedApiPut).toHaveBeenCalledWith('/meal-plans/2026-03-30', { status: 'draft' });
    expect(mockedApiDelete).toHaveBeenCalledWith('/meal-plans/2026-03-30');
    expect(mockedApiGet).toHaveBeenCalledWith('/meal-plans/2026-03-30/swap-options?day=Monday&mealType=Dinner&limit=3');
  });

  it('routes grocery list requests and handles delete/poll failures', async () => {
    mockedApiGet.mockResolvedValue({});
    mockedApiPost.mockResolvedValue({});
    mockedApiPut.mockResolvedValue({});
    mockedApiFetch.mockResolvedValue({ ok: true, status: 200, json: vi.fn().mockResolvedValue({ hasChanges: true }) } as never);

    await getCurrentGroceryList();
    await generateGroceryList({ clearExisting: false });
    await toggleGroceryItem('item_1', { version: 4 });
    await addGroceryItem({ name: 'Milk', section: 'dairy', version: 4 });
    await setGroceryItemInStock('item_1', { inStock: true, version: 4 });
    await removeGroceryItem('item_1', 4);
    await pollGroceryList('2026-03-29T00:00:00Z');
    await getPantryStaples();
    await replacePantryStaples({ items: [] });
    await addPantryStapleItem({ name: 'Salt' });
    await deletePantryStapleItem('Salt');

    expect(mockedApiGet).toHaveBeenCalledWith('/grocery-lists/current');
    expect(mockedApiPost).toHaveBeenCalledWith('/grocery-lists/generate', { clearExisting: false });
    expect(mockedApiPut).toHaveBeenCalledWith('/grocery-lists/items/item_1/toggle', { version: 4 });
    expect(mockedApiPut).toHaveBeenCalledWith('/grocery-lists/items/item_1/in-stock', { inStock: true, version: 4 });
    expect(mockedApiFetch).toHaveBeenCalledWith('/grocery-lists/items/item_1?version=4', { method: 'DELETE' });
    expect(mockedApiFetch).toHaveBeenCalledWith('/grocery-lists/poll?since=2026-03-29T00%3A00%3A00Z');

    mockedApiFetch.mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: vi.fn().mockResolvedValue({ detail: 'Version conflict' })
    } as never);

    await expect(removeGroceryItem('item_2', 1)).rejects.toMatchObject({ status: 409 });
  });

  it('routes chat and recipe requests to expected endpoints', async () => {
    mockedApiGet.mockResolvedValue({});
    mockedApiPost.mockResolvedValue({});
    mockedApiPut.mockResolvedValue({});

    await sendChatMessage({ message: 'Hello' });
    await getChatHistory('conv_1', 10);

    await listRecipes();
    await getRecipe('recipe_1');
    await createRecipe({
      name: 'Recipe',
      category: 'Dinner',
      tags: [],
      ingredients: [],
      instructions: []
    });
    await updateRecipe('recipe_1', { description: 'updated' });
    await deleteRecipe('recipe_1');
    await importRecipeFromUrl({ url: 'https://example.com' });
    await createRecipeUploadUrl('recipe_1', { fileName: 'image.jpg', contentType: 'image/jpeg' });
    await listFavoriteRecipes('Dinner');
    await addFavoriteRecipe('recipe_1', { notes: 'Great' });
    await removeFavoriteRecipe('recipe_1');

    expect(mockedApiPost).toHaveBeenCalledWith('/chat/message', { message: 'Hello' });
    expect(mockedApiGet).toHaveBeenCalledWith('/chat/history?conversationId=conv_1&limit=10');
    expect(mockedApiGet).toHaveBeenCalledWith('/recipes');
    expect(mockedApiGet).toHaveBeenCalledWith('/recipes/recipe_1');
    expect(mockedApiDelete).toHaveBeenCalledWith('/recipes/recipe_1');
    expect(mockedApiPost).toHaveBeenCalledWith('/recipes/import-from-url', { url: 'https://example.com' });
    expect(mockedApiPost).toHaveBeenCalledWith('/recipes/recipe_1/upload-url', {
      fileName: 'image.jpg',
      contentType: 'image/jpeg'
    });
    expect(mockedApiGet).toHaveBeenCalledWith('/recipes/favorites?category=Dinner');
    expect(mockedApiDelete).toHaveBeenCalledWith('/recipes/recipe_1/favorite');
  });

  it('routes profile and dependent requests to expected endpoints', async () => {
    mockedApiGet.mockResolvedValue({});
    mockedApiPost.mockResolvedValue({});
    mockedApiPut.mockResolvedValue({});

    await getProfile();
    await updateProfile({ name: 'Updated' });
    await listDependents();
    await createDependent({ name: 'Kid' });
    await updateDependent('dep_1', { ageGroup: '8-12' });
    await deleteDependent('dep_1');

    expect(mockedApiGet).toHaveBeenCalledWith('/profile');
    expect(mockedApiPut).toHaveBeenCalledWith('/profile', { name: 'Updated' });
    expect(mockedApiGet).toHaveBeenCalledWith('/family/dependents');
    expect(mockedApiPost).toHaveBeenCalledWith('/family/dependents', { name: 'Kid' });
    expect(mockedApiPut).toHaveBeenCalledWith('/family/dependents/dep_1', { ageGroup: '8-12' });
    expect(mockedApiDelete).toHaveBeenCalledWith('/family/dependents/dep_1');
  });
});
