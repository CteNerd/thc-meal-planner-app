# Migration Specification

## Overview

Migrate existing data into DynamoDB tables for the new web application. Migration is a one-time operation run after initial infrastructure deployment.

> **Important**: This new repo does **not** have access to the old `thc-meal-prep-planner` repository. All source data needed for migration is stored locally in `.local/seed-data/` (gitignored) and referenced by generic labels in these specs. The migration script reads from `.local/seed-data/*.json` files — not from external markdown files or embedded PII.

> **Verification Required**: All profile data, recipes, and constraints below are DRAFT. During implementation, agents must present each data record to the user for confirmation before writing to code or database. The user has identified known issues that will be corrected at implementation time.

---

## Source Data Origin

The data below was extracted from the following files in the legacy `thc-meal-prep-planner` repo (no longer accessible from this repo):

| Original Source File | Target | Status |
|---------------------|--------|--------|
| `.local/profiles/adult-1.md` | Users table | Stored locally — **verify with user** |
| `.local/profiles/adult-2.md` | Users table | Stored locally — **verify with user** |
| `recipes/*.md` (6 files) | Recipes table | Embedded below — **verify with user** |
| `constraints/enhanced_constraints.yaml` | C# ConstraintEngine config | Embedded below |
| `constraints/store-preferences.md` | App constants | Embedded below |
| `history/history_2026-01-20.json` | MealPlans table | Archived plan, low priority |
| `plans/grocery_list_2026-01-20.md` | GroceryLists table | Optional, low priority |

---

## Migration Approach

Since the old repo is not accessible, migration uses **seed data from `.local/seed-data/`** (gitignored, populated from `.local/profiles/`). The migration is implemented as a C# console app that writes the verified JSON records directly to DynamoDB.

### Project Structure

```
src/
└── ThcMealPlanner.Migration/
    ├── Program.cs                    # Entry point, orchestrates all migrations
    ├── SeedData/                     # Copied from .local/seed-data/ before running
    │   ├── Users.json                # Verified profile JSON
    │   ├── Recipes.json              # Verified recipe JSON
    │   └── Constraints.json          # Verified constraint config
    ├── Migrators/
    │   ├── ProfileMigrator.cs        # Reads Users.json → Users table
    │   ├── RecipeMigrator.cs         # Reads Recipes.json → Recipes table
    │   └── ConstraintMigrator.cs     # Validates constraint config loads
    └── ThcMealPlanner.Migration.csproj
```

> **Note**: `SeedData/` is gitignored. Before running migration, copy files from `.local/seed-data/` into `src/ThcMealPlanner.Migration/SeedData/`.

### Execution

```bash
# Run migration against dev environment
dotnet run --project src/ThcMealPlanner.Migration -- \
  --table-prefix thc-meal-planner-dev \
  --region us-east-1 \
  --dry-run  # Preview changes without writing

# Execute for real
dotnet run --project src/ThcMealPlanner.Migration -- \
  --table-prefix thc-meal-planner-dev \
  --region us-east-1
```

---

## Profile Migration

> **⚠ VERIFY WITH USER**: The profile data below is a draft extracted from the legacy repo. Agents must present this data to the user and get confirmation before writing to DynamoDB or seed files.

### Adult 1 (DRAFT — verify before committing)

> Profile data is stored in `.local/profiles/adult-1.md`. The JSON below is a **template** showing the DynamoDB record structure. Real values come from `.local/seed-data/Users.json` at migration time.

**Source notes** (from `.local/profiles/adult-1.md`):
- Vegetarian, no severe allergies
- Ingredient exclusions and macro targets defined in local profile
- Cooking constraints from legacy `enhanced_constraints.yaml`

```json
{
  "PK": "USER#{cognitoSub}",
  "SK": "PROFILE",
  "name": "<from .local/profiles/adult-1.md>",
  "email": "<from .local/profiles/adult-1.md>",
  "familyId": "FAM#<family-id>",
  "role": "head_of_household",
  "dietaryPrefs": ["vegetarian"],
  "allergies": [],
  "excludedIngredients": ["<from local profile>"],
  "macroTargets": {
    "calories": "<from local profile>",
    "protein": "<from local profile>",
    "carbohydrates": "<from local profile>",
    "fat": "<from local profile>",
    "fiber": "<from local profile>",
    "sodium": null
  },
  "cuisinePreferences": ["<from local profile>"],
  "cookingConstraints": {
    "maxWeekdayPrepMinutes": "<from local profile>",
    "maxWeekendPrepMinutes": "<from local profile>",
    "prefersBatchCooking": true,
    "batchCookDay": "Sunday"
  },
  "flavorPreferences": {
    "spiceLevel": "<from local profile>",
    "prefersSavory": true,
    "favoriteHerbs": ["<from local profile>"]
  },
  "defaultServings": 2,
  "familyMembers": [],
  "doctorNotes": ["<from local profile>"],
  "notificationPrefs": {
    "mealPlanEmail": true,
    "weeklyDigest": true,
    "securityAlerts": true
  },
  "createdAt": "2026-01-20T00:00:00Z",
  "updatedAt": "2026-01-20T00:00:00Z"
}
```

