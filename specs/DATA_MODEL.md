# Data Model Specification

## Overview

All data is stored in Amazon DynamoDB with on-demand pricing ($0 at idle). The application uses 6 tables with single-table design principles where appropriate. All tables use string partition keys (PK) and sort keys (SK) for flexible access patterns.

---

## Tables

### 1. Users Table

Stores user profiles with full dietary information migrated from markdown profiles.

**Table Name**: `thc-meal-planner-{env}-users`

| Attribute | Type | Description |
|-----------|------|-------------|
| PK | String | `USER#{cognitoSub}` |
| SK | String | `PROFILE` |
| name | String | Display name |
| email | String | Email address |
| familyId | String | Shared family identifier (e.g., `FAM#<family-id>`) |
| role | String | `head_of_household`, `member`, or `dependent` |
| dietaryPrefs | List\<String\> | e.g., `["vegetarian"]` |
| allergies | List\<Map\> | `[{allergen, severity, reaction, crossContamination}]` |
| excludedIngredients | List\<String\> | e.g., `["mushrooms", "peanuts"]` |
| macroTargets | Map | `{calories, protein, carbohydrates, fat, fiber, sodium}` (nullable per field) |
| cuisinePreferences | List\<String\> | e.g., `["Mediterranean", "Asian", "Mexican"]` |
| cookingConstraints | Map | `{maxWeekdayPrepMinutes, maxWeekendPrepMinutes, prefersBatchCooking, batchCookDay}` |
| flavorPreferences | Map | `{spiceLevel, prefersSavory, favoriteHerbs[]}` |
| defaultServings | Number | Default serving size |
| familyMembers | List\<Map\> | `[{name, age, preferences}]` (denormalized summary — each child also has a full `dependent` profile record) |
| doctorNotes | List\<String\> | Medical dietary notes |
| notificationPrefs | Map | `{mealPlanEmail, weeklyDigest, securityAlerts}` |
| createdAt | String | ISO 8601 |
| updatedAt | String | ISO 8601 |

**TTL**: None (permanent).

**Dependent Profiles**: Children (and future family members without login) are stored as Users table records with `role: "dependent"`. Their PK uses a generated ID (`USER#dep_{shortId}`) since they have no Cognito sub. The parent's `familyMembers` array is kept as a denormalized summary for quick access; the full dependent profile is the source of truth.

> **Future Enhancement**: When children gain their own app access, their records are promoted from `dependent` to `member` and linked to a Cognito account.

**Access Patterns**:

| Pattern | Key Condition |
|---------|---------------|
| Get user profile | PK = `USER#{sub}`, SK = `PROFILE` |
| List family members | GSI `FamilyIndex`: PK = `FAM#{familyId}` |
| List dependents only | GSI `FamilyIndex`: PK = `FAM#{familyId}`, filter `role = "dependent"` |

**GSI: FamilyIndex**:
- PK: `familyId`
- SK: `name`
- Projection: ALL

---

### 2. MealPlans Table

Stores weekly meal plans with recipe references and nutritional summaries.

**Table Name**: `thc-meal-planner-{env}-mealplans`

| Attribute | Type | Description |
|-----------|------|-------------|
| PK | String | `FAMILY#{familyId}` |
| SK | String | `PLAN#{weekStartDate}` (ISO date: `2026-01-20`) |
| status | String | `active` or `archived` |
| meals | List\<Map\> | See Meal object below |
| nutritionalSummary | Map | `{dailyAverages: {calories, protein, carbs, fat}}` |
| constraintsUsed | String | Constraint config version/reference |
| generatedBy | String | `ai` or `manual` |
| qualityScore | Map | `{overall, varietyScore, diversityScore, constraintViolations, grade}` |
| createdAt | String | ISO 8601 |
| updatedAt | String | ISO 8601 |
| TTL | Number | Unix epoch — 90 days after `weekStartDate` + 7 |

**Meal Object**:
```json
{
  "day": "Monday",
  "mealType": "dinner",
  "recipeId": "rec_abc123",
  "recipeName": "Veggie Stir Fry",
  "servings": 4,
  "assignedMembers": ["Adult 1", "Adult 2"],
  "prepTime": 10,
  "cookTime": 15,
  "nutritionalInfo": {
    "calories": 320,
    "protein": 15,
    "carbohydrates": 42,
    "fat": 12,
    "sodium": 680
  }
}
```

**Access Patterns**:

