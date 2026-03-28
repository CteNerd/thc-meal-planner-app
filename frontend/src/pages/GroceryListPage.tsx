import { useEffect, useMemo, useState } from 'react';
import { Button } from '../components/ui/Button';
import { Card } from '../components/ui/Card';
import {
  addGroceryItem,
  generateGroceryList,
  getCurrentGroceryList,
  pollGroceryList,
  removeGroceryItem,
  setGroceryItemInStock,
  toggleGroceryItem
} from '../services/groceryListApi';
import { ApiError, getApiErrorMessage } from '../services/api';
import type { GroceryItem, GroceryList } from '../types';

const DEFAULT_SECTION = 'produce';

export function GroceryListPage() {
  const [list, setList] = useState<GroceryList | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [newItemName, setNewItemName] = useState('');
  const [newItemSection, setNewItemSection] = useState(DEFAULT_SECTION);
  const [newItemQuantity, setNewItemQuantity] = useState('1');
  const [newItemUnit, setNewItemUnit] = useState('');

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setIsLoading(true);
        setError(null);
        const current = await getCurrentGroceryList();
        if (!active) {
          return;
        }

        setList(current);
      } catch (err) {
        if (!active) {
          return;
        }

        if (err instanceof ApiError && err.status === 404) {
          setList(null);
        } else {
          setError(getApiErrorMessage(err, 'Unable to load grocery list.'));
        }
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    }

    void load();

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (!list?.updatedAt) {
      return;
    }

    let active = true;

    const interval = window.setInterval(() => {
      if (document.hidden || !active) {
        return;
      }

      void (async () => {
        try {
          const poll = await pollGroceryList(list.updatedAt);
          if (!active || !poll?.hasChanges) {
            return;
          }

          const current = await getCurrentGroceryList();
          if (active) {
            setList(current);
          }
        } catch {
          // Ignore polling errors and continue normal user flows.
        }
      })();
    }, 5000);

    return () => {
      active = false;
      window.clearInterval(interval);
    };
  }, [list?.updatedAt]);

  const sectionGroups = useMemo(() => {
    if (!list) {
      return [];
    }

    const groups = new Map<string, GroceryItem[]>();
    for (const item of list.items) {
      const section = item.section?.trim() || 'other';
      const current = groups.get(section) ?? [];
      current.push(item);
      groups.set(section, current);
    }

    return Array.from(groups.entries())
      .map(([section, items]) => ({
        section,
        items: [...items].sort((a, b) => a.name.localeCompare(b.name))
      }))
      .sort((a, b) => a.section.localeCompare(b.section));
  }, [list]);

  const toBuyCount = useMemo(() => {
    return list?.items.filter((item) => !item.checkedOff && !item.inStock).length ?? 0;
  }, [list]);

  async function handleGenerate() {
    try {
      setIsBusy(true);
      setError(null);
      const generated = await generateGroceryList({ clearExisting: false });
      setList(generated);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to generate grocery list.'));
    } finally {
      setIsBusy(false);
    }
  }

  async function handleToggleChecked(item: GroceryItem) {
    if (!list) {
      return;
    }

    const snapshot = list;
    const optimisticItems = list.items.map((candidate) =>
      candidate.id === item.id ? { ...candidate, checkedOff: !candidate.checkedOff } : candidate
    );
    setList({ ...list, items: optimisticItems });

    try {
      const response = await toggleGroceryItem(item.id, { version: snapshot.version });
      setList((current) => {
        if (!current) {
          return current;
        }

        return {
          ...current,
          items: current.items.map((candidate) => (candidate.id === item.id ? response.item : candidate)),
          version: response.version,
          updatedAt: response.updatedAt,
          progress: response.progress
        };
      });
    } catch (err) {
      await recoverFromMutationError(err, snapshot, 'Unable to update item status.');
    }
  }

  async function handleToggleInStock(item: GroceryItem) {
    if (!list) {
      return;
    }

    const snapshot = list;
    const optimisticItems = list.items.map((candidate) =>
      candidate.id === item.id ? { ...candidate, inStock: !candidate.inStock } : candidate
    );
    setList({ ...list, items: optimisticItems });

    try {
      const response = await setGroceryItemInStock(item.id, {
        inStock: !item.inStock,
        version: snapshot.version
      });

      setList((current) => {
        if (!current) {
          return current;
        }

        return {
          ...current,
          items: current.items.map((candidate) => (candidate.id === item.id ? response.item : candidate)),
          version: response.version,
          updatedAt: response.updatedAt,
          progress: response.progress
        };
      });
    } catch (err) {
      await recoverFromMutationError(err, snapshot, 'Unable to update in-stock state.');
    }
  }

  async function handleAddItem() {
    if (!list) {
      return;
    }

    const name = newItemName.trim();
    if (!name) {
      setError('Item name is required.');
      return;
    }

    const quantity = Number.parseFloat(newItemQuantity);
    const normalizedQuantity = Number.isFinite(quantity) && quantity > 0 ? quantity : 1;

    try {
      setIsBusy(true);
      setError(null);

      const response = await addGroceryItem({
        name,
        section: newItemSection,
        quantity: normalizedQuantity,
        unit: newItemUnit.trim() || undefined,
        version: list.version
      });

      setList((current) => {
        if (!current) {
          return current;
        }

        return {
          ...current,
          items: [...current.items, response.item],
          version: response.version,
          updatedAt: response.updatedAt,
          progress: response.progress
        };
      });

      setNewItemName('');
      setNewItemQuantity('1');
      setNewItemUnit('');
      setNewItemSection(DEFAULT_SECTION);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        try {
          const latest = await getCurrentGroceryList();
          setList(latest);
        } catch {
          // Preserve current state and show conflict message.
        }
      }

      setError(getApiErrorMessage(err, 'Unable to add grocery item.'));
    } finally {
      setIsBusy(false);
    }
  }

  async function handleRemoveItem(item: GroceryItem) {
    if (!list) {
      return;
    }

    const snapshot = list;
    setList({ ...list, items: list.items.filter((candidate) => candidate.id !== item.id) });

    try {
      await removeGroceryItem(item.id, snapshot.version);
      const latest = await getCurrentGroceryList();
      setList(latest);
    } catch (err) {
      await recoverFromMutationError(err, snapshot, 'Unable to remove item.');
    }
  }

  async function recoverFromMutationError(error: unknown, snapshot: GroceryList, fallbackMessage: string) {
    if (error instanceof ApiError && error.status === 409) {
      try {
        const latest = await getCurrentGroceryList();
        setList(latest);
      } catch {
        setList(snapshot);
      }
    } else {
      setList(snapshot);
    }

    setError(getApiErrorMessage(error, fallbackMessage));
  }

  return (
    <Card>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-2xl font-semibold text-slate-900">Grocery List</h2>
          <p className="mt-2 text-sm text-slate-600">
            Shared live list for the week. Polling refreshes every 5 seconds while this tab is active.
          </p>
        </div>
        <Button type="button" onClick={handleGenerate} disabled={isBusy || isLoading}>
          {isBusy ? 'Working...' : 'Generate From Meal Plan'}
        </Button>
      </div>

      {isLoading ? <p className="mt-4 text-sm text-slate-600">Loading grocery list...</p> : null}
      {error ? <p className="mt-4 text-sm text-red-700">{error}</p> : null}

      {!isLoading && !list ? (
        <div className="mt-5 rounded-2xl border border-dashed border-slate-300 bg-white/70 p-5 text-sm text-slate-600">
          No active grocery list exists yet. Generate from the active meal plan to start collaborating.
        </div>
      ) : null}

      {list ? (
        <div className="mt-5 space-y-4">
          <div className="grid gap-3 md:grid-cols-4">
            <MetricCard label="To Buy" value={toBuyCount} />
            <MetricCard label="Completed" value={list.progress.completed} />
            <MetricCard label="Total Items" value={list.progress.total} />
            <MetricCard label="Progress" value={`${list.progress.percentage}%`} />
          </div>

          <div className="rounded-2xl bg-slate-50 p-4 ring-1 ring-slate-200">
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Add Item</p>
            <div className="mt-3 grid gap-2 sm:grid-cols-2 md:grid-cols-5">
              <input
                placeholder="Item name"
                className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none transition focus:border-sky-400 focus:ring-4 focus:ring-sky-100"
                value={newItemName}
                onChange={(event) => setNewItemName(event.target.value)}
              />
              <select
                className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none transition focus:border-sky-400 focus:ring-4 focus:ring-sky-100"
                value={newItemSection}
                onChange={(event) => setNewItemSection(event.target.value)}
              >
                <option value="produce">produce</option>
                <option value="protein">protein</option>
                <option value="dairy">dairy</option>
                <option value="pantry">pantry</option>
                <option value="frozen">frozen</option>
                <option value="household">household</option>
                <option value="other">other</option>
              </select>
              <input
                placeholder="Qty"
                className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none transition focus:border-sky-400 focus:ring-4 focus:ring-sky-100"
                value={newItemQuantity}
                onChange={(event) => setNewItemQuantity(event.target.value)}
              />
              <input
                placeholder="Unit"
                className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none transition focus:border-sky-400 focus:ring-4 focus:ring-sky-100"
                value={newItemUnit}
                onChange={(event) => setNewItemUnit(event.target.value)}
              />
              <Button type="button" onClick={handleAddItem} disabled={isBusy}>
                Add
              </Button>
            </div>
          </div>

          <div className="space-y-3">
            {sectionGroups.length === 0 ? (
              <p className="text-sm text-slate-600">No items in this list yet.</p>
            ) : (
              sectionGroups.map((group) => (
                <section key={group.section} className="rounded-2xl bg-white ring-1 ring-slate-200">
                  <header className="border-b border-slate-100 px-4 py-3">
                    <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">{group.section}</p>
                  </header>
                  <ul className="divide-y divide-slate-100">
                    {group.items.map((item) => (
                      <li key={item.id} className="flex flex-wrap items-center gap-3 px-4 py-3">
                        <input
                          type="checkbox"
                          checked={item.checkedOff}
                          onChange={() => {
                            void handleToggleChecked(item);
                          }}
                          aria-label={`toggle checked ${item.name}`}
                          className="h-4 w-4 rounded border-slate-300 text-sky-500 focus:ring-sky-400"
                        />
                        <div className="min-w-[220px] flex-1">
                          <p className={`text-sm font-medium ${item.checkedOff ? 'text-slate-400 line-through' : 'text-slate-900'}`}>
                            {item.name}
                          </p>
                          <p className="text-xs text-slate-500">
                            {item.quantity} {item.unit ?? ''} • {item.mealAssociations.length} meal link
                            {item.mealAssociations.length === 1 ? '' : 's'}
                          </p>
                        </div>
                        <Button
                          type="button"
                          variant={item.inStock ? 'secondary' : 'ghost'}
                          onClick={() => {
                            void handleToggleInStock(item);
                          }}
                        >
                          {item.inStock ? 'In Stock' : 'Mark In Stock'}
                        </Button>
                        <Button
                          type="button"
                          variant="ghost"
                          onClick={() => {
                            void handleRemoveItem(item);
                          }}
                        >
                          Remove
                        </Button>
                      </li>
                    ))}
                  </ul>
                </section>
              ))
            )}
          </div>
        </div>
      ) : null}
    </Card>
  );
}

function MetricCard({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-2xl bg-slate-50 p-4 ring-1 ring-slate-200">
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</p>
      <p className="mt-1 text-lg font-semibold text-slate-900">{value}</p>
    </div>
  );
}