### Adult 2 (DRAFT — verify before committing)

> Profile data is stored in `.local/profiles/adult-2.md`. The JSON below is a **template** showing the DynamoDB record structure. Real values come from `.local/seed-data/Users.json` at migration time.

**Source notes** (from `.local/profiles/adult-2.md`):
- Severe food allergy with anaphylaxis risk
- Autoimmune dietary restriction → ingredient exclusions
- Medical dietary limit on sodium
- Family members: Child 1 (preschool), Child 2 (elementary)

```json
{
  "PK": "USER#{cognitoSub}",
  "SK": "PROFILE",
  "name": "<from .local/profiles/adult-2.md>",
  "email": "<from .local/profiles/adult-2.md>",
  "familyId": "FAM#<family-id>",
  "role": "head_of_household",
  "dietaryPrefs": ["gluten-free", "low-sodium"],
  "allergies": [
    {
      "allergen": "<from local profile>",
      "severity": "severe",
      "reaction": "anaphylaxis",
      "crossContamination": true
    },
    {
      "allergen": "<from local profile>",
      "severity": "precautionary",
      "reaction": null,
      "crossContamination": true
    }
  ],
  "excludedIngredients": ["<from local profile — allergens + autoimmune triggers>"],
  "macroTargets": {
    "calories": "<from local profile>",
    "protein": "<from local profile>",
    "carbohydrates": "<from local profile>",
    "fat": "<from local profile>",
    "fiber": null,
    "sodium": "<from local profile — medical limit>"
  },
  "cuisinePreferences": ["<from local profile>"],
  "cookingConstraints": {
    "maxWeekdayPrepMinutes": "<from local profile>",
    "maxWeekendPrepMinutes": "<from local profile>",
    "prefersBatchCooking": true,
    "batchCookDay": "Sunday"
  },
  "flavorPreferences": {
    "spiceLevel": "<from local profile>",
    "prefersSavory": true,
    "favoriteHerbs": ["<from local profile>"]
  },
  "defaultServings": 4,
  "familyMembers": [
    { "name": "<Child 1 name>", "age": "<Child 1 age>", "preferences": "<from local profile>" },
    { "name": "<Child 2 name>", "age": "<Child 2 age>", "preferences": "<from local profile>" }
  ],
  "doctorNotes": ["<from local profile — medical dietary management notes>"],
  "notificationPrefs": {
    "mealPlanEmail": true,
    "weeklyDigest": true,
    "securityAlerts": true
  },
  "createdAt": "2026-01-20T00:00:00Z",
  "updatedAt": "2026-01-20T00:00:00Z"
}
```

> **Note**: `cognitoSub` values are assigned during user provisioning in Cognito. Migration script takes a mapping file or prompts for the Cognito sub IDs.

---

## Recipe Migration

> **⚠ VERIFY WITH USER**: The recipe categorizations below (cuisine, proteinSource, cookingMethod) are inferred and may need correction. Verify each recipe with the user.

The 6 recipes from the legacy repo are embedded as seed data. The original markdown files are no longer accessible — the data below is the authoritative source.

### Recipe Summary (DRAFT — verify before committing)

### Inferred Fields

These categorizations were inferred from the legacy recipe content and may need correction:

| Recipe | cuisine | proteinSource | cookingMethod | category |
|--------|---------|--------------|---------------|----------|
| Breakfast Burritos | Mexican | Eggs | Pan-fried | breakfast |
| Overnight Oats | American | Dairy | No-cook | breakfast |
| Grilled Chicken Salad | American | Chicken | Grilled | lunch |
| Mediterranean Quinoa Bowl | Mediterranean | Beans | Steamed | lunch |
| Fish Tacos | Mexican | Fish | Pan-fried | dinner |
| Veggie Stir Fry | Asian | Tofu | Stir-fried | dinner |

