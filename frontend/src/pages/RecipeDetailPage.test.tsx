import '@testing-library/jest-dom/vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { RecipeDetailPage } from './RecipeDetailPage';
import type { FavoriteRecipe, Recipe } from '../types';
import { addFavoriteRecipe, deleteRecipe, getRecipe, listFavoriteRecipes, removeFavoriteRecipe } from '../services/recipeApi';

const mockNavigate = vi.fn();

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate
  };
});

vi.mock('../services/recipeApi', () => ({
  addFavoriteRecipe: vi.fn(),
  deleteRecipe: vi.fn(),
  getRecipe: vi.fn(),
  listFavoriteRecipes: vi.fn(),
  removeFavoriteRecipe: vi.fn()
}));

const mockedAddFavoriteRecipe = vi.mocked(addFavoriteRecipe);
const mockedDeleteRecipe = vi.mocked(deleteRecipe);
const mockedGetRecipe = vi.mocked(getRecipe);
const mockedListFavoriteRecipes = vi.mocked(listFavoriteRecipes);
const mockedRemoveFavoriteRecipe = vi.mocked(removeFavoriteRecipe);

function buildRecipe(overrides?: Partial<Recipe>): Recipe {
  return {
    recipeId: 'rec_1',
    familyId: 'FAM#test-family',
    name: 'Veggie Stir Fry',
    category: 'dinner',
    tags: ['quick'],
    ingredients: [{ name: 'Broccoli' }],
    instructions: ['Stir fry all ingredients.'],
    sourceType: 'manual',
    createdByUserId: 'test-user-123',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    ...overrides
  };
}

function buildFavorite(overrides?: Partial<FavoriteRecipe>): FavoriteRecipe {
  return {
    userId: 'test-user-123',
    recipeId: 'rec_1',
    recipeName: 'Veggie Stir Fry',
    recipeCategory: 'dinner',
    addedAt: new Date().toISOString(),
    ...overrides
  };
}

function renderRecipeDetailPage() {
  render(
    <MemoryRouter initialEntries={['/cookbook/rec_1']}>
      <Routes>
        <Route path="/cookbook/:recipeId" element={<RecipeDetailPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('RecipeDetailPage', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mockNavigate.mockReset();
  });

  it('loads and renders recipe details', async () => {
    mockedGetRecipe.mockResolvedValue(buildRecipe({ description: 'Fast and easy.' }));
    mockedListFavoriteRecipes.mockResolvedValue([buildFavorite()]);

    renderRecipeDetailPage();

    expect(await screen.findByText('Veggie Stir Fry')).toBeInTheDocument();
    expect(screen.getByText('Fast and easy.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Favorited' })).toBeInTheDocument();
  });

  it('toggles favorite state', async () => {
    mockedGetRecipe.mockResolvedValue(buildRecipe());
    mockedListFavoriteRecipes.mockResolvedValue([]);
    mockedAddFavoriteRecipe.mockResolvedValue(buildFavorite());
    mockedRemoveFavoriteRecipe.mockResolvedValue();

    renderRecipeDetailPage();

    await screen.findByText('Veggie Stir Fry');
    fireEvent.click(screen.getByRole('button', { name: 'Add to favorites' }));

    await waitFor(() => {
      expect(mockedAddFavoriteRecipe).toHaveBeenCalledWith('rec_1', {});
    });

    fireEvent.click(screen.getByRole('button', { name: 'Favorited' }));

    await waitFor(() => {
      expect(mockedRemoveFavoriteRecipe).toHaveBeenCalledWith('rec_1');
    });
  });

  it('deletes a recipe after confirmation', async () => {
    mockedGetRecipe.mockResolvedValue(buildRecipe());
    mockedListFavoriteRecipes.mockResolvedValue([]);
    mockedDeleteRecipe.mockResolvedValue();
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    renderRecipeDetailPage();

    await screen.findByText('Veggie Stir Fry');
    fireEvent.click(screen.getByRole('button', { name: 'Delete recipe' }));

    await waitFor(() => {
      expect(mockedDeleteRecipe).toHaveBeenCalledWith('rec_1');
      expect(mockNavigate).toHaveBeenCalledWith('/cookbook');
    });
  });

  it('does not delete when confirmation is cancelled', async () => {
    mockedGetRecipe.mockResolvedValue(buildRecipe());
    mockedListFavoriteRecipes.mockResolvedValue([]);
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    renderRecipeDetailPage();

    await screen.findByText('Veggie Stir Fry');
    fireEvent.click(screen.getByRole('button', { name: 'Delete recipe' }));

    expect(mockedDeleteRecipe).not.toHaveBeenCalled();
    expect(mockNavigate).not.toHaveBeenCalled();
  });
});
