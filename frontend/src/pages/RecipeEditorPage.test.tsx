import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { RecipeEditorPage } from './RecipeEditorPage';
import type { ImportedRecipeDraft, Recipe } from '../types';
import {
  createRecipe,
  createRecipeUploadUrl,
  getRecipe,
  importRecipeFromUrl,
  updateRecipe,
  uploadRecipeImage
} from '../services/recipeApi';

vi.mock('../services/recipeApi', () => ({
  createRecipe: vi.fn(),
  createRecipeUploadUrl: vi.fn(),
  getRecipe: vi.fn(),
  importRecipeFromUrl: vi.fn(),
  updateRecipe: vi.fn(),
  uploadRecipeImage: vi.fn()
}));

const mockedCreateRecipe = vi.mocked(createRecipe);
const mockedCreateRecipeUploadUrl = vi.mocked(createRecipeUploadUrl);
const mockedGetRecipe = vi.mocked(getRecipe);
const mockedImportRecipeFromUrl = vi.mocked(importRecipeFromUrl);
const mockedUpdateRecipe = vi.mocked(updateRecipe);
const mockedUploadRecipeImage = vi.mocked(uploadRecipeImage);

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

function buildDraft(overrides?: Partial<ImportedRecipeDraft>): ImportedRecipeDraft {
  return {
    name: 'Imported Pasta',
    category: 'dinner',
    tags: ['comfort'],
    ingredients: [{ name: 'Pasta' }],
    instructions: ['Boil water'],
    sourceType: 'url',
    sourceUrl: 'https://example.com/pasta',
    warnings: ['Review imported content before saving.'],
    ...overrides
  };
}

describe('RecipeEditorPage', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mockedCreateRecipe.mockResolvedValue(buildRecipe());
    mockedUpdateRecipe.mockResolvedValue(buildRecipe());
    mockedCreateRecipeUploadUrl.mockResolvedValue({
      uploadUrl: 'https://example.com/upload',
      imageKey: 'recipes/rec_1/main.jpg',
      imageUrl: '/images/recipes/rec_1/main.jpg'
    });
    mockedUploadRecipeImage.mockResolvedValue();
  });

  it('imports a draft from URL into the form', async () => {
    mockedImportRecipeFromUrl.mockResolvedValue(buildDraft());

    render(
      <MemoryRouter initialEntries={['/cookbook/new']}>
        <Routes>
          <Route path="/cookbook/new" element={<RecipeEditorPage />} />
        </Routes>
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText('Recipe import URL'), { target: { value: 'https://example.com/pasta' } });
    fireEvent.click(screen.getByRole('button', { name: 'Import draft' }));

    await waitFor(() => {
      expect(screen.getByLabelText('Recipe name')).toHaveValue('Imported Pasta');
    });

    expect(screen.getByText('Review imported content before saving.')).toBeInTheDocument();
  });

  it('creates a recipe from form values', async () => {
    render(
      <MemoryRouter initialEntries={['/cookbook/new']}>
        <Routes>
          <Route path="/cookbook/new" element={<RecipeEditorPage />} />
        </Routes>
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText('Recipe name'), { target: { value: 'New Recipe' } });
    fireEvent.change(screen.getByLabelText('Recipe ingredients'), { target: { value: '1|cup|Rice' } });
    fireEvent.change(screen.getByLabelText('Recipe instructions'), { target: { value: 'Cook the rice' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create recipe' }));

    await waitFor(() => {
      expect(mockedCreateRecipe).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'New Recipe',
          ingredients: [{ quantity: '1', unit: 'cup', name: 'Rice', section: undefined, notes: undefined }],
          instructions: ['Cook the rice']
        })
      );
    });
  });

  it('loads existing recipe when editing', async () => {
    mockedGetRecipe.mockResolvedValue(buildRecipe({ recipeId: 'rec_edit', name: 'Editable Recipe' }));

    render(
      <MemoryRouter initialEntries={['/cookbook/rec_edit/edit']}>
        <Routes>
          <Route path="/cookbook/:recipeId/edit" element={<RecipeEditorPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(await screen.findByDisplayValue('Editable Recipe')).toBeInTheDocument();
  });
}