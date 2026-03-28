import type {
  CreateMealPlanPayload,
  GenerateMealPlanPayload,
  MealPlan,
  MealSwapSuggestion,
  UpdateMealPlanPayload
} from '../types';
import { apiDelete, apiGet, apiPost, apiPut } from './api';

export async function getCurrentMealPlan(): Promise<MealPlan> {
  return await apiGet<MealPlan>('/meal-plans/current');
}

export async function getMealPlanByWeek(weekStartDate: string): Promise<MealPlan> {
  return await apiGet<MealPlan>(`/meal-plans/${weekStartDate}`);
}

export async function getMealPlanHistory(): Promise<MealPlan[]> {
  return await apiGet<MealPlan[]>('/meal-plans/history');
}

export async function createMealPlan(payload: CreateMealPlanPayload): Promise<MealPlan> {
  return await apiPost<MealPlan, CreateMealPlanPayload>('/meal-plans', payload);
}

export async function generateMealPlan(payload: GenerateMealPlanPayload): Promise<MealPlan> {
  return await apiPost<MealPlan, GenerateMealPlanPayload>('/meal-plans/generate', payload);
}

export async function updateMealPlan(weekStartDate: string, payload: UpdateMealPlanPayload): Promise<MealPlan> {
  return await apiPut<MealPlan, UpdateMealPlanPayload>(`/meal-plans/${weekStartDate}`, payload);
}

export async function deleteMealPlan(weekStartDate: string): Promise<void> {
  await apiDelete(`/meal-plans/${weekStartDate}`);
}

export async function getMealSwapSuggestions(
  weekStartDate: string,
  day: string,
  mealType: string,
  limit = 5
): Promise<MealSwapSuggestion[]> {
  const query = new URLSearchParams({
    day,
    mealType,
    limit: String(limit)
  });

  return await apiGet<MealSwapSuggestion[]>(`/meal-plans/${weekStartDate}/swap-options?${query.toString()}`);
}
