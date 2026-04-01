import { Card } from '../components/ui/Card';
import { useEffect, useState } from 'react';
import type { DependentProfile, UserProfile } from '../types';
import { createDependent, deleteDependent, getProfile, listDependents, updateDependent, updateProfile } from '../services/profileApi';
import { Input } from '../components/ui/Input';
import { Button } from '../components/ui/Button';
import { getApiErrorMessage } from '../services/api';

type PrimaryProfileEditDraft = {
  name: string;
  dietaryPrefs: string;
  allergens: string;
  excludedIngredients: string;
  cuisinePreferences: string;
  defaultServings: string;
  doctorNotes: string;
  macroCalories: string;
  macroProtein: string;
  macroCarbs: string;
  macroFat: string;
  macroFiber: string;
  macroSodium: string;
};

type DependentEditDraft = {
  name: string;
  ageGroup: string;
  eatingStyle: string;
  dietaryPrefs: string;
  allergens: string;
  preferredFoods: string;
  avoidedFoods: string;
  notes: string;
};

const EMPTY_PRIMARY_DRAFT: PrimaryProfileEditDraft = {
  name: '', dietaryPrefs: '', allergens: '', excludedIngredients: '',
  cuisinePreferences: '', defaultServings: '', doctorNotes: '',
  macroCalories: '', macroProtein: '', macroCarbs: '', macroFat: '', macroFiber: '', macroSodium: ''
};

const EMPTY_DEPENDENT_DRAFT: DependentEditDraft = {
  name: '', ageGroup: '', eatingStyle: '', dietaryPrefs: '',
  allergens: '', preferredFoods: '', avoidedFoods: '', notes: ''
};

