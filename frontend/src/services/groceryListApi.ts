import type {
  AddGroceryItemPayload,
  AddPantryStapleItemPayload,
  GenerateGroceryListPayload,
  GroceryItemMutationResponse,
  GroceryList,
  GroceryListPollResponse,
  PantryStaples,
  ReplacePantryStaplesPayload,
  SetInStockPayload,
  ToggleGroceryItemPayload
} from '../types';
import { ApiError, apiFetch, apiGet, apiPost, apiPut } from './api';

export async function getCurrentGroceryList(): Promise<GroceryList> {
  return await apiGet<GroceryList>('/grocery-lists/current');
}

export async function generateGroceryList(payload: GenerateGroceryListPayload): Promise<GroceryList> {
  return await apiPost<GroceryList, GenerateGroceryListPayload>('/grocery-lists/generate', payload);
}

export async function toggleGroceryItem(itemId: string, payload: ToggleGroceryItemPayload): Promise<GroceryItemMutationResponse> {
  return await apiPut<GroceryItemMutationResponse, ToggleGroceryItemPayload>(`/grocery-lists/items/${itemId}/toggle`, payload);
}

export async function addGroceryItem(payload: AddGroceryItemPayload): Promise<GroceryItemMutationResponse> {
  return await apiPost<GroceryItemMutationResponse, AddGroceryItemPayload>('/grocery-lists/items', payload);
}

export async function setGroceryItemInStock(itemId: string, payload: SetInStockPayload): Promise<GroceryItemMutationResponse> {
  return await apiPut<GroceryItemMutationResponse, SetInStockPayload>(`/grocery-lists/items/${itemId}/in-stock`, payload);
}

export async function removeGroceryItem(itemId: string, version: number): Promise<void> {
  const response = await apiFetch(`/grocery-lists/items/${itemId}?version=${encodeURIComponent(String(version))}`, {
    method: 'DELETE'
  });

  if (!response.ok) {
    let payload: unknown = null;
    try {
      payload = await response.json();
    } catch {
      payload = null;
    }

    throw new ApiError(response.status, `Request failed with status ${response.status}`, payload);
  }
}

export async function pollGroceryList(sinceIso: string): Promise<GroceryListPollResponse | null> {
  const response = await apiFetch(`/grocery-lists/poll?since=${encodeURIComponent(sinceIso)}`);

  if (response.status === 304) {
    return null;
  }

  if (!response.ok) {
    let payload: unknown = null;
    try {
      payload = await response.json();
    } catch {
      payload = null;
    }

    throw new ApiError(response.status, `Request failed with status ${response.status}`, payload);
  }

  return (await response.json()) as GroceryListPollResponse;
}

export async function getPantryStaples(): Promise<PantryStaples> {
  return await apiGet<PantryStaples>('/pantry/staples');
}

export async function replacePantryStaples(payload: ReplacePantryStaplesPayload): Promise<PantryStaples> {
  return await apiPut<PantryStaples, ReplacePantryStaplesPayload>('/pantry/staples', payload);
}

export async function addPantryStapleItem(payload: AddPantryStapleItemPayload): Promise<PantryStaples> {
  return await apiPost<PantryStaples, AddPantryStapleItemPayload>('/pantry/staples/items', payload);
}

export async function deletePantryStapleItem(name: string): Promise<void> {
  const response = await apiFetch(`/pantry/staples/items/${encodeURIComponent(name)}`, {
    method: 'DELETE'
  });

  if (!response.ok) {
    let payload: unknown = null;
    try {
      payload = await response.json();
    } catch {
      payload = null;
    }

    throw new ApiError(response.status, `Request failed with status ${response.status}`, payload);
  }
}
