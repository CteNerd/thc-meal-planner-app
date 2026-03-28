import '@testing-library/jest-dom/vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../services/api';
import {
  generateGroceryList,
  getCurrentGroceryList,
  getPantryStaples,
  pollGroceryList,
  replacePantryStaples,
  removeGroceryItem,
  setGroceryItemInStock,
  toggleGroceryItem,
  addGroceryItem,
  addPantryStapleItem,
  deletePantryStapleItem
} from '../services/groceryListApi';
import { GroceryListPage } from './GroceryListPage';
import type { GroceryList } from '../types';

vi.mock('../services/groceryListApi', () => ({
  getCurrentGroceryList: vi.fn(),
  generateGroceryList: vi.fn(),
  toggleGroceryItem: vi.fn(),
  setGroceryItemInStock: vi.fn(),
  addGroceryItem: vi.fn(),
  pollGroceryList: vi.fn(),
  removeGroceryItem: vi.fn(),
  getPantryStaples: vi.fn(),
  replacePantryStaples: vi.fn(),
  addPantryStapleItem: vi.fn(),
  deletePantryStapleItem: vi.fn()
}));

const mockedGetCurrentGroceryList = vi.mocked(getCurrentGroceryList);
const mockedGenerateGroceryList = vi.mocked(generateGroceryList);
const mockedToggleGroceryItem = vi.mocked(toggleGroceryItem);
const mockedSetGroceryItemInStock = vi.mocked(setGroceryItemInStock);
const mockedAddGroceryItem = vi.mocked(addGroceryItem);
const mockedPollGroceryList = vi.mocked(pollGroceryList);
const mockedRemoveGroceryItem = vi.mocked(removeGroceryItem);
const mockedGetPantryStaples = vi.mocked(getPantryStaples);
const mockedReplacePantryStaples = vi.mocked(replacePantryStaples);
const mockedAddPantryStapleItem = vi.mocked(addPantryStapleItem);
const mockedDeletePantryStapleItem = vi.mocked(deletePantryStapleItem);

function buildList(overrides?: Partial<GroceryList>): GroceryList {
  return {
    familyId: 'FAM#test-family',
    listId: 'LIST#ACTIVE',
    version: 3,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    items: [
      {
        id: 'item_1',
        name: 'Tofu',
        section: 'protein',
        quantity: 1,
        unit: 'block',
        mealAssociations: [{ recipeId: 'rec_1', recipeName: 'Stir Fry', mealDay: 'Monday' }],
        checkedOff: false,
        inStock: false
      }
    ],
    progress: {
      total: 1,
      completed: 0,
      percentage: 0
    },
    ...overrides
  };
}

function buildPantry() {
  return {
    familyId: 'FAM#test-family',
    items: [
      { name: 'Salt', section: 'pantry' }
    ],
    preferredSectionOrder: ['produce', 'protein', 'dairy', 'pantry', 'frozen', 'household', 'other'],
    updatedAt: new Date().toISOString()
  };
}

