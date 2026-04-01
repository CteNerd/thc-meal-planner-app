import React, { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Button } from '../components/ui/Button';
import { Card } from '../components/ui/Card';
import { Input } from '../components/ui/Input';
import { TagInput } from '../components/ui/TagInput';
import { ApiError, getApiErrorMessage, getApiValidationErrors } from '../services/api';
import {
  createRecipe,
  createRecipeUploadUrl,
  getRecipe,
  importRecipeFromImage,
  importRecipeFromUrl,
  updateRecipe,
  uploadRecipeImage
} from '../services/recipeApi';
import { mapImportedValuesToOptions } from '../utils/recipeImportMapper';
import {
  CUISINE_OPTIONS,
  COOKING_METHOD_OPTIONS,
  COOK_MINUTES_OPTIONS,
  DIFFICULTY_OPTIONS,
  PREP_MINUTES_OPTIONS,
  PROTEIN_SOURCE_OPTIONS,
  SERVINGS_OPTIONS,
  TAG_SUGGESTIONS
} from '../constants/recipeOptions';
import type { ImportedRecipeDraft, Recipe, RecipeIngredient, RecipePayload, UpdateRecipePayload } from '../types';

type RecipeFormState = {
  name: string;
  description: string;
  category: string;
  cuisine: string;
  servings: string;
  prepTimeMinutes: string;
  cookTimeMinutes: string;
  proteinSource: string[];
  cookingMethod: string[];
  difficulty: string;
  tags: string[];
  ingredients: string;
  instructions: string;
  variations: string;
  storageInfo: string;
  sourceType: string;
  sourceUrl: string;
  imageKey: string;
  imageUrl: string;
};

const emptyForm: RecipeFormState = {
  name: '',
  description: '',
  category: 'dinner',
  cuisine: '',
  servings: '',
  prepTimeMinutes: '',
  cookTimeMinutes: '',
  proteinSource: [],
  cookingMethod: [],
  difficulty: '',
  tags: [],
  ingredients: '',
  instructions: '',
  variations: '',
  storageInfo: '',
  sourceType: 'manual',
  sourceUrl: '',
  imageKey: '',
  imageUrl: ''
};

