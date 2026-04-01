import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { ProfilePage } from './ProfilePage';
import type { DependentProfile, UserProfile } from '../types';
import { ApiError } from '../services/api';
import {
  createDependent,
  deleteDependent,
  getProfile,
  listDependents,
  updateProfile
} from '../services/profileApi';

vi.mock('../services/profileApi', () => ({
  getProfile: vi.fn(),
  updateProfile: vi.fn(),
  listDependents: vi.fn(),
  createDependent: vi.fn(),
  deleteDependent: vi.fn()
}));

const mockedGetProfile = vi.mocked(getProfile);
const mockedUpdateProfile = vi.mocked(updateProfile);
const mockedListDependents = vi.mocked(listDependents);
const mockedCreateDependent = vi.mocked(createDependent);
const mockedDeleteDependent = vi.mocked(deleteDependent);

function buildProfile(overrides?: Partial<UserProfile>): UserProfile {
  return {
    userId: 'test-user-123',
    name: 'Adult 1',
    email: 'adult1@example.com',
    familyId: 'FAM#test-family',
    role: 'head_of_household',
    dietaryPrefs: [],
    allergies: [],
    excludedIngredients: [],
    cuisinePreferences: [],
    familyMembers: [],
    doctorNotes: [],
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    ...overrides
  };
}

function buildDependent(overrides?: Partial<DependentProfile>): DependentProfile {
  return {
    userId: 'dep_abc123',
    name: 'Child 1',
    familyId: 'FAM#test-family',
    role: 'dependent',
    dietaryPrefs: [],
    allergies: [],
    preferredFoods: [],
    avoidedFoods: [],
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    ...overrides
  };
}

describe('ProfilePage', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mockedDeleteDependent.mockResolvedValue();
    mockedUpdateProfile.mockResolvedValue(buildProfile());
  });

  it('loads and displays profile and dependents', async () => {
    mockedGetProfile.mockResolvedValue(buildProfile());
    mockedListDependents.mockResolvedValue([buildDependent()]);

    render(<ProfilePage />);

    expect(await screen.findByText(/signed in as adult1@example.com/i)).toBeInTheDocument();
    expect(screen.getByText('Child 1')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /remove/i })).toBeInTheDocument();
  });

  it('shows api detail when initial load fails', async () => {
    mockedGetProfile.mockRejectedValue(
      new ApiError(403, 'Forbidden', {
        title: 'Forbidden',
        detail: 'This action requires head_of_household role.'
      })
    );
    mockedListDependents.mockResolvedValue([]);

    render(<ProfilePage />);

    expect(await screen.findByText('This action requires head_of_household role.')).toBeInTheDocument();
  });

  it('shows local validation error when dependent name is empty', async () => {
    mockedGetProfile.mockResolvedValue(buildProfile());
    mockedListDependents.mockResolvedValue([]);

    render(<ProfilePage />);

    await screen.findByText(/signed in as adult1@example.com/i);
    fireEvent.click(screen.getByRole('button', { name: /^add$/i }));

    expect(screen.getByText('Dependent name is required.')).toBeInTheDocument();
    expect(mockedCreateDependent).not.toHaveBeenCalled();
  });

  it('shows api detail when creating dependent fails', async () => {
    mockedGetProfile.mockResolvedValue(buildProfile());
    mockedListDependents.mockResolvedValue([]);
    mockedCreateDependent.mockRejectedValue(
      new ApiError(400, 'Bad Request', {
        title: 'Validation Failed',
        detail: 'Name must not be empty.'
      })
    );

    render(<ProfilePage />);

    await screen.findByText(/signed in as adult1@example.com/i);

    fireEvent.change(screen.getByLabelText('Dependent name'), { target: { value: '   Child 2   ' } });
    fireEvent.click(screen.getByRole('button', { name: /^add$/i }));

    await waitFor(() => {
      expect(screen.getByText('Name must not be empty.')).toBeInTheDocument();
    });

    expect(mockedCreateDependent).toHaveBeenCalledTimes(1);
  });

  it('saves updated profile name and reflects updated value', async () => {
    mockedGetProfile.mockResolvedValue(buildProfile({ name: 'Adult 1', dietaryPrefs: ['vegetarian'] }));
    mockedListDependents.mockResolvedValue([]);
    mockedUpdateProfile.mockResolvedValue(buildProfile({ name: 'Updated Name', dietaryPrefs: ['vegetarian'] }));

    render(<ProfilePage />);

    await screen.findByText(/signed in as adult1@example.com/i);

    // Click Edit button to enter edit mode
    fireEvent.click(screen.getByRole('button', { name: /^edit$/i }));

    // Update the name field in the edit form
    const nameInput = screen.getByLabelText('Name');
    fireEvent.change(nameInput, { target: { value: 'Updated Name' } });

    // Save the changes
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }));

    await waitFor(() => {
      expect(mockedUpdateProfile).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'Updated Name'
        })
      );
    });

    expect(screen.getByText('Updated Name')).toBeInTheDocument();
  });

  it('adds dependent on successful create and clears form fields', async () => {
    mockedGetProfile.mockResolvedValue(buildProfile());
    mockedListDependents.mockResolvedValue([]);
    mockedCreateDependent.mockResolvedValue(buildDependent({ userId: 'dep_new', name: 'Child 2', ageGroup: 'toddler' }));

    render(<ProfilePage />);

    await screen.findByText(/signed in as adult1@example.com/i);

    fireEvent.change(screen.getByLabelText('Dependent name'), { target: { value: 'Child 2' } });
    fireEvent.change(screen.getByLabelText('Dependent age group'), { target: { value: 'toddler' } });
    fireEvent.click(screen.getByRole('button', { name: /^add$/i }));

    await waitFor(() => {
      expect(screen.getByText('Child 2')).toBeInTheDocument();
    });

    expect(screen.getByLabelText('Dependent name')).toHaveValue('');
    expect(screen.getByLabelText('Dependent age group')).toHaveValue('');
  });

  it('shows api detail when deleting dependent fails', async () => {
    mockedGetProfile.mockResolvedValue(buildProfile());
    mockedListDependents.mockResolvedValue([buildDependent({ userId: 'dep_fail', name: 'Child Fail' })]);
    mockedDeleteDependent.mockRejectedValue(
      new ApiError(404, 'Not Found', {
        title: 'Dependent not found',
        detail: 'No dependent exists for the requested user id within this family.'
      })
    );

    render(<ProfilePage />);

    await screen.findByText('Child Fail');
    fireEvent.click(screen.getByRole('button', { name: /remove/i }));

    await waitFor(() => {
      expect(screen.getByText('No dependent exists for the requested user id within this family.')).toBeInTheDocument();
    });
  });
});