describe('GroceryListPage', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mockedGetCurrentGroceryList.mockResolvedValue(buildList());
    mockedGenerateGroceryList.mockResolvedValue(buildList({ version: 4 }));
    mockedToggleGroceryItem.mockResolvedValue({
      item: {
        ...buildList().items[0],
        checkedOff: true
      },
      version: 4,
      updatedAt: new Date().toISOString(),
      progress: {
        total: 1,
        completed: 1,
        percentage: 100
      }
    });
    mockedSetGroceryItemInStock.mockResolvedValue({
      item: {
        ...buildList().items[0],
        inStock: true
      },
      version: 4,
      updatedAt: new Date().toISOString(),
      progress: {
        total: 1,
        completed: 0,
        percentage: 0
      }
    });
    mockedAddGroceryItem.mockResolvedValue({
      item: {
        id: 'item_2',
        name: 'Milk',
        section: 'dairy',
        quantity: 1,
        unit: 'carton',
        mealAssociations: [],
        checkedOff: false,
        inStock: false
      },
      version: 4,
      updatedAt: new Date().toISOString(),
      progress: {
        total: 2,
        completed: 0,
        percentage: 0
      }
    });
    mockedPollGroceryList.mockResolvedValue(null);
    mockedRemoveGroceryItem.mockResolvedValue();
    mockedGetPantryStaples.mockResolvedValue(buildPantry());
    mockedReplacePantryStaples.mockResolvedValue(buildPantry());
    mockedAddPantryStapleItem.mockResolvedValue(buildPantry());
    mockedDeletePantryStapleItem.mockResolvedValue();
  });

  it('loads and renders existing grocery list', async () => {
    render(<GroceryListPage />);

    expect(await screen.findByText('Grocery List')).toBeInTheDocument();
    expect(screen.getByText('Tofu')).toBeInTheDocument();
    expect(screen.getByText('To Buy')).toBeInTheDocument();
  });

  it('generates grocery list when requested', async () => {
    render(<GroceryListPage />);

    await screen.findByText('Tofu');
    fireEvent.click(screen.getByRole('button', { name: 'Generate From Meal Plan' }));

    await waitFor(() => {
      expect(mockedGenerateGroceryList).toHaveBeenCalledWith({ clearExisting: false });
    });
  });

  it('toggles item checked status', async () => {
    render(<GroceryListPage />);

    await screen.findByText('Tofu');
    fireEvent.click(screen.getByLabelText('toggle checked Tofu'));

    await waitFor(() => {
      expect(mockedToggleGroceryItem).toHaveBeenCalledWith('item_1', { version: 3 });
    });

    expect(screen.getByText('Progress')).toBeInTheDocument();
  });

  it('toggles in-stock status', async () => {
    render(<GroceryListPage />);

    await screen.findByText('Tofu');
    fireEvent.click(screen.getByRole('button', { name: 'Mark In Stock' }));

    await waitFor(() => {
      expect(mockedSetGroceryItemInStock).toHaveBeenCalledWith('item_1', {
        inStock: true,
        version: 3
      });
    });
  });

  it('adds manual grocery item', async () => {
    render(<GroceryListPage />);

    await screen.findByText('Tofu');
    fireEvent.change(screen.getByPlaceholderText('Item name'), { target: { value: 'Milk' } });
    fireEvent.change(screen.getByPlaceholderText('Qty'), { target: { value: '1' } });
    fireEvent.change(screen.getByPlaceholderText('Unit'), { target: { value: 'carton' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));

    await waitFor(() => {
      expect(mockedAddGroceryItem).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'Milk',
          section: 'produce',
          quantity: 1,
          unit: 'carton',
          version: 3
        })
      );
    });
  });

  it('shows empty state when no grocery list exists', async () => {
    mockedGetCurrentGroceryList.mockRejectedValue(new ApiError(404, 'Not Found', { detail: 'Not found' }));
    mockedGetPantryStaples.mockResolvedValue(buildPantry());

    render(<GroceryListPage />);

    expect(await screen.findByText('No active grocery list exists yet. Generate from the active meal plan to start collaborating.')).toBeInTheDocument();
  });

  it('removes an item', async () => {
    render(<GroceryListPage />);

    await screen.findByText('Tofu');
    fireEvent.click(screen.getByRole('button', { name: 'Remove' }));

    await waitFor(() => {
      expect(mockedRemoveGroceryItem).toHaveBeenCalledWith('item_1', 3);
    });
  });

  it('reorders section flow', async () => {
    render(<GroceryListPage />);

    await screen.findByText('Store Section Flow');
    fireEvent.click(screen.getAllByRole('button', { name: 'Down' })[0]);

    await waitFor(() => {
      expect(mockedReplacePantryStaples).toHaveBeenCalled();
    });
  });
});
