import { Card } from '../components/ui/Card';
import { useEffect, useState } from 'react';
import type { DependentProfile, UserProfile } from '../types';
import { createDependent, deleteDependent, getProfile, listDependents, updateProfile } from '../services/profileApi';
import { Input } from '../components/ui/Input';
import { Button } from '../components/ui/Button';

export function ProfilePage() {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [dependents, setDependents] = useState<DependentProfile[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [nameDraft, setNameDraft] = useState('');
  const [newDependentName, setNewDependentName] = useState('');
  const [newDependentAgeGroup, setNewDependentAgeGroup] = useState('');

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
      } catch {
        if (!active) {
          return;
        }

        setError('Unable to load profile data.');
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
    } catch {
      setError('Unable to save profile changes.');
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
    } catch {
      setError('Unable to create dependent.');
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
    } catch {
      setError('Unable to delete dependent.');
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
              <ul className="space-y-2">
                {dependents.map((dependent) => (
                  <li key={dependent.userId} className="flex items-center justify-between rounded-xl border border-slate-200 bg-white px-3 py-2">
                    <div className="text-sm text-slate-700">
                      <p className="font-medium">{dependent.name}</p>
                      <p className="text-xs text-slate-500">{dependent.ageGroup ?? 'No age group specified'}</p>
                    </div>
                    <Button variant="ghost" onClick={() => void handleDeleteDependent(dependent.userId)} disabled={isSaving}>Remove</Button>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      )}
    </Card>
  );
}