> Full recipe details (ingredients, instructions, nutrition) are available in [DATA_MODEL.md](DATA_MODEL.md) § Seed Data. During implementation, agents should present each recipe to the user for verification before writing to seed files.

### Recipe ID Generation

Recipe IDs are generated as: `rec_{shortHash}` where `shortHash` is the first 8 characters of SHA-256 of the recipe name (lowercase, trimmed).

---

## Constraint Migration

Constraints are NOT stored in DynamoDB. They are embedded as C# configuration. The values below were extracted from the legacy `enhanced_constraints.yaml` (no longer accessible):

### Constraint Values (embedded — verify with user)

| YAML Field | C# Configuration |
|-----------|-----------------|
| `no_cook_nights: [Wednesday]` | `ConstraintOptions.NoCookNights` |
| `max_repeat_days: 7` | `ConstraintOptions.MaxRepeatDays` |
| `blocked_proteins`, `blocked_cuisines`, `blocked_methods` | `ConstraintOptions.ProteinBlocking`, etc. |
| `weekly_budget: 150` | `ConstraintOptions.WeeklyBudget` |
| `weeknight_max_time: 45` | Already in user profile `cookingConstraints` |
| `weekend_max_time: 180` | Already in user profile `cookingConstraints` |

```json
// appsettings.json
{
  "ConstraintEngine": {
    "NoCookNights": ["Wednesday"],
    "MaxRepeatDays": 7,
    "MinCuisineVariety": 3,
    "MaxConsecutiveSameCuisine": 2,
    "WeeklyBudget": 150.00,
    "ProteinBlocking": {
      "MaxConsecutiveDays": 2
    },
    "QualityScoring": {
      "WeightVariety": 0.3,
      "WeightNutrition": 0.3,
      "WeightConstraints": 0.25,
      "WeightPreferences": 0.15
    }
  }
}
```

---

## Store Preferences Migration

From `constraints/store-preferences.md` → application constants in `IngredientAggregationService`:

```csharp
public static class StoreConfig
{
    // Store names come from .local/profiles/ — these are placeholders
    public static readonly StorePreference[] Stores =
    [
        new("<PrimaryStore>", StoreFrequency.Weekly, DayOfWeek.Saturday, isPrimary: true),
        new("<BulkStore>", StoreFrequency.BiWeekly, null, isPrimary: false),
        new("<SupplementaryStore>", StoreFrequency.AsNeeded, null, isPrimary: false),
    ];

    public static readonly Dictionary<string, string> SectionMapping = new()
    {
        ["produce"] = "Produce",
        ["dairy"] = "Dairy & Eggs",
        ["protein"] = "Meat & Protein",
        ["pantry"] = "Pantry Staples",
        ["frozen"] = "Frozen",
        ["bakery"] = "Bakery & Bread",
        ["spices"] = "Spices & Seasonings",
    };
}
```

---

## History & Plan Migration (Optional)

> The legacy history data (one partial meal plan scored -965/F) has minimal value for the new system. Consider starting fresh rather than migrating low-quality historical data. Discuss with user during implementation.

If migrating, the data would be stored as an archived MealPlan:

```json
{
  "PK": "FAMILY#<family-id>",
  "SK": "PLAN#2026-01-20",
  "status": "archived",
  "meals": [],
  "qualityScore": {
    "overall": -965,
    "grade": "F"
  },
  "generatedBy": "ai",
  "constraintsUsed": "v1-markdown",
  "createdAt": "2026-01-20T00:00:00Z",
  "updatedAt": "2026-01-20T00:00:00Z",
  "TTL": 1753056000
}
```

### Grocery List

Similarly optional. The legacy grocery list was generated from a single recipe and has no long-term value.

---

## Migration Validation

After migration, the script runs validation checks:

| Check | Expected |
|-------|----------|
| Users table count | 2 records |
| Recipes table count | 6 records |
| All recipes have `cuisine` field | Yes |
| All recipes have `proteinSource` field | Yes |
| All recipes have `cookingMethod` field | Yes |
| Adult 1 has NO severe allergies | Verify `allergies` is empty |
| Adult 2 has severe food allergy | Verify severity = "severe" |
| Family members ages correct | Verify from `.local/profiles/adult-2.md` |
| Constraint config loads without error | Yes |

---

## Rollback

Since this is a one-time migration to empty tables:
- **Rollback strategy**: Delete all items from tables and re-run migration
- **No production data at risk** — tables are empty before first migration
- Migration script is idempotent (uses PutItem, which overwrites)
