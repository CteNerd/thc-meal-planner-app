import { Card } from '../components/ui/Card';
import { useEffect, useState } from 'react';
import type { DependentProfile, UserProfile } from '../types';
import { createDependent, deleteDependent, getProfile, listDependents, updateDependent, updateProfile } from '../services/profileApi';
import { Input } from '../components/ui/Input';
import { Button } from '../components/ui/Button';
import { getApiErrorMessage } from '../services/api';

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

const EMPTY_DRAFT: DependentEditDraft = {
  name: '', ageGroup: '', eatingStyle: '', dietaryPrefs: '',
  allergens: '', preferredFoods: '', avoidedFoods: '', notes: ''
};

export function ProfilePage() {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [dependents, setDependents] = useState<DependentProfile[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [nameDraft, setNameDraft] = useState('');
  const [newDependentName, setNewDependentName] = useState('');
  const [newDependentAgeGroup, setNewDependentAgeGroup] = useState('');
  const [editingDependentId, setEditingDependentId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<DependentEditDraft>(EMPTY_DRAFT);

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
        setNameDraft(profileResponse.name);
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

  async function handleProfileSave() {
    if (!profile) {
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      const updated = await updateProfile({
        name: nameDraft
      });
      setProfile(updated);
      setNameDraft(updated.name);
    } catch (error) {
      setError(getApiErrorMessage(error, 'Unable to save profile changes.'));
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
    setEditDraft({
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
    setEditDraft(EMPTY_DRAFT);
  }

  async function handleSaveEditDependent(userId: string) {
    const splitList = (v: string) => v.split(',').map((s) => s.trim()).filter(Boolean);
    try {
      setIsSaving(true);
      setError(null);
      const updated = await updateDependent(userId, {
        name: editDraft.name.trim() || undefined,
        ageGroup: editDraft.ageGroup.trim() || undefined,
        eatingStyle: editDraft.eatingStyle.trim() || undefined,
        dietaryPrefs: splitList(editDraft.dietaryPrefs),
        allergies: splitList(editDraft.allergens).map((a) => ({ allergen: a, severity: 'unknown' })),
        preferredFoods: splitList(editDraft.preferredFoods),
        avoidedFoods: splitList(editDraft.avoidedFoods),
        notes: editDraft.notes.trim() || undefined
      });
      setDependents((current) => current.map((d) => d.userId === userId ? updated : d));
      setEditingDependentId(null);
      setEditDraft(EMPTY_DRAFT);
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
            <div className="grid gap-3 sm:grid-cols-[1fr_auto]">
              <Input value={nameDraft} onChange={(event) => setNameDraft(event.target.value)} aria-label="Profile name" />
              <Button onClick={handleProfileSave} disabled={isSaving || !profile}>Save</Button>
            </div>
            <p className="text-xs text-slate-500">Signed in as {profile?.email ?? 'unknown email'}</p>
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
                          <Input value={editDraft.name} onChange={(e) => setEditDraft((d) => ({ ...d, name: e.target.value }))} aria-label="Name" />
                        </div>
                        <div>
                          <label className="text-xs font-medium text-slate-600">Age group</label>
                          <Input value={editDraft.ageGroup} onChange={(e) => setEditDraft((d) => ({ ...d, ageGroup: e.target.value }))} placeholder="e.g. 8-12" aria-label="Age group" />
                        </div>
                        <div>
                          <label className="text-xs font-medium text-slate-600">Eating style</label>
                          <Input value={editDraft.eatingStyle} onChange={(e) => setEditDraft((d) => ({ ...d, eatingStyle: e.target.value }))} placeholder="e.g. picky, adventurous" aria-label="Eating style" />
                        </div>
                        <div>
                          <label className="text-xs font-medium text-slate-600">Dietary prefs <span className="text-slate-400">(comma-separated)</span></label>
                          <Input value={editDraft.dietaryPrefs} onChange={(e) => setEditDraft((d) => ({ ...d, dietaryPrefs: e.target.value }))} placeholder="e.g. vegetarian, gluten-free" aria-label="Dietary prefs" />
                        </div>
                        <div>
                          <label className="text-xs font-medium text-slate-600">Allergies <span className="text-slate-400">(allergen names, comma-separated)</span></label>
                          <Input value={editDraft.allergens} onChange={(e) => setEditDraft((d) => ({ ...d, allergens: e.target.value }))} placeholder="e.g. peanuts, dairy" aria-label="Allergies" />
                        </div>
                        <div>
                          <label className="text-xs font-medium text-slate-600">Preferred foods <span className="text-slate-400">(comma-separated)</span></label>
                          <Input value={editDraft.preferredFoods} onChange={(e) => setEditDraft((d) => ({ ...d, preferredFoods: e.target.value }))} placeholder="e.g. chicken, rice" aria-label="Preferred foods" />
                        </div>
                        <div>
                          <label className="text-xs font-medium text-slate-600">Avoided foods <span className="text-slate-400">(comma-separated)</span></label>
                          <Input value={editDraft.avoidedFoods} onChange={(e) => setEditDraft((d) => ({ ...d, avoidedFoods: e.target.value }))} placeholder="e.g. mushrooms, onions" aria-label="Avoided foods" />
                        </div>
                        <div className="sm:col-span-2">
                          <label className="text-xs font-medium text-slate-600">Notes</label>
                          <Input value={editDraft.notes} onChange={(e) => setEditDraft((d) => ({ ...d, notes: e.target.value }))} placeholder="Any other relevant notes" aria-label="Notes" />
                        </div>
                      </div>
                      <div className="flex gap-2">
                        <Button onClick={() => void handleSaveEditDependent(dependent.userId)} disabled={isSaving || !editDraft.name.trim()}>Save</Button>
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