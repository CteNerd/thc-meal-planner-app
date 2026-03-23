export type DashboardCard = {
  title: string;
  body: string;
};

export type Allergy = {
  allergen: string;
  severity: string;
  reaction?: string;
  crossContamination?: boolean;
};

export type MacroTargets = {
  calories?: number;
  protein?: number;
  carbohydrates?: number;
  fat?: number;
  fiber?: number;
  sodium?: number;
};

export type FamilyMember = {
  name: string;
  age?: number;
  preferences?: string;
};

export type UserProfile = {
  userId: string;
  name: string;
  email: string;
  familyId: string;
  role: string;
  dietaryPrefs: string[];
  allergies: Allergy[];
  excludedIngredients: string[];
  macroTargets?: MacroTargets;
  cuisinePreferences: string[];
  defaultServings?: number;
  familyMembers: FamilyMember[];
  doctorNotes: string[];
  createdAt: string;
  updatedAt: string;
};

export type UpdateProfilePayload = {
  name?: string;
  email?: string;
  dietaryPrefs?: string[];
  allergies?: Allergy[];
  excludedIngredients?: string[];
  macroTargets?: MacroTargets;
  cuisinePreferences?: string[];
  defaultServings?: number;
  familyMembers?: FamilyMember[];
  doctorNotes?: string[];
};

export type DependentProfile = {
  userId: string;
  name: string;
  familyId: string;
  role: string;
  ageGroup?: string;
  dietaryPrefs: string[];
  allergies: Allergy[];
  eatingStyle?: string;
  preferredFoods: string[];
  avoidedFoods: string[];
  macroTargets?: MacroTargets;
  notes?: string;
  createdAt: string;
  updatedAt: string;
};

export type CreateDependentPayload = {
  name: string;
  ageGroup?: string;
  dietaryPrefs?: string[];
  allergies?: Allergy[];
  eatingStyle?: string;
  preferredFoods?: string[];
  avoidedFoods?: string[];
  macroTargets?: MacroTargets;
  notes?: string;
};

export type UpdateDependentPayload = Partial<CreateDependentPayload>;

export type ApiProblemDetails = {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
};