| Pattern | Key Condition |
|---------|--------------|
| Get plan for a specific week | PK = `FAMILY#{fam}`, SK = `PLAN#{date}` |
| Get active plan | GSI `StatusIndex`: PK = `FAMILY#{fam}`, SK begins_with `active#` |
| List all plans (history) | PK = `FAMILY#{fam}`, SK begins_with `PLAN#` |

**GSI: StatusIndex**:
- PK: `familyId` (projected from familyId attribute)
- SK: `status#createdAt` (composite)
- Projection: KEYS_ONLY

---

### 3. Recipes Table

The family cookbook. Stores all recipes permanently with full nutritional data, images, and source tracking.

**Table Name**: `thc-meal-planner-{env}-recipes`

| Attribute | Type | Description |
|-----------|------|-------------|
| PK | String | `RECIPE#{recipeId}` |
| SK | String | `METADATA` |
| name | String | Recipe name |
| description | String | Short description for recipe cards |
| category | String | `breakfast`, `lunch`, `dinner`, `snack` |
| cuisine | String | `Mexican`, `Mediterranean`, `Asian`, `American`, `Indian`, `Middle Eastern` |
| servings | Number | Default servings |
| prepTime | Number | Minutes |
| cookTime | Number | Minutes |
| totalTime | Number | Minutes (prepTime + cookTime) |
| proteinSource | String | `Chicken`, `Fish`, `Tofu`, `Eggs`, `Beans`, `Beef`, `Pork`, `None` |
| cookingMethod | String | `Grilled`, `Baked`, `Stir-fried`, `Pan-fried`, `No-cook`, `Steamed` |
| difficulty | String | `Easy`, `Medium`, `Hard` |
| tags | List\<String\> | e.g., `["vegetarian", "gluten-free", "kid-friendly", "freezer-friendly"]` |
| ingredients | List\<Map\> | `[{name, quantity, unit, section, notes}]` |
| instructions | List\<String\> | Ordered steps |
| nutritionalInfo | Map | Per-serving: `{calories, protein, carbohydrates, fat, fiber, sodium, sugar}` |
| dietaryInfo | Map | `{vegetarian, vegan, glutenFree, dairyFree, nutFree, lowSodium}` |
| variations | List\<String\> | Recipe variant descriptions |
| storage | Map | `{refrigerator, freezer}` |
| imageKey | String | S3 key for recipe image (nullable) |
| thumbnailKey | String | S3 key for thumbnail (nullable) |
| sourceUrl | String | Original URL if imported from web (nullable) |
| sourceType | String | `manual`, `url`, `image_upload` |
| familyId | String | Owning family |
| createdBy | String | User ID who created |
| createdAt | String | ISO 8601 |
| updatedAt | String | ISO 8601 |

**TTL**: None (permanent — this is the family cookbook).

**Access Patterns**:

| Pattern | Key Condition |
|---------|--------------|
| Get recipe by ID | PK = `RECIPE#{id}`, SK = `METADATA` |
| List all recipes | Scan with filter (acceptable for <500 recipes) |
| Browse by category | GSI `CategoryIndex`: PK = `category`, SK = `name` |
| Search by cuisine | GSI `CuisineIndex`: PK = `cuisine`, SK = `name` |

**GSI: CategoryIndex**:
- PK: `category`
- SK: `name`
- Projection: ALL

**GSI: CuisineIndex**:
- PK: `cuisine`
- SK: `name`
- Projection: ALL

---

### 4. Favorites Table

Per-user recipe favorites with personal notes and portion overrides.

**Table Name**: `thc-meal-planner-{env}-favorites`

| Attribute | Type | Description |
|-----------|------|-------------|
| PK | String | `USER#{userId}` |
| SK | String | `FAV#{recipeId}` |
| recipeId | String | Reference to recipe |
| recipeName | String | Denormalized for quick listing |
| recipeCategory | String | Denormalized for grouping |
| notes | String | Personal notes (e.g., "double the garlic") |
| portionOverride | Number | Preferred serving size |
| addedAt | String | ISO 8601 |

**TTL**: None (permanent).

**Access Patterns**:

| Pattern | Key Condition |
|---------|--------------|
| Get user's favorites | PK = `USER#{userId}`, SK begins_with `FAV#` |
| Check if recipe is favorited | PK = `USER#{userId}`, SK = `FAV#{recipeId}` |

---

### 5. GroceryLists Table

Living document grocery list with per-item completion tracking and optimistic concurrency.

**Table Name**: `thc-meal-planner-{env}-grocerylists`

