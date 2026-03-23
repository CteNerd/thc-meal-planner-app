import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Button } from '../components/ui/Button';
import { Card } from '../components/ui/Card';
import { Input } from '../components/ui/Input';
import { getApiErrorMessage } from '../services/api';
import {
  createRecipe,
  createRecipeUploadUrl,
  getRecipe,
  importRecipeFromUrl,
  updateRecipe,
  uploadRecipeImage
} from '../services/recipeApi';
import type { Recipe, RecipeIngredient, RecipePayload } from '../types';

type RecipeFormState = {
  name: string;
  description: string;
  category: string;
  cuisine: string;
  servings: string;
  prepTimeMinutes: string;
  cookTimeMinutes: string;
  proteinSource: string;
  cookingMethod: string;
  difficulty: string;
  tags: string;
  ingredients: string;
  instructions: string;
  variations: string;
  storageInfo: string;
  sourceType: string;
  sourceUrl: string;
  imageKey: string;
};

const emptyForm: RecipeFormState = {
  name: '',
  description: '',
  category: 'dinner',
  cuisine: '',
  servings: '',
  prepTimeMinutes: '',
  cookTimeMinutes: '',
  proteinSource: '',
  cookingMethod: '',
  difficulty: '',
  tags: '',
  ingredients: '',
  instructions: '',
  variations: '',
  storageInfo: '',
  sourceType: 'manual',
  sourceUrl: '',
  imageKey: ''
};

