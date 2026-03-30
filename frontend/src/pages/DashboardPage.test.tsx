import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DashboardPage } from './DashboardPage';
import { getCurrentMealPlan } from '../services/mealPlanApi';
import { getCurrentGroceryList } from '../services/groceryListApi';
import { listRecipes } from '../services/recipeApi';
import { getChatHistory } from '../services/chatApi';

vi.mock('../services/mealPlanApi', () => ({
  getCurrentMealPlan: vi.fn()
}));

vi.mock('../services/groceryListApi', () => ({
  getCurrentGroceryList: vi.fn()
}));

vi.mock('../services/recipeApi', () => ({
  listRecipes: vi.fn()
}));

vi.mock('../services/chatApi', () => ({
  getChatHistory: vi.fn()
}));

const mockedGetCurrentMealPlan = vi.mocked(getCurrentMealPlan);
const mockedGetCurrentGroceryList = vi.mocked(getCurrentGroceryList);
const mockedListRecipes = vi.mocked(listRecipes);
const mockedGetChatHistory = vi.mocked(getChatHistory);

describe('DashboardPage', () => {
  function renderDashboard() {
    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    );
  }

  beforeEach(() => {
    mockedGetCurrentMealPlan.mockResolvedValue({
      familyId: 'family_1',
      weekStartDate: '2026-03-30',
      status: 'active',
      meals: [
        {
          day: 'Monday',
          mealType: 'Dinner',
          recipeId: 'recipe_1',
          recipeName: 'Sheet Pan Salmon'
        }
      ],
      constraintsUsed: 'default',
      generatedBy: 'user_1',
      createdAt: '2026-03-28T10:00:00Z',
      updatedAt: '2026-03-28T10:00:00Z'
    });

    mockedGetCurrentGroceryList.mockResolvedValue({
      familyId: 'family_1',
      listId: 'list_1',
      items: [],
      version: 1,
      createdAt: '2026-03-28T10:00:00Z',
      updatedAt: '2026-03-28T10:00:00Z',
      progress: {
        total: 10,
        completed: 4,
        percentage: 40
      }
    });

    mockedListRecipes.mockResolvedValue([
      {
        recipeId: 'recipe_1',
        familyId: 'family_1',
        name: 'Sheet Pan Salmon',
        category: 'Dinner',
        tags: [],
        ingredients: [],
        instructions: [],
        sourceType: 'manual',
        createdByUserId: 'user_1',
        createdAt: '2026-03-28T10:00:00Z',
        updatedAt: '2026-03-28T10:00:00Z'
      }
    ]);

    mockedGetChatHistory.mockResolvedValue({
      conversationId: 'conv_1',
      messages: [
        {
          role: 'assistant',
          content: 'Your plan is ready.',
          timestamp: '2026-03-28T10:00:00Z'
        }
      ]
    });
  });

  afterEach(() => {
    vi.resetAllMocks();
  });

  it('renders loaded dashboard summary cards', async () => {
    renderDashboard();

    expect(await screen.findByText('This week at a glance')).toBeTruthy();
    expect(await screen.findByText('1 planned meal slots in week 2026-03-30.')).toBeTruthy();
    expect(await screen.findByText('4/10 items completed (40%).')).toBeTruthy();
    expect(await screen.findByText('1 recipes available for planning and swaps.')).toBeTruthy();
    expect(await screen.findByText('Your plan is ready.')).toBeTruthy();
  });

  it('shows fallback state when endpoints fail', async () => {
    mockedGetCurrentMealPlan.mockRejectedValue(new Error('no meal plan'));
    mockedGetCurrentGroceryList.mockRejectedValue(new Error('no list'));
    mockedListRecipes.mockRejectedValue(new Error('no recipes'));
    mockedGetChatHistory.mockRejectedValue(new Error('no chat'));

    renderDashboard();

    expect(await screen.findByText('no meal plan')).toBeTruthy();
    expect(await screen.findByText('No active meal plan yet. Generate a new plan to start the week.')).toBeTruthy();
    expect(await screen.findByText('No active grocery list yet. Generate one from your meal plan.')).toBeTruthy();
  });
});