export function ProfilePage() {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [dependents, setDependents] = useState<DependentProfile[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [isEditingProfile, setIsEditingProfile] = useState(false);
  const [primaryProfileDraft, setPrimaryProfileDraft] = useState<PrimaryProfileEditDraft>(EMPTY_PRIMARY_DRAFT);
  const [newDependentName, setNewDependentName] = useState('');
  const [newDependentAgeGroup, setNewDependentAgeGroup] = useState('');
  const [editingDependentId, setEditingDependentId] = useState<string | null>(null);
  const [dependentEditDraft, setDependentEditDraft] = useState<DependentEditDraft>(EMPTY_DEPENDENT_DRAFT);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setIsLoading(true);
        const [profileResponse, dependentsResponse] = await Promise.all([getProfile(), listDependents()]);

        if (!active) {
          return;
        }

        setProfile(profileResponse);
        setDependents(dependentsResponse);
      } catch (error) {
        if (!active) {
          return;
        }

        setError(getApiErrorMessage(error, 'Unable to load profile data.'));
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

  function handleBeginEditProfile() {
    if (!profile) return;
    setPrimaryProfileDraft({
      name: profile.name,
      dietaryPrefs: (profile.dietaryPrefs ?? []).join(', '),
      allergens: (profile.allergies ?? []).map((a) => a.allergen).join(', '),
      excludedIngredients: (profile.excludedIngredients ?? []).join(', '),
      cuisinePreferences: (profile.cuisinePreferences ?? []).join(', '),
      defaultServings: profile.defaultServings?.toString() ?? '',
      doctorNotes: (profile.doctorNotes ?? []).join(', '),
      macroCalories: profile.macroTargets?.calories?.toString() ?? '',
      macroProtein: profile.macroTargets?.protein?.toString() ?? '',
      macroCarbs: profile.macroTargets?.carbohydrates?.toString() ?? '',
      macroFat: profile.macroTargets?.fat?.toString() ?? '',
      macroFiber: profile.macroTargets?.fiber?.toString() ?? '',
      macroSodium: profile.macroTargets?.sodium?.toString() ?? ''
    });
    setIsEditingProfile(true);
  }

  function handleCancelEditProfile() {
    setPrimaryProfileDraft(EMPTY_PRIMARY_DRAFT);
    setIsEditingProfile(false);
  }

  async function handleSaveProfile() {
    if (!profile) return;
    const splitList = (v: string) => v.split(',').map((s) => s.trim()).filter(Boolean);
    const parseNum = (v: string): number | undefined => {
      const n = parseInt(v, 10);
      return isNaN(n) ? undefined : n;
    };

    try {
      setIsSaving(true);
      setError(null);
      const updated = await updateProfile({
        name: primaryProfileDraft.name.trim() || undefined,
        dietaryPrefs: splitList(primaryProfileDraft.dietaryPrefs),
        allergies: splitList(primaryProfileDraft.allergens).map((a) => ({ allergen: a, severity: 'unknown' })),
        excludedIngredients: splitList(primaryProfileDraft.excludedIngredients),
        cuisinePreferences: splitList(primaryProfileDraft.cuisinePreferences),
        defaultServings: parseNum(primaryProfileDraft.defaultServings),
        doctorNotes: splitList(primaryProfileDraft.doctorNotes),
        macroTargets: {
          calories: parseNum(primaryProfileDraft.macroCalories),
          protein: parseNum(primaryProfileDraft.macroProtein),
          carbohydrates: parseNum(primaryProfileDraft.macroCarbs),
          fat: parseNum(primaryProfileDraft.macroFat),
          fiber: parseNum(primaryProfileDraft.macroFiber),
          sodium: parseNum(primaryProfileDraft.macroSodium)
        }
      });
      setProfile(updated);
      setIsEditingProfile(false);
      setPrimaryProfileDraft(EMPTY_PRIMARY_DRAFT);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to save profile changes.'));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleCreateDependent() {
    if (!newDependentName.trim()) {
      setError('Dependent name is required.');
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      const created = await createDependent({
        name: newDependentName.trim(),
        ageGroup: newDependentAgeGroup.trim() || undefined
      });
      setDependents((current) => [...current, created]);
      setNewDependentName('');
      setNewDependentAgeGroup('');
    } catch (error) {
      setError(getApiErrorMessage(error, 'Unable to create dependent.'));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDeleteDependent(userId: string) {
    try {
      setIsSaving(true);
      setError(null);
      await deleteDependent(userId);
      setDependents((current) => current.filter((dependent) => dependent.userId !== userId));
    } catch (error) {
      setError(getApiErrorMessage(error, 'Unable to delete dependent.'));
    } finally {
      setIsSaving(false);
    }
  }

  function handleBeginEditDependent(dep: DependentProfile) {
    setEditingDependentId(dep.userId);
    setDependentEditDraft({
      name: dep.name,
      ageGroup: dep.ageGroup ?? '',
      eatingStyle: dep.eatingStyle ?? '',
      dietaryPrefs: (dep.dietaryPrefs ?? []).join(', '),
      allergens: (dep.allergies ?? []).map((a) => a.allergen).join(', '),
      preferredFoods: (dep.preferredFoods ?? []).join(', '),
      avoidedFoods: (dep.avoidedFoods ?? []).join(', '),
      notes: dep.notes ?? ''
    });
  }

  function handleCancelEditDependent() {
    setEditingDependentId(null);
    setDependentEditDraft(EMPTY_DEPENDENT_DRAFT);
  }

  async function handleSaveEditDependent(userId: string) {
    const splitList = (v: string) => v.split(',').map((s) => s.trim()).filter(Boolean);
    try {
      setIsSaving(true);
      setError(null);
      const updated = await updateDependent(userId, {
        name: dependentEditDraft.name.trim() || undefined,
        ageGroup: dependentEditDraft.ageGroup.trim() || undefined,
        eatingStyle: dependentEditDraft.eatingStyle.trim() || undefined,
        dietaryPrefs: splitList(dependentEditDraft.dietaryPrefs),
        allergies: splitList(dependentEditDraft.allergens).map((a) => ({ allergen: a, severity: 'unknown' })),
        preferredFoods: splitList(dependentEditDraft.preferredFoods),
        avoidedFoods: splitList(dependentEditDraft.avoidedFoods),
        notes: dependentEditDraft.notes.trim() || undefined
      });
      setDependents((current) => current.map((d) => d.userId === userId ? updated : d));
      setEditingDependentId(null);
      setDependentEditDraft(EMPTY_DEPENDENT_DRAFT);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Unable to save dependent changes.'));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Card>
      <h2 className="text-2xl font-semibold text-slate-900">Profile</h2>
      {isLoading ? (
        <p className="mt-3 text-sm text-slate-600">Loading profile...</p>
      ) : error ? (
        <div className="mt-3 space-y-3">
          <p className="text-sm text-red-700">{error}</p>
          <p className="text-xs text-slate-600">You can retry by saving again or refreshing this page.</p>
        </div>
      ) : (
        <div className="mt-4 space-y-6">
          <section className="space-y-3">
            <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Primary Profile</h3>
            {isEditingProfile ? (
              <div className="rounded-xl border border-sky-200 bg-sky-50 p-4 space-y-3">
                <p className="text-sm font-semibold text-sky-800">Editing your profile</p>
                <div className="grid gap-2 sm:grid-cols-2">
                  <div>
                    <label className="text-xs font-medium text-slate-600">Name <span className="text-red-500">*</span></label>
                    <Input value={primaryProfileDraft.name} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, name: e.target.value }))} aria-label="Name" />
                  </div>
                  <div>
                    <label className="text-xs font-medium text-slate-600">Default servings</label>
                    <Input value={primaryProfileDraft.defaultServings} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, defaultServings: e.target.value }))} placeholder="e.g. 2" type="number" aria-label="Default servings" />
                  </div>
                  <div>
                    <label className="text-xs font-medium text-slate-600">Dietary prefs <span className="text-slate-400">(comma-separated)</span></label>
                    <Input value={primaryProfileDraft.dietaryPrefs} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, dietaryPrefs: e.target.value }))} placeholder="e.g. vegetarian, gluten-free" aria-label="Dietary prefs" />
                  </div>
                  <div>
                    <label className="text-xs font-medium text-slate-600">Allergies <span className="text-slate-400">(allergen names, comma-separated)</span></label>
                    <Input value={primaryProfileDraft.allergens} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, allergens: e.target.value }))} placeholder="e.g. peanuts, dairy" aria-label="Allergies" />
                  </div>
                  <div>
                    <label className="text-xs font-medium text-slate-600">Excluded ingredients <span className="text-slate-400">(comma-separated)</span></label>
                    <Input value={primaryProfileDraft.excludedIngredients} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, excludedIngredients: e.target.value }))} placeholder="e.g. mushrooms, onions" aria-label="Excluded ingredients" />
                  </div>
                  <div>
                    <label className="text-xs font-medium text-slate-600">Cuisine preferences <span className="text-slate-400">(comma-separated)</span></label>
                    <Input value={primaryProfileDraft.cuisinePreferences} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, cuisinePreferences: e.target.value }))} placeholder="e.g. Italian, Asian" aria-label="Cuisine preferences" />
                  </div>
                  <div className="sm:col-span-2">
                    <label className="text-xs font-medium text-slate-600">Doctor notes <span className="text-slate-400">(comma-separated)</span></label>
                    <Input value={primaryProfileDraft.doctorNotes} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, doctorNotes: e.target.value }))} placeholder="Any medical or nutritional notes" aria-label="Doctor notes" />
                  </div>
                  <div className="sm:col-span-2">
                    <p className="text-xs font-semibold text-slate-700 mb-2">Macro Targets (daily)</p>
                    <div className="grid gap-2 sm:grid-cols-2">
                      <div>
                        <label className="text-xs font-medium text-slate-600">Calories</label>
                        <Input value={primaryProfileDraft.macroCalories} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, macroCalories: e.target.value }))} placeholder="e.g. 2200" type="number" aria-label="Macro calories" />
                      </div>
                      <div>
                        <label className="text-xs font-medium text-slate-600">Protein (g)</label>
                        <Input value={primaryProfileDraft.macroProtein} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, macroProtein: e.target.value }))} placeholder="e.g. 80" type="number" aria-label="Macro protein" />
                      </div>
                      <div>
                        <label className="text-xs font-medium text-slate-600">Carbs (g)</label>
                        <Input value={primaryProfileDraft.macroCarbs} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, macroCarbs: e.target.value }))} placeholder="e.g. 275" type="number" aria-label="Macro carbs" />
                      </div>
                      <div>
                        <label className="text-xs font-medium text-slate-600">Fat (g)</label>
                        <Input value={primaryProfileDraft.macroFat} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, macroFat: e.target.value }))} placeholder="e.g. 73" type="number" aria-label="Macro fat" />
                      </div>
                      <div>
                        <label className="text-xs font-medium text-slate-600">Fiber (g)</label>
                        <Input value={primaryProfileDraft.macroFiber} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, macroFiber: e.target.value }))} placeholder="e.g. 30" type="number" aria-label="Macro fiber" />
                      </div>
                      <div>
                        <label className="text-xs font-medium text-slate-600">Sodium (mg)</label>
                        <Input value={primaryProfileDraft.macroSodium} onChange={(e) => setPrimaryProfileDraft((d) => ({ ...d, macroSodium: e.target.value }))} placeholder="e.g. 2300" type="number" aria-label="Macro sodium" />
                      </div>
                    </div>
                  </div>
                </div>
                <div className="flex gap-2">
                  <Button onClick={handleSaveProfile} disabled={isSaving || !primaryProfileDraft.name.trim()}>Save</Button>
                  <Button variant="ghost" onClick={handleCancelEditProfile} disabled={isSaving}>Cancel</Button>
                </div>
              </div>
            ) : (
              <div className="space-y-3">
                <div className="rounded-xl border border-slate-200 bg-white px-4 py-3 space-y-2">
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex-1">
                      <p className="text-sm font-semibold text-slate-900">{profile?.name}</p>
                      <p className="text-xs text-slate-500">Signed in as {profile?.email ?? 'unknown email'}</p>
                      {profile && profile.dietaryPrefs.length > 0 && (
                        <p className="text-xs text-slate-600 mt-1">Dietary: {profile.dietaryPrefs.join(', ')}</p>
                      )}
                      {profile && profile.allergies.length > 0 && (
                        <p className="text-xs text-red-600 mt-1">Allergies: {profile.allergies.map((a) => a.allergen).join(', ')}</p>
                      )}
                      {profile && profile.excludedIngredients.length > 0 && (
                        <p className="text-xs text-amber-700 mt-1">Excludes: {profile.excludedIngredients.join(', ')}</p>
                      )}
                    </div>
                    <Button variant="ghost" onClick={handleBeginEditProfile} disabled={isSaving}>Edit</Button>
                  </div>
                </div>
              </div>
            )}
          </section>

          <section className="space-y-3">
            <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Dependents</h3>
            <div className="grid gap-3 sm:grid-cols-[1fr_1fr_auto]">
              <Input
                value={newDependentName}
                onChange={(event) => setNewDependentName(event.target.value)}
                placeholder="Dependent name"
                aria-label="Dependent name"
              />
              <Input
                value={newDependentAgeGroup}
                onChange={(event) => setNewDependentAgeGroup(event.target.value)}
                placeholder="Age group"
                aria-label="Dependent age group"
              />
              <Button onClick={handleCreateDependent} disabled={isSaving}>Add</Button>
            </div>

            {dependents.length === 0 ? (
              <p className="text-sm text-slate-600">No dependent profiles yet.</p>
            ) : (
              <ul className="space-y-3">
                {dependents.map((dependent) =>
                  editingDependentId === dependent.userId ? (
                    <li key={dependent.userId} className="rounded-xl border border-sky-200 bg-sky-50 p-4 space-y-3">
                      <p className="text-sm font-semibold text-sky-800">Editing {dependent.name}</p>
                      <div className="grid gap-2 sm:grid-cols-2">
                        <div>
                          <label className="text-xs font-medium text-slate-600">Name <span className="text-red-500">*</span></label>
                          <Input value={dependentEditDraft.name} onChange={(e) => setDependentEditDraft((d) => ({ ...d, name: e.target.value }))} aria-label="Name" />
                        </div>
                        <div>
                          <label className="text-xs font-medium text-slate-600">Age group</label>
                          <Input value={dependentEditDraft.ageGroup} onChange={(e) => setDependentEditDraft((d) => ({ ...d, ageGroup: e.target.value }))} placeholder="e.g. 8-12" aria-label="Age group" />
                        </div>
                        <div>
                          <label className="text-xs font-medium text-slate-600">Eating style</label>
                          <Input value={dependentEditDraft.eatingStyle} onChange={(e) => setDependentEditDraft((d) => ({ ...d, eatingStyle: e.target.value }))} placeholder="e.g. picky, adventurous" aria-label="Eating style" />
                        </div>
                        <div>
                          <label className="text-xs font-medium text-slate-600">Dietary prefs <span className="text-slate-400">(comma-separated)</span></label>
                          <Input value={dependentEditDraft.dietaryPrefs} onChange={(e) => setDependentEditDraft((d) => ({ ...d, dietaryPrefs: e.target.value }))} placeholder="e.g. vegetarian, gluten-free" aria-label="Dietary prefs" />
                        </div>
                        <div>
                          <label className="text-xs font-medium text-slate-600">Allergies <span className="text-slate-400">(allergen names, comma-separated)</span></label>
                          <Input value={dependentEditDraft.allergens} onChange={(e) => setDependentEditDraft((d) => ({ ...d, allergens: e.target.value }))} placeholder="e.g. peanuts, dairy" aria-label="Allergies" />
                        </div>
                        <div>
                          <label className="text-xs font-medium text-slate-600">Preferred foods <span className="text-slate-400">(comma-separated)</span></label>
                          <Input value={dependentEditDraft.preferredFoods} onChange={(e) => setDependentEditDraft((d) => ({ ...d, preferredFoods: e.target.value }))} placeholder="e.g. chicken, rice" aria-label="Preferred foods" />
                        </div>
                        <div>
                          <label className="text-xs font-medium text-slate-600">Avoided foods <span className="text-slate-400">(comma-separated)</span></label>
                          <Input value={dependentEditDraft.avoidedFoods} onChange={(e) => setDependentEditDraft((d) => ({ ...d, avoidedFoods: e.target.value }))} placeholder="e.g. mushrooms, onions" aria-label="Avoided foods" />
                        </div>
                        <div className="sm:col-span-2">
                          <label className="text-xs font-medium text-slate-600">Notes</label>
                          <Input value={dependentEditDraft.notes} onChange={(e) => setDependentEditDraft((d) => ({ ...d, notes: e.target.value }))} placeholder="Any other relevant notes" aria-label="Notes" />
                        </div>
                      </div>
                      <div className="flex gap-2">
                        <Button onClick={() => void handleSaveEditDependent(dependent.userId)} disabled={isSaving || !dependentEditDraft.name.trim()}>Save</Button>
                        <Button variant="ghost" onClick={handleCancelEditDependent} disabled={isSaving}>Cancel</Button>
                      </div>
                    </li>
                  ) : (
                    <li key={dependent.userId} className="rounded-xl border border-slate-200 bg-white px-3 py-2">
                      <div className="flex items-start justify-between gap-2">
                        <div className="text-sm text-slate-700 space-y-0.5">
                          <p className="font-medium">{dependent.name}</p>
                          <p className="text-xs text-slate-500">{dependent.ageGroup ?? 'No age group'}{dependent.eatingStyle ? ` · ${dependent.eatingStyle}` : ''}</p>
                          {dependent.allergies.length > 0 && (
                            <p className="text-xs text-red-600">Allergies: {dependent.allergies.map((a) => a.allergen).join(', ')}</p>
                          )}
                          {dependent.avoidedFoods.length > 0 && (
                            <p className="text-xs text-amber-700">Avoids: {dependent.avoidedFoods.join(', ')}</p>
                          )}
                          {dependent.preferredFoods.length > 0 && (
                            <p className="text-xs text-green-700">Prefers: {dependent.preferredFoods.join(', ')}</p>
                          )}
                        </div>
                        <div className="flex gap-1 shrink-0">
                          <Button variant="ghost" onClick={() => handleBeginEditDependent(dependent)} disabled={isSaving}>Edit</Button>
                          <Button variant="ghost" onClick={() => void handleDeleteDependent(dependent.userId)} disabled={isSaving}>Remove</Button>
                        </div>
                      </div>
                    </li>
                  )
                )}
              </ul>
            )}
          </section>
        </div>
      )}
    </Card>
  );
}