export function RecipeEditorPage() {
  const { recipeId } = useParams();
  const isEditMode = Boolean(recipeId);
  const navigate = useNavigate();
  const [form, setForm] = useState<RecipeFormState>(emptyForm);
  const [importUrl, setImportUrl] = useState('');
  const [importWarnings, setImportWarnings] = useState<string[]>([]);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isLoading, setIsLoading] = useState(isEditMode);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    async function loadRecipe() {
      if (!recipeId) {
        return;
      }

      try {
        const recipe = await getRecipe(recipeId);
        if (!active) {
          return;
        }

        setForm(toFormState(recipe));
      } catch (err) {
        if (active) {
          setError(getApiErrorMessage(err, 'Unable to load recipe for editing.'));
        }
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    }

    void loadRecipe();

    return () => {
      active = false;
    };
  }, [recipeId]);

  const ingredientPreviewCount = useMemo(() => {
    return parseIngredients(form.ingredients).length;
  }, [form.ingredients]);

  const instructionPreviewCount = useMemo(() => {
    return parseInstructions(form.instructions).length;
  }, [form.instructions]);

  async function handleImport() {
    if (!importUrl.trim()) {
      setError('Recipe URL is required for import.');
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      const draft = await importRecipeFromUrl({ url: importUrl.trim() });
      setForm((current) => ({
        ...current,
        name: draft.name,
        description: draft.description ?? '',
        category: draft.category,
        cuisine: draft.cuisine ?? '',
        servings: draft.servings?.toString() ?? '',
        prepTimeMinutes: draft.prepTimeMinutes?.toString() ?? '',
        cookTimeMinutes: draft.cookTimeMinutes?.toString() ?? '',
        proteinSource: draft.proteinSource ?? '',
        cookingMethod: draft.cookingMethod ?? '',
        difficulty: draft.difficulty ?? '',
        tags: draft.tags.join(', '),
        ingredients: formatIngredients(draft.ingredients),
        instructions: draft.instructions.join('\n'),
        sourceType: draft.sourceType,
        sourceUrl: draft.sourceUrl
      }));
      setImportWarnings(draft.warnings);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to import recipe draft from URL.'));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      setIsSaving(true);
      setError(null);
      const payload = toPayload(form);
      const savedRecipe = isEditMode && recipeId
        ? await updateRecipe(recipeId, payload)
        : await createRecipe(payload);

      let finalRecipe = savedRecipe;
      if (selectedFile) {
        const upload = await createRecipeUploadUrl(savedRecipe.recipeId, {
          fileName: selectedFile.name,
          contentType: selectedFile.type
        });
        await uploadRecipeImage(upload.uploadUrl, selectedFile);
        finalRecipe = await updateRecipe(savedRecipe.recipeId, {
          imageKey: upload.imageKey
        });
      }

      navigate(`/cookbook/${finalRecipe.recipeId}`);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to save recipe.'));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Card>
      {isLoading ? (
        <p className="text-sm text-slate-600">Loading recipe editor...</p>
      ) : (
        <div className="space-y-6">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <Link to={isEditMode && recipeId ? `/cookbook/${recipeId}` : '/cookbook'} className="text-xs font-semibold uppercase tracking-[0.2em] text-sky-700">
                {isEditMode ? 'Back to recipe' : 'Back to cookbook'}
              </Link>
              <h2 className="mt-2 text-3xl font-semibold text-slate-900">{isEditMode ? 'Edit recipe' : 'Add recipe'}</h2>
              <p className="mt-2 text-sm text-slate-600">Manual recipe entry with URL import draft review and image upload workflow.</p>
            </div>
          </div>

          <section className="rounded-3xl bg-slate-50 p-5">
            <h3 className="text-lg font-semibold text-slate-900">Import from URL</h3>
            <div className="mt-4 grid gap-3 md:grid-cols-[1fr_auto]">
              <Input value={importUrl} onChange={(event) => setImportUrl(event.target.value)} aria-label="Recipe import URL" placeholder="https://example.com/recipe" />
              <Button type="button" onClick={() => void handleImport()} disabled={isSaving}>Import draft</Button>
            </div>
            {importWarnings.length > 0 ? (
              <ul className="mt-3 space-y-1 text-sm text-amber-800">
                {importWarnings.map((warning) => (
                  <li key={warning}>{warning}</li>
                ))}
              </ul>
            ) : null}
          </section>

          <form className="space-y-6" onSubmit={handleSubmit}>
            {error ? <p className="text-sm text-red-700">{error}</p> : null}

            <section className="grid gap-4 md:grid-cols-2">
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Name
                <Input value={form.name} onChange={(event) => updateField(setForm, 'name', event.target.value)} aria-label="Recipe name" />
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Category
                <select value={form.category} onChange={(event) => updateField(setForm, 'category', event.target.value)} aria-label="Recipe category" className="w-full rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-sky-400 focus:ring-4 focus:ring-sky-100">
                  <option value="breakfast">breakfast</option>
                  <option value="lunch">lunch</option>
                  <option value="dinner">dinner</option>
                  <option value="snack">snack</option>
                </select>
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700 md:col-span-2">
                Description
                <textarea value={form.description} onChange={(event) => updateField(setForm, 'description', event.target.value)} aria-label="Recipe description" rows={3} className="w-full rounded-3xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-sky-400 focus:ring-4 focus:ring-sky-100" />
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Cuisine
                <Input value={form.cuisine} onChange={(event) => updateField(setForm, 'cuisine', event.target.value)} aria-label="Recipe cuisine" />
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Tags
                <Input value={form.tags} onChange={(event) => updateField(setForm, 'tags', event.target.value)} aria-label="Recipe tags" placeholder="kid-friendly, quick" />
              </label>
            </section>

            <section className="grid gap-4 md:grid-cols-3">
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Servings
                <Input value={form.servings} onChange={(event) => updateField(setForm, 'servings', event.target.value)} aria-label="Recipe servings" inputMode="numeric" />
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Prep minutes
                <Input value={form.prepTimeMinutes} onChange={(event) => updateField(setForm, 'prepTimeMinutes', event.target.value)} aria-label="Recipe prep time" inputMode="numeric" />
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Cook minutes
                <Input value={form.cookTimeMinutes} onChange={(event) => updateField(setForm, 'cookTimeMinutes', event.target.value)} aria-label="Recipe cook time" inputMode="numeric" />
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Protein source
                <Input value={form.proteinSource} onChange={(event) => updateField(setForm, 'proteinSource', event.target.value)} aria-label="Recipe protein source" />
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Cooking method
                <Input value={form.cookingMethod} onChange={(event) => updateField(setForm, 'cookingMethod', event.target.value)} aria-label="Recipe cooking method" />
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Difficulty
                <Input value={form.difficulty} onChange={(event) => updateField(setForm, 'difficulty', event.target.value)} aria-label="Recipe difficulty" />
              </label>
            </section>

            <section className="grid gap-4 lg:grid-cols-2">
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Ingredients
                <textarea
                  value={form.ingredients}
                  onChange={(event) => updateField(setForm, 'ingredients', event.target.value)}
                  aria-label="Recipe ingredients"
                  rows={10}
                  className="w-full rounded-3xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-sky-400 focus:ring-4 focus:ring-sky-100"
                  placeholder="One ingredient per line. Use quantity|unit|name|section|notes"
                />
                <p className="text-xs text-slate-500">Parsed ingredients: {ingredientPreviewCount}</p>
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Instructions
                <textarea
                  value={form.instructions}
                  onChange={(event) => updateField(setForm, 'instructions', event.target.value)}
                  aria-label="Recipe instructions"
                  rows={10}
                  className="w-full rounded-3xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-sky-400 focus:ring-4 focus:ring-sky-100"
                  placeholder="One instruction per line"
                />
                <p className="text-xs text-slate-500">Parsed steps: {instructionPreviewCount}</p>
              </label>
            </section>

            <section className="grid gap-4 lg:grid-cols-2">
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Variations
                <textarea value={form.variations} onChange={(event) => updateField(setForm, 'variations', event.target.value)} aria-label="Recipe variations" rows={4} className="w-full rounded-3xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-sky-400 focus:ring-4 focus:ring-sky-100" />
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Storage info
                <textarea value={form.storageInfo} onChange={(event) => updateField(setForm, 'storageInfo', event.target.value)} aria-label="Recipe storage info" rows={4} className="w-full rounded-3xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-sky-400 focus:ring-4 focus:ring-sky-100" />
              </label>
            </section>

            <section className="grid gap-4 lg:grid-cols-2">
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Source URL
                <Input value={form.sourceUrl} onChange={(event) => updateField(setForm, 'sourceUrl', event.target.value)} aria-label="Recipe source URL" />
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Recipe image
                <input type="file" accept="image/jpeg,image/png,image/webp" aria-label="Recipe image file" onChange={(event) => setSelectedFile(event.target.files?.[0] ?? null)} className="w-full rounded-2xl border border-dashed border-slate-300 bg-white px-4 py-3 text-sm text-slate-700" />
              </label>
            </section>

            <div className="flex flex-wrap justify-end gap-3">
              <Link to={isEditMode && recipeId ? `/cookbook/${recipeId}` : '/cookbook'}>
                <Button type="button" variant="ghost">Cancel</Button>
              </Link>
              <Button type="submit" disabled={isSaving}>{isSaving ? 'Saving...' : isEditMode ? 'Save recipe' : 'Create recipe'}</Button>
            </div>
          </form>
        </div>
      )}
    </Card>
  );
}

