import '@testing-library/jest-dom/vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { MealPlansPage } from './MealPlansPage';
import { ApiError } from '../services/api';
import {
  generateMealPlan,
  getCurrentMealPlan,
  getMealPlanHistory,
  getMealSwapSuggestions,
  updateMealPlan
} from '../services/mealPlanApi';
import { listRecipes } from '../services/recipeApi';
import type { MealPlan, Recipe } from '../types';

vi.mock('../services/mealPlanApi', () => ({
  getCurrentMealPlan: vi.fn(),
  getMealPlanHistory: vi.fn(),
  generateMealPlan: vi.fn(),
  updateMealPlan: vi.fn(),
  getMealSwapSuggestions: vi.fn()
}));

vi.mock('../services/recipeApi', () => ({
  listRecipes: vi.fn()
}));

const mockedGetCurrentMealPlan = vi.mocked(getCurrentMealPlan);
const mockedGetMealPlanHistory = vi.mocked(getMealPlanHistory);
const mockedGenerateMealPlan = vi.mocked(generateMealPlan);
const mockedUpdateMealPlan = vi.mocked(updateMealPlan);
const mockedGetMealSwapSuggestions = vi.mocked(getMealSwapSuggestions);
const mockedListRecipes = vi.mocked(listRecipes);

function buildRecipe(overrides?: Partial<Recipe>): Recipe {
  return {
    recipeId: 'rec_1',
    familyId: 'FAM#test-family',
    name: 'Dinner A',
    category: 'dinner',
    tags: [],
    ingredients: [{ name: 'Ingredient' }],
    instructions: ['Cook'],
    sourceType: 'manual',
    createdByUserId: 'test-user-123',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    ...overrides
  };
}

function buildMealPlan(overrides?: Partial<MealPlan>): MealPlan {
  return {
    familyId: 'FAM#test-family',
    weekStartDate: '2026-03-30',
    status: 'active',
    meals: [
      {
        day: 'Monday',
        mealType: 'dinner',
        recipeId: 'rec_1',
        recipeName: 'Dinner A',
        prepTime: 10,
        cookTime: 15,
        nutritionalInfo: { calories: 450 }
      }
    ],
    nutritionalSummary: {
      dailyAverages: {
        calories: 1800,
        protein: 90,
        carbohydrates: 220,
        fat: 70
      }
    },
    constraintsUsed: 'v1',
    generatedBy: 'ai',
    qualityScore: {
      overall: 82,
      varietyScore: 30,
      diversityScore: 30,
      constraintViolations: 0,
      grade: 'B+'
    },
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    ...overrides
  };
}

describe('MealPlansPage', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mockedGetCurrentMealPlan.mockResolvedValue(buildMealPlan());
    mockedGetMealPlanHistory.mockResolvedValue([buildMealPlan()]);
    mockedListRecipes.mockResolvedValue([
      buildRecipe(),
      buildRecipe({ recipeId: 'rec_2', name: 'Dinner B' }),
      buildRecipe({ recipeId: 'rec_b', name: 'Breakfast A', category: 'breakfast' }),
      buildRecipe({ recipeId: 'rec_l', name: 'Lunch A', category: 'lunch' })
    ]);
    mockedGenerateMealPlan.mockResolvedValue(buildMealPlan({ weekStartDate: '2026-04-06' }));
    mockedUpdateMealPlan.mockResolvedValue(buildMealPlan({ meals: [{ day: 'Monday', mealType: 'dinner', recipeId: 'rec_2', recipeName: 'Dinner B' }] }));
    mockedGetMealSwapSuggestions.mockResolvedValue([
      {
        recipeId: 'rec_2',
        recipeName: 'Dinner B',
        prepTime: 12,
        cookTime: 18,
        constraintSafe: true,
        score: 86,
        notes: []
      }
    ]);
  });

  it('loads and renders current plan details', async () => {
    render(<MealPlansPage />);

    expect(await screen.findByText('Meal Plans')).toBeInTheDocument();
    expect(screen.getByText('2026-03-30')).toBeInTheDocument();
    expect(screen.getByText('B+ (82/100)')).toBeInTheDocument();
    expect(screen.getAllByText('Dinner A').length).toBeGreaterThan(0);
  });

  it('generates this week plan', async () => {
    render(<MealPlansPage />);

    await screen.findByText('2026-03-30');
    fireEvent.click(screen.getByRole('button', { name: 'Generate This Week' }));

    await waitFor(() => {
      expect(mockedGenerateMealPlan).toHaveBeenCalledWith(
        expect.objectContaining({
          replaceExisting: true,
          weekStartDate: expect.any(String)
        })
      );
    });

    expect(screen.getByText('2026-04-06')).toBeInTheDocument();
  });

  it('loads swap suggestions and swaps a slot', async () => {
    render(<MealPlansPage />);

    await screen.findByText('2026-03-30');
    fireEvent.click(screen.getByLabelText('Monday dinner suggest'));

    await waitFor(() => {
      expect(mockedGetMealSwapSuggestions).toHaveBeenCalled();
    });

    fireEvent.change(screen.getByLabelText('Monday dinner swap'), { target: { value: 'rec_2' } });

    await waitFor(() => {
      expect(mockedUpdateMealPlan).toHaveBeenCalledWith('2026-03-30', {
        meals: [{ day: 'Monday', mealType: 'dinner', recipeId: 'rec_2' }]
      });
    });

    expect(screen.getAllByText('Dinner B').length).toBeGreaterThan(0);
  });

  it('shows history tab entries', async () => {
    render(<MealPlansPage />);

    await screen.findByText('2026-03-30');
    fireEvent.click(screen.getByRole('button', { name: 'History' }));

    expect(await screen.findByText('Week of 2026-03-30')).toBeInTheDocument();
  });

  it('handles missing current plan as empty state', async () => {
    mockedGetCurrentMealPlan.mockRejectedValue(new ApiError(404, 'Not Found', { detail: 'No active meal plan' }));
    mockedGetMealPlanHistory.mockResolvedValue([]);
    mockedListRecipes.mockResolvedValue([]);

    render(<MealPlansPage />);

    expect(await screen.findByText('No active weekly plan yet. Generate one for this week to get started.')).toBeInTheDocument();
  });
});