| Attribute | Type | Description |
|-----------|------|-------------|
| PK | String | `FAMILY#{familyId}` |
| SK | String | `LIST#ACTIVE` |
| items | List\<Map\> | See GroceryItem object below |
| version | Number | Optimistic concurrency control |
| createdAt | String | ISO 8601 |
| updatedAt | String | ISO 8601 |

**TTL**: None on the list itself. The list is a permanent living document. Completed items carry individual TTL (see below).

**GroceryItem Object**:
```json
{
  "id": "item_001",
  "name": "firm tofu",
  "section": "protein",
  "quantity": 1,
  "unit": "block",
  "mealAssociations": [
    {
      "recipeId": "rec_abc123",
      "recipeName": "Veggie Stir Fry",
      "mealDay": "Monday"
    }
  ],
  "checkedOff": false,
  "checkedOffBy": null,
  "checkedOffByName": null,
  "checkedOffTimestamp": null,
  "completedTTL": null,
  "inStock": false
}
```

**Pantry / In-Stock Tracking**: Items with `inStock: true` are ingredients the family already has on hand (e.g., salt, pepper, olive oil). When generating a grocery list from a meal plan, the service checks the family's pantry staples and marks matching items as `inStock`. Users can also toggle `inStock` per-item. In-stock items remain on the list (for reference) but are visually distinguished and excluded from the "to buy" count.

**Item TTL Strategy**: When an item is checked off, `completedTTL` is set to current time + 7 days (Unix epoch). A scheduled cleanup process (or application-level filtering) uses this to remove stale completed items. DynamoDB TTL attribute is set on a projected copy if per-item expiry is needed, or application logic filters them.

> **Note**: Since DynamoDB TTL operates at the item (row) level and our grocery list is a single item with embedded items array, the 7-day completed item cleanup is handled in application code during reads — not via DynamoDB TTL. When fetching the list, the service filters out completed items older than 7 days and writes back the cleaned list.

**Optimistic Concurrency**: All write operations include a `version` field. The DynamoDB update uses a condition expression: `attribute_exists(version) AND version = :expectedVersion`. On conflict, returns 409 to the client.

**Access Patterns**:

| Pattern | Key Condition |
|---------|--------------|
| Get active list | PK = `FAMILY#{fam}`, SK = `LIST#ACTIVE` |
| Poll for changes | PK = `FAMILY#{fam}`, SK = `LIST#ACTIVE`, filter `updatedAt > :since` |
| Get pantry staples | PK = `FAMILY#{fam}`, SK = `PANTRY#STAPLES` |

**Pantry Staples Record**:

A separate item stores the family's persistent pantry staples — ingredients they always keep in stock:

```json
{
  "PK": "FAMILY#{familyId}",
  "SK": "PANTRY#STAPLES",
  "items": [
    { "name": "salt", "section": "spices" },
    { "name": "black pepper", "section": "spices" },
    { "name": "olive oil", "section": "pantry" },
    { "name": "garlic", "section": "produce" }
  ],
  "updatedAt": "2026-03-22T00:00:00Z"
}
```

During grocery list generation, ingredients matching pantry staples are auto-marked `inStock: true`.

**Grocery List Reactivity**: When a meal plan is modified (meal swapped, added, or removed), the grocery list must be recalculated. The `mealAssociations` on each item link ingredients to specific meals. On meal change, the service:
1. Removes items exclusively associated with the removed/replaced meal
2. Adds new items from the replacement recipe
3. Adjusts quantities for shared ingredients
4. Preserves manually-added items and checked-off states
5. Re-applies pantry staple matching (`inStock` flags)
6. Increments the list `version` to trigger polling updates

---

### 6. ChatHistory Table

Stores AI chat conversation history per user with 30-day TTL.

**Table Name**: `thc-meal-planner-{env}-chathistory`

| Attribute | Type | Description |
|-----------|------|-------------|
| PK | String | `USER#{userId}` |
| SK | String | `MSG#{timestamp}` (ISO 8601 with milliseconds for uniqueness) |
| conversationId | String | Groups messages into conversations |
| role | String | `user` or `assistant` |
| content | String | Message text (markdown) |
| actions | List\<Map\> | Any CRUD actions executed `[{type, status, result}]` |
| pendingConfirmation | Map | If a destructive action awaits confirmation |
| createdAt | String | ISO 8601 |
| TTL | Number | Unix epoch — 30 days after creation |

**Access Patterns**:

| Pattern | Key Condition |
|---------|--------------|
| Get user's chat history | PK = `USER#{userId}`, SK begins_with `MSG#`, ScanIndexForward=false |
| Get conversation messages | PK = `USER#{userId}`, SK begins_with `MSG#`, filter on `conversationId` |

