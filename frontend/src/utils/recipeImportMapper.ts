import {
  CUISINE_OPTIONS,
  COOKING_METHOD_OPTIONS,
  COOK_MINUTES_OPTIONS,
  DIFFICULTY_OPTIONS,
  PREP_MINUTES_OPTIONS,
  PROTEIN_SOURCE_OPTIONS
} from '../constants/recipeOptions';
  import {
    CUISINE_OPTIONS,
    COOKING_METHOD_OPTIONS,
    COOK_MINUTES_OPTIONS,
    DIFFICULTY_OPTIONS,
    PREP_MINUTES_OPTIONS,
    PROTEIN_SOURCE_OPTIONS,
    SERVINGS_OPTIONS
  } from '../constants/recipeOptions';

/**
 * Find the closest match in a list of options.
 * Uses fuzzy matching (includes check) and returns the best match.
 */
function findClosestMatch(value: string | undefined, options: string[]): string | undefined {
  if (!value || !value.trim()) return undefined;

  const normalized = value.toLowerCase().trim();

  // Exact match (case-insensitive)
  const exact = options.find((opt) => opt.toLowerCase() === normalized);
  if (exact) return exact;

  // Substring match
  const substring = options.find((opt) => opt.toLowerCase().includes(normalized) || normalized.includes(opt.toLowerCase()));
  if (substring) return substring;

  return undefined;
}

/**
 * Round a numeric value to the nearest option in a list of numeric strings.
 */
function roundToNearestOption(value: number | undefined, options: string[]): string | undefined {
  if (value === undefined) return undefined;

  const numOptions = options.map(Number).filter(Number.isFinite);
  if (numOptions.length === 0) return undefined;

  const closest = numOptions.reduce((prev, curr) => (Math.abs(curr - value) < Math.abs(prev - value) ? curr : prev));
  return String(closest);
}

/**
 * Map imported recipe draft values to predefined dropdown options.
 * Handles:
 * - Rounding numeric times to nearest increment
 * - Fuzzy matching for list fields
 * - Preserving unmapped values as free-text
 */
export function mapImportedValuesToOptions(imported: {
  cuisine?: string;
  servings?: number;
  prepTimeMinutes?: number;
  cookTimeMinutes?: number;
  proteinSource?: string[];
  cookingMethod?: string[];
  difficulty?: string;
}): {
  cuisine: string;
  servings: string;
  prepTimeMinutes: string;
  cookTimeMinutes: string;
  proteinSource: string[];
  cookingMethod: string[];
  difficulty: string;
} {
  // Cuisine: try to match, fall back to value
  let mappedCuisine = findClosestMatch(imported.cuisine, CUISINE_OPTIONS);
  if (!mappedCuisine && imported.cuisine) {
    mappedCuisine = imported.cuisine;
  }

  // Servings: round to nearest option, fall back to original
  let mappedServings = roundToNearestOption(imported.servings, SERVINGS_OPTIONS);
  if (!mappedServings && imported.servings) {
    mappedServings = String(imported.servings);
  }

  // Prep minutes: round to nearest 5-minute increment
  let mappedPrepMinutes = roundToNearestOption(imported.prepTimeMinutes, PREP_MINUTES_OPTIONS);
  if (!mappedPrepMinutes && imported.prepTimeMinutes !== undefined) {
    mappedPrepMinutes = String(imported.prepTimeMinutes);
  }

  // Cook minutes: round to nearest 5-minute increment
  let mappedCookMinutes = roundToNearestOption(imported.cookTimeMinutes, COOK_MINUTES_OPTIONS);
  if (!mappedCookMinutes && imported.cookTimeMinutes !== undefined) {
    mappedCookMinutes = String(imported.cookTimeMinutes);
  }

  // Difficulty: try to match
  let mappedDifficulty = findClosestMatch(imported.difficulty, DIFFICULTY_OPTIONS);
  if (!mappedDifficulty && imported.difficulty) {
    mappedDifficulty = imported.difficulty;
  }

  // Protein sources: try to match each, preserve unmapped
  const mappedProteinSources = (imported.proteinSource ?? []).map((ps) => {
    const matched = findClosestMatch(ps, PROTEIN_SOURCE_OPTIONS);
    return matched || ps;
  });

  // Cooking methods: try to match each, preserve unmapped
  const mappedCookingMethods = (imported.cookingMethod ?? []).map((cm) => {
    const matched = findClosestMatch(cm, COOKING_METHOD_OPTIONS);
    return matched || cm;
  });

  return {
    cuisine: mappedCuisine || '',
    servings: mappedServings || '',
    prepTimeMinutes: mappedPrepMinutes || '',
    cookTimeMinutes: mappedCookMinutes || '',
    proteinSource: mappedProteinSources,
    cookingMethod: mappedCookingMethods,
    difficulty: mappedDifficulty || ''
  };
}