export function RecipeEditorPage() {
  const { recipeId } = useParams();
  const isEditMode = Boolean(recipeId);
  const navigate = useNavigate();
  const [form, setForm] = useState<RecipeFormState>(emptyForm);
  const [importUrl, setImportUrl] = useState('');
  const [importWarnings, setImportWarnings] = useState<string[]>([]);
  const [imageImportWarnings, setImageImportWarnings] = useState<string[]>([]);
  const [imageImportRateLimited, setImageImportRateLimited] = useState<string | null>(null); // holds pending imageKey when 429 hit
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [selectedFilePreviewUrl, setSelectedFilePreviewUrl] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(isEditMode);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<Record<string, string[]>>({});

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

  useEffect(() => {
    if (!selectedFile) {
      setSelectedFilePreviewUrl(null);
      return;
    }

    const objectUrl = URL.createObjectURL(selectedFile);
    setSelectedFilePreviewUrl(objectUrl);

    return () => {
      URL.revokeObjectURL(objectUrl);
    };
  }, [selectedFile]);

  const ingredientPreviewCount = useMemo(() => {
    return parseIngredients(form.ingredients).length;
  }, [form.ingredients]);

  const instructionPreviewCount = useMemo(() => {
    return parseInstructions(form.instructions).length;
  }, [form.instructions]);

  const displayedImageUrl = selectedFilePreviewUrl ?? form.imageUrl;

  async function handleImport() {
    if (!importUrl.trim()) {
      setError('Recipe URL is required for import.');
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      setImportWarnings([]);
      const draft = await importRecipeFromUrl({ url: importUrl.trim() });

      setForm((current) => ({
        ...current,
        ...mapDraftToFormValues(draft)
      }));
      setImportWarnings(draft.warnings);
    } catch (err) {
      const message = getApiErrorMessage(err, 'Unable to import recipe draft from URL.');
      if (/could not be converted|cannot be converted|mapped to an array|element of type/i.test(message)) {
        setError('That recipe site uses a non-standard format we could not auto-map. Try another URL, or create a draft manually and paste ingredients/instructions.');
      } else {
        setError(message);
      }
    } finally {
      setIsSaving(false);
    }
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      setIsSaving(true);
      setError(null);
      setImageImportWarnings([]);
      setValidationErrors({});
      const payload = toPayload(form);
      const savedRecipe = isEditMode && recipeId
        ? await updateRecipe(recipeId, payload)
        : await createRecipe(payload);

      let finalRecipe = savedRecipe;
      if (selectedFile) {
        finalRecipe = await uploadSelectedImage(savedRecipe.recipeId);
      }

      navigate(`/cookbook/${finalRecipe.recipeId}`);
    } catch (err) {
      const fieldErrors = getApiValidationErrors(err);
      if (fieldErrors) {
        setValidationErrors(fieldErrors);
      }
      setError(getApiErrorMessage(err, 'Unable to save recipe.'));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleCreateImageDraft() {
    if (isEditMode) {
      setError('Image-first draft is only available when creating a new recipe.');
      return;
    }

    if (!selectedFile) {
      setError('Choose a recipe photo first.');
      return;
    }

    try {
      setIsSaving(true);
      setError(null);

      const draftName = selectedFile.name.replace(/\.[^/.]+$/, '').trim();
      const baseRecipe = await createRecipe({
        name: draftName.length > 0 ? draftName : `Photo recipe draft ${new Date().toISOString().slice(0, 10)}`,
        category: 'dinner',
        cuisine: '',
        description: 'Draft created from recipe image upload. Complete details after review.',
        ingredients: [
          {
            name: 'Review uploaded image and add ingredients',
            quantity: undefined,
            unit: undefined,
            section: undefined,
            notes: undefined
          }
        ],
        instructions: ['Review uploaded image and add preparation steps.'],
        tags: ['draft'],
        sourceType: 'image_upload'
      });

      const upload = await createRecipeUploadUrl(baseRecipe.recipeId, {
        fileName: selectedFile.name,
        contentType: selectedFile.type
      });
      await uploadRecipeImage(upload.uploadUrl, selectedFile);

      const updated = await updateRecipe(baseRecipe.recipeId, {
        imageKey: upload.imageKey,
        sourceType: 'image_upload'
      });

      let extractedRecipe = updated;
      try {
        const draft = await importRecipeFromImage(baseRecipe.recipeId, { imageKey: upload.imageKey });
        extractedRecipe = await updateRecipe(baseRecipe.recipeId, {
          ...toUpdatePayloadFromDraft(draft),
          imageKey: upload.imageKey,
          sourceType: 'image_upload'
        });
        setImageImportWarnings(draft.warnings);
        setImageImportRateLimited(null);
      } catch (importErr) {
        if (importErr instanceof ApiError && importErr.status === 429) {
          setImageImportRateLimited(upload.imageKey);
          setImageImportWarnings([]);
        } else {
          setImageImportWarnings([
            getApiErrorMessage(
              importErr,
              'Image uploaded, but AI extraction could not parse details. You can edit manually or re-run extraction from the edit page.'
            )
          ]);
        }
      }

      navigate(`/cookbook/${extractedRecipe.recipeId}/edit`);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to create image draft recipe. Try a JPG/PNG/WEBP image under 10MB.'));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleOcrRetry(imageKey: string) {
    const targetRecipeId = recipeId ?? imageImportRateLimited ? recipeId : null;
    if (!targetRecipeId && !recipeId) {
      setError('Cannot retry OCR: recipe is not saved yet.');
      return;
    }
    const id = recipeId ?? '';
    try {
      setIsSaving(true);
      setError(null);
      setImageImportRateLimited(null);
      setImageImportWarnings([]);
      const draft = await importRecipeFromImage(id, { imageKey, preferOcr: true });
      const updated = await updateRecipe(id, {
        ...toUpdatePayloadFromDraft(draft),
        imageKey,
        sourceType: 'image_upload'
      });
      setForm(toFormState(updated));
      setImageImportWarnings(draft.warnings);
    } catch (err) {
      setError(getApiErrorMessage(err, 'OCR extraction failed. You can edit the recipe manually.'));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleReRunImageExtraction() {
    if (!recipeId || (!form.imageKey && !selectedFile)) {
      setError('No uploaded image found for extraction.');
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      setImageImportWarnings([]);
      setImageImportRateLimited(null);
      let activeImageKey = form.imageKey;

      if (selectedFile) {
        const updatedRecipe = await uploadSelectedImage(recipeId, 'image_upload');
        activeImageKey = updatedRecipe.imageKey ?? activeImageKey;
      }

      const draft = await importRecipeFromImage(recipeId, { imageKey: activeImageKey });
      const updated = await updateRecipe(recipeId, {
        ...toUpdatePayloadFromDraft(draft),
        imageKey: activeImageKey,
        sourceType: 'image_upload'
      });
      setForm(toFormState(updated));
      setSelectedFile(null);
      setImageImportWarnings(draft.warnings);
    } catch (err) {
      if (err instanceof ApiError && err.status === 429) {
        setImageImportRateLimited(form.imageKey);
        setImageImportWarnings([]);
      } else {
        setError(getApiErrorMessage(err, 'Unable to re-run AI extraction from the uploaded image.'));
      }
    } finally {
      setIsSaving(false);
    }
  }

  async function uploadSelectedImage(targetRecipeId: string, sourceType?: string) {
    if (!selectedFile) {
      throw new Error('No image selected.');
    }

    const upload = await createRecipeUploadUrl(targetRecipeId, {
      fileName: selectedFile.name,
      contentType: selectedFile.type
    });
    await uploadRecipeImage(upload.uploadUrl, selectedFile);

    const updatedRecipe = await updateRecipe(targetRecipeId, {
      imageKey: upload.imageKey,
      sourceType
    });

    setForm((current) => ({
      ...current,
      imageKey: updatedRecipe.imageKey ?? upload.imageKey,
      imageUrl: updatedRecipe.imageUrl ?? upload.imageUrl,
      sourceType: updatedRecipe.sourceType
    }));

    return updatedRecipe;
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

          <section className="rounded-3xl bg-slate-50 p-5">
            <h3 className="text-lg font-semibold text-slate-900">Quick capture from photo</h3>
            <p className="mt-1 text-sm text-slate-600">Snap a recipe card or handwritten note, upload now, and complete details later.</p>
            <div className="mt-4 grid gap-3 md:grid-cols-[1fr_auto]">
              <input
                type="file"
                accept="image/jpeg,image/png,image/webp"
                aria-label="Recipe image file"
                onChange={(event) => setSelectedFile(event.target.files?.[0] ?? null)}
                className="w-full rounded-2xl border border-dashed border-slate-300 bg-white px-4 py-3 text-sm text-slate-700"
              />
              <Button type="button" onClick={() => void handleCreateImageDraft()} disabled={isSaving || isEditMode || !selectedFile}>
                Create draft from photo
              </Button>
            </div>
            {imageImportWarnings.length > 0 ? (
              <ul className="mt-3 space-y-1 text-sm text-amber-800">
                {imageImportWarnings.map((warning) => (
                  <li key={warning}>{warning}</li>
                ))}
              </ul>
            ) : null}
            {imageImportRateLimited ? (
              <div className="mt-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 space-y-2">
                <p className="text-sm font-medium text-amber-900">AI Vision is temporarily rate limited</p>
                <p className="text-xs text-amber-800">
                  OpenAI returned a rate limit error. You can retry using AWS Textract OCR instead — OCR reads raw text from the image and may need more manual cleanup than AI Vision, but it does not have rate limits.
                </p>
                <Button type="button" onClick={() => void handleOcrRetry(imageImportRateLimited)} disabled={isSaving}>
                  Retry with OCR (lower fidelity)
                </Button>
              </div>
            ) : null}
          </section>

          <form className="space-y-6" onSubmit={handleSubmit}>
            {error ? <p className="text-sm text-red-700">{error}</p> : null}
            {Object.keys(validationErrors).length > 0 ? (
              <ul className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 list-disc list-inside space-y-0.5">
                {Object.entries(validationErrors).flatMap(([, msgs]) => msgs).map((msg, i) => (
                  <li key={i}>{msg}</li>
                ))}
              </ul>
            ) : null}

            <section className="grid gap-4 md:grid-cols-2">
              <label className="space-y-2 text-sm font-medium text-slate-700">
                <span>Name <span className="text-red-600">*</span></span>
                <Input required hasError={hasFieldError(validationErrors, 'name')} value={form.name} onChange={(event) => updateField(setForm, 'name', event.target.value)} aria-label="Recipe name" placeholder="Family Taco Bowls" />
                {fieldErrorMessage(validationErrors, 'name')}
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                <span>Category <span className="text-red-600">*</span></span>
                <select required value={form.category} onChange={(event) => updateField(setForm, 'category', event.target.value)} aria-label="Recipe category" className={`w-full rounded-2xl border bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:ring-4 ${hasFieldError(validationErrors, 'category') ? 'border-red-400 focus:border-red-500 focus:ring-red-100' : 'border-slate-200 focus:border-sky-400 focus:ring-sky-100'}`}>
                  <option value="breakfast">breakfast</option>
                  <option value="lunch">lunch</option>
                  <option value="dinner">dinner</option>
                  <option value="snack">snack</option>
                </select>
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700 md:col-span-2">
                Description
                <textarea value={form.description} onChange={(event) => updateField(setForm, 'description', event.target.value)} aria-label="Recipe description" rows={3} placeholder="One-sentence overview of flavor and prep style" className={`w-full rounded-3xl border bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:ring-4 ${hasFieldError(validationErrors, 'description') ? 'border-red-400 focus:border-red-500 focus:ring-red-100' : 'border-slate-200 focus:border-sky-400 focus:ring-sky-100'}`} />
                {fieldErrorMessage(validationErrors, 'description')}
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Cuisine
                <select value={form.cuisine} onChange={(event) => updateField(setForm, 'cuisine', event.target.value)} aria-label="Recipe cuisine" className={`w-full rounded-2xl border bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:ring-4 ${hasFieldError(validationErrors, 'cuisine') ? 'border-red-400 focus:border-red-500 focus:ring-red-100' : 'border-slate-200 focus:border-sky-400 focus:ring-sky-100'}`}>
                  <option value="">Select or enter custom cuisine</option>
                  {CUISINE_OPTIONS.map((c) => (
                    <option key={c} value={c}>
                      {c}
                    </option>
                  ))}
                </select>
                {fieldErrorMessage(validationErrors, 'cuisine')}
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Tags
                <TagInput hasError={hasFieldError(validationErrors, 'tags')} values={form.tags} onChange={(tags) => setForm((c) => ({ ...c, tags }))} placeholder="Type a tag and press Enter" suggestions={TAG_SUGGESTIONS} />
                {fieldErrorMessage(validationErrors, 'tags')}
              </label>
            </section>

            <section className="grid gap-4 md:grid-cols-3">
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Servings
                <select value={form.servings} onChange={(event) => updateField(setForm, 'servings', event.target.value)} aria-label="Recipe servings" className={`w-full rounded-2xl border bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:ring-4 ${hasFieldError(validationErrors, 'servings') ? 'border-red-400 focus:border-red-500 focus:ring-red-100' : 'border-slate-200 focus:border-sky-400 focus:ring-sky-100'}`}>
                  <option value="">Select servings</option>
                  {SERVINGS_OPTIONS.map((s) => (
                    <option key={s} value={s}>
                      {s}
                    </option>
                  ))}
                </select>
                {fieldErrorMessage(validationErrors, 'servings')}
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Prep minutes
                <select value={form.prepTimeMinutes} onChange={(event) => updateField(setForm, 'prepTimeMinutes', event.target.value)} aria-label="Recipe prep time" className={`w-full rounded-2xl border bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:ring-4 ${hasFieldError(validationErrors, 'prepTimeMinutes') ? 'border-red-400 focus:border-red-500 focus:ring-red-100' : 'border-slate-200 focus:border-sky-400 focus:ring-sky-100'}`}>
                  <option value="">Select prep time</option>
                  {PREP_MINUTES_OPTIONS.map((p) => (
                    <option key={p} value={p}>
                      {p} min
                    </option>
                  ))}
                </select>
                {fieldErrorMessage(validationErrors, 'prepTimeMinutes')}
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Cook minutes
                <select value={form.cookTimeMinutes} onChange={(event) => updateField(setForm, 'cookTimeMinutes', event.target.value)} aria-label="Recipe cook time" className={`w-full rounded-2xl border bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:ring-4 ${hasFieldError(validationErrors, 'cookTimeMinutes') ? 'border-red-400 focus:border-red-500 focus:ring-red-100' : 'border-slate-200 focus:border-sky-400 focus:ring-sky-100'}`}>
                  <option value="">Select cook time</option>
                  {COOK_MINUTES_OPTIONS.map((c) => (
                    <option key={c} value={c}>
                      {c} min
                    </option>
                  ))}
                </select>
                {fieldErrorMessage(validationErrors, 'cookTimeMinutes')}
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Protein source
                <TagInput hasError={hasFieldError(validationErrors, 'proteinSource')} values={form.proteinSource} onChange={(sources) => setForm((c) => ({ ...c, proteinSource: sources }))} placeholder="Type a protein and press Enter" suggestions={PROTEIN_SOURCE_OPTIONS} />
                {fieldErrorMessage(validationErrors, 'proteinSource')}
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Cooking method
                <TagInput hasError={hasFieldError(validationErrors, 'cookingMethod')} values={form.cookingMethod} onChange={(methods) => setForm((c) => ({ ...c, cookingMethod: methods }))} placeholder="Type a method and press Enter" suggestions={COOKING_METHOD_OPTIONS} />
                {fieldErrorMessage(validationErrors, 'cookingMethod')}
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Difficulty
                <select value={form.difficulty} onChange={(event) => updateField(setForm, 'difficulty', event.target.value)} aria-label="Recipe difficulty" className={`w-full rounded-2xl border bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:ring-4 ${hasFieldError(validationErrors, 'difficulty') ? 'border-red-400 focus:border-red-500 focus:ring-red-100' : 'border-slate-200 focus:border-sky-400 focus:ring-sky-100'}`}>
                  <option value="">Select difficulty level</option>
                  {DIFFICULTY_OPTIONS.map((d) => (
                    <option key={d} value={d}>
                      {d}
                    </option>
                  ))}
                </select>
                {fieldErrorMessage(validationErrors, 'difficulty')}
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
                  className={`w-full rounded-3xl border bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:ring-4 ${hasFieldError(validationErrors, 'ingredients') ? 'border-red-400 focus:border-red-500 focus:ring-red-100' : 'border-slate-200 focus:border-sky-400 focus:ring-sky-100'}`}
                  placeholder="One ingredient per line. Use quantity|unit|name|section|notes"
                />
                {fieldErrorMessage(validationErrors, 'ingredients')}
                <p className="text-xs text-slate-500">Parsed ingredients: {ingredientPreviewCount}</p>
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Instructions
                <textarea
                  value={form.instructions}
                  onChange={(event) => updateField(setForm, 'instructions', event.target.value)}
                  aria-label="Recipe instructions"
                  rows={10}
                  className={`w-full rounded-3xl border bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:ring-4 ${hasFieldError(validationErrors, 'instructions') ? 'border-red-400 focus:border-red-500 focus:ring-red-100' : 'border-slate-200 focus:border-sky-400 focus:ring-sky-100'}`}
                  placeholder="One instruction per line"
                />
                {fieldErrorMessage(validationErrors, 'instructions')}
                <p className="text-xs text-slate-500">Parsed steps: {instructionPreviewCount}</p>
              </label>
            </section>

            <section className="grid gap-4 lg:grid-cols-2">
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Variations
                <textarea value={form.variations} onChange={(event) => updateField(setForm, 'variations', event.target.value)} aria-label="Recipe variations" rows={4} className={`w-full rounded-3xl border bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:ring-4 ${hasFieldError(validationErrors, 'variations') ? 'border-red-400 focus:border-red-500 focus:ring-red-100' : 'border-slate-200 focus:border-sky-400 focus:ring-sky-100'}`} />
                {fieldErrorMessage(validationErrors, 'variations')}
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Storage info
                <textarea value={form.storageInfo} onChange={(event) => updateField(setForm, 'storageInfo', event.target.value)} aria-label="Recipe storage info" rows={4} className={`w-full rounded-3xl border bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:ring-4 ${hasFieldError(validationErrors, 'storageInfo') ? 'border-red-400 focus:border-red-500 focus:ring-red-100' : 'border-slate-200 focus:border-sky-400 focus:ring-sky-100'}`} />
                {fieldErrorMessage(validationErrors, 'storageInfo')}
              </label>
            </section>

            <section className="grid gap-4 lg:grid-cols-2">
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Source URL
                <Input hasError={hasFieldError(validationErrors, 'sourceUrl')} value={form.sourceUrl} onChange={(event) => updateField(setForm, 'sourceUrl', event.target.value)} aria-label="Recipe source URL" placeholder="https://example.com/recipe" />
                {fieldErrorMessage(validationErrors, 'sourceUrl')}
              </label>
              <label className="space-y-2 text-sm font-medium text-slate-700">
                Recipe image
                <input type="file" accept="image/jpeg,image/png,image/webp" aria-label="Recipe image file" onChange={(event) => setSelectedFile(event.target.files?.[0] ?? null)} className="w-full rounded-2xl border border-dashed border-slate-300 bg-white px-4 py-3 text-sm text-slate-700" />
                {isEditMode ? <p className="text-xs text-slate-500">Select a new photo here, then re-run AI extraction to replace the current image before saving.</p> : null}
              </label>
            </section>

            {displayedImageUrl ? (
              <section className="rounded-3xl bg-slate-50 p-5">
                <h4 className="text-sm font-semibold text-slate-900">Uploaded photo</h4>
                <p className="mt-1 text-xs text-slate-600">
                  {selectedFile
                    ? `Pending replacement: ${selectedFile.name}.`
                    : 'This image is attached to the current recipe draft.'}
                </p>
                {isEditMode ? (
                  <div className="mt-3 flex flex-wrap gap-3">
                    <Button type="button" onClick={() => void handleReRunImageExtraction()} disabled={isSaving}>
                      {selectedFile ? 'Replace photo and re-run AI extraction' : 'Re-run AI extraction from photo'}
                    </Button>
                    {selectedFile ? (
                      <Button type="button" variant="ghost" onClick={() => setSelectedFile(null)} disabled={isSaving}>
                        Keep current photo
                      </Button>
                    ) : null}
                  </div>
                ) : null}
                <img
                  src={displayedImageUrl}
                  alt="Uploaded recipe"
                  className="mt-3 max-h-72 w-full rounded-2xl object-contain bg-white p-2"
                  loading="lazy"
                />
                {imageImportWarnings.length > 0 ? (
                  <ul className="mt-3 space-y-1 text-sm text-amber-800">
                    {imageImportWarnings.map((warning) => (
                      <li key={warning}>{warning}</li>
                    ))}
                  </ul>
                ) : null}
                {imageImportRateLimited ? (
                  <div className="mt-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 space-y-2">
                    <p className="text-sm font-medium text-amber-900">AI Vision is temporarily rate limited</p>
                    <p className="text-xs text-amber-800">
                      OpenAI returned a rate limit error. You can retry using AWS Textract OCR instead — OCR reads raw text from the image and may need more manual cleanup than AI Vision, but it does not have rate limits.
                    </p>
                    <Button type="button" onClick={() => void handleOcrRetry(imageImportRateLimited)} disabled={isSaving}>
                      Retry with OCR (lower fidelity)
                    </Button>
                  </div>
                ) : null}
              </section>
            ) : null}

            <p className="text-xs text-slate-500">Fields marked with <span className="text-red-600">*</span> are required. Other fields are optional and can be added later.</p>

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

// FluentValidation emits PascalCase property names; camelCase form key → PascalCase lookup
function hasFieldError(errors: Record<string, string[]>, formKey: string): boolean {
  const pascal = formKey.charAt(0).toUpperCase() + formKey.slice(1);
  return pascal in errors || Object.keys(errors).some((k) => k.startsWith(`${pascal}[`));
}

function fieldErrorMessage(errors: Record<string, string[]>, formKey: string): React.ReactNode {
  const pascal = formKey.charAt(0).toUpperCase() + formKey.slice(1);
  const msgs = [
    ...(errors[pascal] ?? []),
    ...Object.entries(errors)
      .filter(([k]) => k.startsWith(`${pascal}[`))
      .flatMap(([, v]) => v)
  ];
  if (msgs.length === 0) return null;
  return <p className="text-xs text-red-600">{msgs[0]}</p>;
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
    proteinSource: recipe.proteinSource ?? [],
    cookingMethod: recipe.cookingMethod ?? [],
    difficulty: recipe.difficulty ?? '',
    tags: recipe.tags,
    ingredients: formatIngredients(recipe.ingredients),
    instructions: recipe.instructions.join('\n'),
    variations: recipe.variations ?? '',
    storageInfo: recipe.storageInfo ?? '',
    sourceType: recipe.sourceType,
    sourceUrl: recipe.sourceUrl ?? '',
    imageKey: recipe.imageKey ?? '',
    imageUrl: recipe.imageUrl ?? ''
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
    proteinSource: normalizeStringArray(form.proteinSource),
    cookingMethod: normalizeStringArray(form.cookingMethod),
    difficulty: form.difficulty.trim(),
    tags: normalizeStringArray(form.tags),
    ingredients: parseIngredients(form.ingredients),
    instructions: parseInstructions(form.instructions),
    imageKey: form.imageKey.trim() || undefined,
    sourceType: form.sourceType || 'manual',
    sourceUrl: form.sourceUrl.trim() || undefined,
    variations: form.variations.trim(),
    storageInfo: form.storageInfo.trim()
  };
}

function mapDraftToFormValues(draft: ImportedRecipeDraft): Partial<RecipeFormState> {
  const mapped = mapImportedValuesToOptions({
    cuisine: draft.cuisine,
    servings: draft.servings,
    prepTimeMinutes: draft.prepTimeMinutes,
    cookTimeMinutes: draft.cookTimeMinutes,
    proteinSource: draft.proteinSource,
    cookingMethod: draft.cookingMethod,
    difficulty: draft.difficulty
  });

  return {
    name: draft.name,
    description: draft.description ?? '',
    category: draft.category,
    cuisine: mapped.cuisine,
    servings: mapped.servings,
    prepTimeMinutes: mapped.prepTimeMinutes,
    cookTimeMinutes: mapped.cookTimeMinutes,
    proteinSource: mapped.proteinSource,
    cookingMethod: mapped.cookingMethod,
    difficulty: mapped.difficulty,
    tags: draft.tags,
    ingredients: formatIngredients(draft.ingredients),
    instructions: draft.instructions.join('\n'),
    sourceType: draft.sourceType,
    sourceUrl: draft.sourceType === 'url' ? draft.sourceUrl : ''
  };
}

function toUpdatePayloadFromDraft(draft: ImportedRecipeDraft): UpdateRecipePayload {
  const mapped = mapImportedValuesToOptions({
    cuisine: draft.cuisine,
    servings: draft.servings,
    prepTimeMinutes: draft.prepTimeMinutes,
    cookTimeMinutes: draft.cookTimeMinutes,
    proteinSource: draft.proteinSource,
    cookingMethod: draft.cookingMethod,
    difficulty: draft.difficulty
  });

  return {
    name: draft.name,
    description: draft.description,
    category: draft.category,
    cuisine: mapped.cuisine || undefined,
    servings: mapped.servings ? Number(mapped.servings) : undefined,
    prepTimeMinutes: mapped.prepTimeMinutes ? Number(mapped.prepTimeMinutes) : undefined,
    cookTimeMinutes: mapped.cookTimeMinutes ? Number(mapped.cookTimeMinutes) : undefined,
    proteinSource: mapped.proteinSource?.length ? normalizeStringArray(mapped.proteinSource) : undefined,
    cookingMethod: mapped.cookingMethod?.length ? normalizeStringArray(mapped.cookingMethod) : undefined,
    difficulty: mapped.difficulty || undefined,
    tags: normalizeStringArray(draft.tags),
    ingredients: draft.ingredients,
    instructions: draft.instructions,
    sourceType: draft.sourceType
  };
}

function normalizeStringArray(values: string[] | undefined): string[] {
  if (!values || values.length === 0) {
    return [];
  }

  return values
    .map((value) => value.trim())
    .filter((value) => value.length > 0);
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