function updateField(
  setForm: React.Dispatch<React.SetStateAction<RecipeFormState>>,
  field: keyof RecipeFormState,
  value: string
) {
  setForm((current) => ({ ...current, [field]: value }));
}

function toFormState(recipe: Recipe): RecipeFormState {
  return {
    name: recipe.name,
    description: recipe.description ?? '',
    category: recipe.category,
    cuisine: recipe.cuisine ?? '',
    servings: recipe.servings?.toString() ?? '',
    prepTimeMinutes: recipe.prepTimeMinutes?.toString() ?? '',
    cookTimeMinutes: recipe.cookTimeMinutes?.toString() ?? '',
    proteinSource: recipe.proteinSource ?? '',
    cookingMethod: recipe.cookingMethod ?? '',
    difficulty: recipe.difficulty ?? '',
    tags: recipe.tags.join(', '),
    ingredients: formatIngredients(recipe.ingredients),
    instructions: recipe.instructions.join('\n'),
    variations: recipe.variations ?? '',
    storageInfo: recipe.storageInfo ?? '',
    sourceType: recipe.sourceType,
    sourceUrl: recipe.sourceUrl ?? '',
    imageKey: recipe.imageKey ?? ''
  };
}

function toPayload(form: RecipeFormState): RecipePayload {
  return {
    name: form.name.trim(),
    description: form.description.trim(),
    category: form.category,
    cuisine: form.cuisine.trim(),
    servings: parseOptionalNumber(form.servings),
    prepTimeMinutes: parseOptionalNumber(form.prepTimeMinutes),
    cookTimeMinutes: parseOptionalNumber(form.cookTimeMinutes),
    proteinSource: form.proteinSource.trim(),
    cookingMethod: form.cookingMethod.trim(),
    difficulty: form.difficulty.trim(),
    tags: form.tags.split(',').map((tag) => tag.trim()).filter(Boolean),
    ingredients: parseIngredients(form.ingredients),
    instructions: parseInstructions(form.instructions),
    imageKey: form.imageKey.trim() || undefined,
    sourceType: form.sourceType || 'manual',
    sourceUrl: form.sourceUrl.trim() || undefined,
    variations: form.variations.trim(),
    storageInfo: form.storageInfo.trim()
  };
}

function parseOptionalNumber(value: string): number | undefined {
  const trimmed = value.trim();
  if (!trimmed) {
    return undefined;
  }

  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function parseIngredients(value: string): RecipeIngredient[] {
  return value
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const [quantity = '', unit = '', name = '', section = '', notes = ''] = line.split('|').map((part) => part.trim());
      if (!name) {
        return { name: quantity };
      }

      return {
        name,
        quantity: quantity || undefined,
        unit: unit || undefined,
        section: section || undefined,
        notes: notes || undefined
      };
    });
}

function formatIngredients(ingredients: RecipeIngredient[]): string {
  return ingredients
    .map((ingredient) => [ingredient.quantity, ingredient.unit, ingredient.name, ingredient.section, ingredient.notes].map((part) => part ?? '').join('|').replace(/\|+$/, ''))
    .join('\n');
}

function parseInstructions(value: string): string[] {
  return value
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);
}