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

export type RecipeIngredient = {
  name: string;
  quantity?: string;
  unit?: string;
  section?: string;
  notes?: string;
};

export type RecipeNutrition = {
  calories?: number;
  protein?: number;
  carbohydrates?: number;
  fat?: number;
  fiber?: number;
  sodium?: number;
  sugar?: number;
};

export type Recipe = {
  recipeId: string;
  familyId: string;
  name: string;
  description?: string;
  category: string;
  cuisine?: string;
  servings?: number;
  prepTimeMinutes?: number;
  cookTimeMinutes?: number;
  proteinSource?: string[];
  cookingMethod?: string[];
  difficulty?: string;
  tags: string[];
  ingredients: RecipeIngredient[];
  instructions: string[];
  nutrition?: RecipeNutrition;
  imageKey?: string;
  thumbnailKey?: string;
  sourceType: string;
  sourceUrl?: string;
  variations?: string;
  storageInfo?: string;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
};

export type RecipePayload = {
  name: string;
  description?: string;
  category: string;
  cuisine?: string;
  servings?: number;
  prepTimeMinutes?: number;
  cookTimeMinutes?: number;
  proteinSource?: string[];
  cookingMethod?: string[];
  difficulty?: string;
  tags: string[];
  ingredients: RecipeIngredient[];
  instructions: string[];
  nutrition?: RecipeNutrition;
  imageKey?: string;
  thumbnailKey?: string;
  sourceType?: string;
  sourceUrl?: string;
  variations?: string;
  storageInfo?: string;
};

export type UpdateRecipePayload = Partial<RecipePayload>;

export type ImportedRecipeDraft = RecipePayload & {
  sourceType: string;
  sourceUrl: string;
  warnings: string[];
};

export type ImportRecipeFromUrlPayload = {
  url: string;
};

export type CreateRecipeUploadUrlPayload = {
  fileName: string;
  contentType: string;
};

export type RecipeUploadUrlResponse = {
  uploadUrl: string;
  imageKey: string;
  imageUrl: string;
};

export type MealNutritionalInfo = {
  calories?: number;
  protein?: number;
  carbohydrates?: number;
  fat?: number;
  sodium?: number;
};

export type QualityScore = {
  overall: number;
  varietyScore: number;
  diversityScore: number;
  constraintViolations: number;
  grade: string;
};

export type DailyAverages = {
  calories?: number;
  protein?: number;
  carbohydrates?: number;
  fat?: number;
};

export type NutritionalSummary = {
  dailyAverages?: DailyAverages;
};

export type MealSlot = {
  day: string;
  mealType: string;
  recipeId: string;
  recipeName: string;
  servings?: number;
  prepTime?: number;
  cookTime?: number;
  nutritionalInfo?: MealNutritionalInfo;
};

export type MealPlan = {
  familyId: string;
  weekStartDate: string;
  status: string;
  meals: MealSlot[];
  nutritionalSummary?: NutritionalSummary;
  constraintsUsed: string;
  generatedBy: string;
  qualityScore?: QualityScore;
  createdAt: string;
  updatedAt: string;
};

export type CreateMealSlotPayload = {
  day: string;
  mealType: string;
  recipeId: string;
  servings?: number;
};

export type CreateMealPlanPayload = {
  weekStartDate: string;
  meals: CreateMealSlotPayload[];
};

export type UpdateMealPlanPayload = {
  meals?: CreateMealSlotPayload[];
  status?: string;
};

export type GenerateMealPlanPayload = {
  weekStartDate: string;
  prompt?: string;
  replaceExisting?: boolean;
};

export type MealSwapSuggestion = {
  recipeId: string;
  recipeName: string;
  prepTime?: number;
  cookTime?: number;
  constraintSafe: boolean;
  score: number;
  notes: string[];
};

export type FavoriteRecipe = {
  userId: string;
  recipeId: string;
  recipeName: string;
  recipeCategory: string;
  notes?: string;
  portionOverride?: number;
  addedAt: string;
};

export type FavoriteRecipePayload = {
  notes?: string;
  portionOverride?: number;
};

export type ApiProblemDetails = {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
};