---

## Store Sections

Used for grocery list grouping. Migrated from `constraints/store-preferences.md`:

| Section ID | Display Name | Store Mapping |
|-----------|--------------|---------------|
| `produce` | Produce | Primary store fresh section |
| `dairy` | Dairy & Eggs | Primary store dairy aisle |
| `protein` | Meat & Protein | Primary store meat counter / tofu section |
| `pantry` | Pantry Staples | Primary store center aisles |
| `frozen` | Frozen | Primary store frozen section |
| `bakery` | Bakery & Bread | Primary store bakery |
| `spices` | Spices & Seasonings | Primary store spice aisle |
| `beverages` | Beverages | Primary store beverage aisle |
| `household` | Household & Non-Food | Primary store household section |

---

## Seed Data

### Users (from existing profiles)

> **PII Note**: Real profile data (names, medical conditions, macro targets) is stored locally in `.local/profiles/` (gitignored). The seed data below uses generic labels. During migration, the bootstrap script reads from `.local/seed-data/Users.json` to populate DynamoDB with real values.

**Adult 1** (mapped from `.local/profiles/adult-1.md`):
- Dietary prefs: `["vegetarian"]`
- Allergies: `[]` (none severe)
- Ingredient exclusions: specific food preferences
- Macro targets: custom calorie/protein/carb/fat/fiber targets
- Cuisine preferences: multiple
- Cooking constraints: weekday prep limit, batch cooking on weekends
- Default servings: 2
- Doctor notes: nutritional supplementation focus

**Adult 2** (mapped from `.local/profiles/adult-2.md`):
- Dietary prefs: `["gluten-free", "low-sodium"]`
- Allergies: severe food allergy (anaphylaxis-level), precautionary allergen avoidance
- Ingredient exclusions: allergens + autoimmune-triggering grains
- Macro targets: custom calorie/protein/carb/fat/sodium targets
- Cuisine preferences: multiple
- Cooking constraints: shorter weekday prep limit, batch cooking on weekends
- Default servings: 4
- Family members: Child 1 (preschool, picky eater), Child 2 (elementary, adventurous)
- Doctor notes: medical dietary management, allergen avoidance, supplementation

**Child 1** (dependent — mapped from `.local/profiles/child-1.md`):
- Role: `dependent`
- Age group: preschool
- Dietary prefs: `[]` (no restrictions)
- Allergies: `[]` (none — inherits family-level allergen avoidance)
- Eating style: picky eater, prefers mild flavors
- Preferred foods: from local profile
- Macro targets: age-appropriate (from local profile)
- Notes: all meals must account for this family member's preferences when generating plans

**Child 2** (dependent — mapped from `.local/profiles/child-2.md`):
- Role: `dependent`
- Age group: elementary
- Dietary prefs: `[]` (no restrictions)
- Allergies: `[]` (none — inherits family-level allergen avoidance)
- Eating style: adventurous, loves vegetables
- Preferred foods: from local profile
- Macro targets: age-appropriate (from local profile)
- Notes: willing to try most things; helps drive recipe variety

### Recipes (6 from existing repo)

| Recipe | Category | Cuisine | Protein | Key Tags |
|--------|----------|---------|---------|----------|
| Breakfast Burritos | Breakfast | Mexican | Eggs | kid-friendly, freezer-friendly, meal-prep |
| Overnight Oats | Breakfast | American | Dairy | gluten-free, nut-free, no-cook |
| Grilled Chicken Salad | Lunch | American | Chicken | gluten-free, high-protein |
| Mediterranean Quinoa Bowl | Lunch | Mediterranean | Beans | vegan, high-fiber |
| Fish Tacos | Dinner | Mexican | Fish | gluten-free |
| Veggie Stir Fry | Dinner | Asian | Tofu | vegetarian, gluten-free, dairy-free |

---

## Capacity Planning

For a 2-adult household with 2 dependents:

| Table | Estimated Items | Reads/Month | Writes/Month |
|-------|----------------|-------------|--------------|
| Users | 4 (2 adults + 2 dependents) | ~500 | ~20 |
| MealPlans | ~50 (4/month × 12 + archived) | ~2,000 | ~50 |
| Recipes | 50-200 over time | ~5,000 | ~100 |
| Favorites | 20-50 | ~1,000 | ~50 |
| GroceryLists | 1 (living document) | ~30,000 (polling) | ~500 |
| ChatHistory | ~500/month (TTL cleans) | ~2,000 | ~500 |

All well within DynamoDB on-demand free tier (25 WCU / 25 RCU always-free).
