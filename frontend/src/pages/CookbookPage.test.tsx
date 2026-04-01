import '@testing-library/jest-dom/vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { CookbookPage } from './CookbookPage';
import type { FavoriteRecipe, Recipe } from '../types';
import { ApiError } from '../services/api';
import { addFavoriteRecipe, deleteRecipe, listFavoriteRecipes, listRecipes, removeFavoriteRecipe } from '../services/recipeApi';

vi.mock('../services/recipeApi', () => ({
  listRecipes: vi.fn(),
  listFavoriteRecipes: vi.fn(),
  addFavoriteRecipe: vi.fn(),
  deleteRecipe: vi.fn(),
  removeFavoriteRecipe: vi.fn()
}));

const mockedListRecipes = vi.mocked(listRecipes);
const mockedListFavoriteRecipes = vi.mocked(listFavoriteRecipes);
const mockedAddFavoriteRecipe = vi.mocked(addFavoriteRecipe);
const mockedDeleteRecipe = vi.mocked(deleteRecipe);
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

function renderCookbookPage() {
  render(
    <MemoryRouter>
      <CookbookPage />
    </MemoryRouter>
  );
}

describe('CookbookPage', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mockedAddFavoriteRecipe.mockResolvedValue(buildFavorite());
    mockedDeleteRecipe.mockResolvedValue();
    mockedRemoveFavoriteRecipe.mockResolvedValue();
  });

  it('loads and renders cookbook recipes', async () => {
    mockedListRecipes.mockResolvedValue([buildRecipe(), buildRecipe({ recipeId: 'rec_2', name: 'Taco Bowls' })]);
    mockedListFavoriteRecipes.mockResolvedValue([buildFavorite()]);

    renderCookbookPage();

    expect(await screen.findByText('Veggie Stir Fry')).toBeInTheDocument();
    expect(screen.getByText('Taco Bowls')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Favorited' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Favorite' })).toBeInTheDocument();
  });

  it('filters recipes by search and category', async () => {
    mockedListRecipes.mockResolvedValue([
      buildRecipe({ recipeId: 'rec_1', name: 'Veggie Stir Fry', category: 'dinner' }),
      buildRecipe({ recipeId: 'rec_2', name: 'Berry Parfait', category: 'breakfast', ingredients: [{ name: 'Yogurt' }] })
    ]);
    mockedListFavoriteRecipes.mockResolvedValue([]);

    renderCookbookPage();

    await screen.findByText('Veggie Stir Fry');

    fireEvent.change(screen.getByLabelText('Search recipes'), { target: { value: 'berry' } });

    expect(screen.queryByText('Veggie Stir Fry')).not.toBeInTheDocument();
    expect(screen.getByText('Berry Parfait')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Filter by category'), { target: { value: 'dinner' } });

    expect(screen.getByText('No recipes match your current filters.')).toBeInTheDocument();
  });

  it('adds and removes favorites', async () => {
    mockedListRecipes.mockResolvedValue([buildRecipe()]);
    mockedListFavoriteRecipes.mockResolvedValue([]);

    renderCookbookPage();

    await screen.findByText('Veggie Stir Fry');
    fireEvent.click(screen.getByRole('button', { name: 'Favorite' }));

    await waitFor(() => {
      expect(mockedAddFavoriteRecipe).toHaveBeenCalledWith('rec_1', {});
    });

    expect(screen.getByRole('button', { name: 'Favorited' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Favorited' }));

    await waitFor(() => {
      expect(mockedRemoveFavoriteRecipe).toHaveBeenCalledWith('rec_1');
    });

    expect(screen.getByRole('button', { name: 'Favorite' })).toBeInTheDocument();
  });

  it('deletes a recipe from the cookbook after confirmation', async () => {
    mockedListRecipes.mockResolvedValue([
      buildRecipe({ recipeId: 'rec_1', name: 'Veggie Stir Fry' }),
      buildRecipe({ recipeId: 'rec_2', name: 'Taco Bowls' })
    ]);
    mockedListFavoriteRecipes.mockResolvedValue([]);
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    renderCookbookPage();

    await screen.findByText('Veggie Stir Fry');
    fireEvent.click(screen.getAllByRole('button', { name: 'Delete recipe' })[0]);

    await waitFor(() => {
      expect(mockedDeleteRecipe).toHaveBeenCalledWith('rec_1');
    });

    expect(screen.queryByText('Veggie Stir Fry')).not.toBeInTheDocument();
    expect(screen.getByText('Taco Bowls')).toBeInTheDocument();
  });

  it('does not delete a recipe when cookbook confirmation is cancelled', async () => {
    mockedListRecipes.mockResolvedValue([buildRecipe()]);
    mockedListFavoriteRecipes.mockResolvedValue([]);
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    renderCookbookPage();

    await screen.findByText('Veggie Stir Fry');
    fireEvent.click(screen.getByRole('button', { name: 'Delete recipe' }));

    expect(mockedDeleteRecipe).not.toHaveBeenCalled();
    expect(screen.getByText('Veggie Stir Fry')).toBeInTheDocument();
  });

  it('shows api detail when load fails', async () => {
    mockedListRecipes.mockRejectedValue(
      new ApiError(403, 'Forbidden', {
        title: 'Forbidden',
        detail: 'Recipe access denied.'
      })
    );
    mockedListFavoriteRecipes.mockResolvedValue([]);

    renderCookbookPage();

    expect(await screen.findByText('Recipe access denied.')).toBeInTheDocument();
  });
});
