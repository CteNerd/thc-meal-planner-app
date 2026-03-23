import type {
  CreateDependentPayload,
  DependentProfile,
  UpdateDependentPayload,
  UpdateProfilePayload,
  UserProfile
} from '../types';
import { apiDelete, apiGet, apiPost, apiPut } from './api';

export async function getProfile(): Promise<UserProfile> {
  return await apiGet<UserProfile>('/profile');
}

export async function updateProfile(payload: UpdateProfilePayload): Promise<UserProfile> {
  return await apiPut<UserProfile, UpdateProfilePayload>('/profile', payload);
}

export async function listDependents(): Promise<DependentProfile[]> {
  return await apiGet<DependentProfile[]>('/family/dependents');
}

export async function createDependent(payload: CreateDependentPayload): Promise<DependentProfile> {
  return await apiPost<DependentProfile, CreateDependentPayload>('/family/dependents', payload);
}

export async function updateDependent(userId: string, payload: UpdateDependentPayload): Promise<DependentProfile> {
  return await apiPut<DependentProfile, UpdateDependentPayload>(`/family/dependents/${userId}`, payload);
}

export async function deleteDependent(userId: string): Promise<void> {
  await apiDelete(`/family/dependents/${userId}`);
}
