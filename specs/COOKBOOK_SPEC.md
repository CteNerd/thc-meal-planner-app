# Cookbook Specification

## Overview

The Cookbook is the family's recipe library. Users can add recipes manually, import from URLs, upload photos for AI-powered parsing, and organize with favorites. All recipes are stored permanently in DynamoDB with optional images in S3.

---

## Features

### 1. Manual Recipe Creation

Users create recipes via a form with complete nutritional and categorical data.

**Required Fields**: name, category, ingredients (at least 1), instructions (at least 1)

**Optional Fields**: cuisine, servings, prepTime, cookTime, proteinSource, cookingMethod, difficulty, tags, description, variations, storage info, image

**Form Sections**:
1. **Basics**: name, description, category (dropdown), cuisine (dropdown), difficulty (radio)
2. **Timing**: prep minutes, cook minutes (auto-calculates total)
3. **Servings & Protein**: default servings, protein source (dropdown), cooking method (dropdown)
4. **Ingredients**: dynamic list — name, quantity, unit, section, notes per ingredient
5. **Instructions**: ordered list with drag-to-reorder
6. **Tags**: tag input with autocomplete from existing tags
7. **Nutrition** (optional): per-serving calories, protein, carbs, fat, fiber, sodium, sugar
8. **Image**: drag-and-drop upload zone (see Image Upload below)
9. **Variations & Storage**: free-text areas

### 2. Import from URL

Paste a recipe URL → backend fetches and parses into structured recipe format using OpenAI.

**Flow**:
```
User pastes URL
     │
POST /api/recipes/import-from-url
     { "url": "https://example.com/recipe" }
     │
Backend:
  1. Validate URL (allowlist: HTTP/HTTPS, block internal IPs)
  2. Fetch page content (HttpClient, 10s timeout, 1MB limit)
  3. Extract text content (strip HTML)
  4. Send to OpenAI: "Parse this into a recipe JSON"
  5. Return parsed recipe for user review
     │
User reviews & edits parsed recipe
     │
User saves → POST /api/recipes (standard create)
```

**SSRF Protection**:
- Only HTTP/HTTPS schemes allowed
- Block: `localhost`, `127.0.0.1`, `169.254.x.x`, `10.x.x.x`, `172.16-31.x.x`, `192.168.x.x`, `[::1]`
- DNS resolution validated before fetch
- 10-second timeout, 1MB response limit
- User-Agent header set to identify as recipe importer

**OpenAI Parsing Prompt**:
```
Parse the following recipe text into a structured JSON object with these fields:
name, description, category (breakfast/lunch/dinner/snack), cuisine, servings,
prepTime (minutes), cookTime (minutes), proteinSource, cookingMethod, difficulty,
tags (array), ingredients (array of {name, quantity, unit, section}),
instructions (array of strings), nutritionalInfo ({calories, protein,
carbohydrates, fat, fiber, sodium} per serving if available).

If a field cannot be determined, use null.

Recipe text:
{extractedText}
```

### 3. Upload Recipe from Image

Upload a photo of a recipe (from a cookbook, handwritten card, screenshot) → AI extracts recipe data.

**Flow**:
```
User uploads image
     │
POST /api/recipes/from-image
     Content-Type: multipart/form-data
     { "image": <file> }
     │
Backend:
  1. Validate file type (JPEG, PNG, WebP only) and size (< 5MB)
  2. Upload to S3 temp location
  3. Send image URL to OpenAI Vision API (gpt-4o)
  4. Parse response into recipe structure
  5. Return parsed recipe for user review
  6. Delete temp image if user doesn't save
     │
User reviews & edits → saves to cookbook
```

> **Note**: Image-to-recipe uses `gpt-4o` (not mini) because vision capabilities are needed. This is a per-use cost (~$0.01–0.03 per image parse).

### 4. Recipe Image Upload

For the recipe's display image (shown on cards and detail pages):

**Flow**:
```
Frontend requests pre-signed URL
     │
POST /api/recipes/{id}/upload-url
     { "fileName": "photo.jpg", "contentType": "image/jpeg" }
     │
Backend generates pre-signed S3 PUT URL (15 min expiry)
     { "uploadUrl": "https://s3...", "imageKey": "recipes/rec_abc/uuid.jpg" }
     │
Frontend uploads directly to S3 via PUT
     │
Frontend confirms upload
     │
PATCH /api/recipes/{id}
     { "imageKey": "recipes/rec_abc/uuid.jpg" }
```

**Image Processing**:
- Original stored at full resolution
- Thumbnail generated on upload via Lambda@Edge or application-level resize (future: could use CloudFront Functions)
- Served via CloudFront `/images/*` path with caching

**Constraints**:
- Max file size: 5 MB
- Accepted types: `image/jpeg`, `image/png`, `image/webp`
- S3 key format: `recipes/{recipeId}/{uuid}.{ext}`

### 5. Favorites System

Per-user recipe favorites with personal customization.

**Features**:
- Heart icon toggle on recipe cards and detail pages
- Personal notes per favorite (e.g., "double the garlic", "kids love this one")
- Portion override (e.g., always make 6 servings instead of default 4)
- Favorites list page with filtering by category
- Favorited recipes get priority in AI meal plan suggestions

**API Endpoints**:
- `POST /api/recipes/{id}/favorite` — add to favorites
- `DELETE /api/recipes/{id}/favorite` — remove from favorites
- `GET /api/recipes/favorites` — list user's favorites

**Data Model** (Favorites table):
```json
{
  "PK": "USER#{userId}",
  "SK": "FAV#{recipeId}",
  "recipeId": "rec_abc123",
  "recipeName": "Veggie Stir Fry",
  "recipeCategory": "dinner",
  "notes": "Add extra sriracha for Adult 1's portion",
  "portionOverride": 4,
  "addedAt": "2026-01-25T10:30:00Z"
}
```

---

## Recipe Dietary Safety

Before serving any recipe in meal plans or chatbot suggestions, the system validates compatibility:

```csharp
public class RecipeSafetyValidator
{
    public ValidationResult ValidateForUser(Recipe recipe, UserProfile user)
    {
        var violations = new List<string>();

        // Check allergy ingredients
        foreach (var allergy in user.Allergies)
        {
            if (recipe.ContainsAllergen(allergy.Allergen))
            {
                violations.Add(
                    $"Contains {allergy.Allergen} " +
                    $"(severity: {allergy.Severity})");
            }
        }

        // Check excluded ingredients
        foreach (var excluded in user.ExcludedIngredients)
        {
            if (recipe.ContainsIngredient(excluded))
            {
                violations.Add($"Contains excluded: {excluded}");
            }
        }

        // Check dietary compatibility
        if (user.DietaryPrefs.Contains("vegetarian") &&
            !recipe.DietaryInfo.Vegetarian)
        {
            violations.Add("Not vegetarian");
        }

        if (user.DietaryPrefs.Contains("gluten-free") &&
            !recipe.DietaryInfo.GlutenFree)
        {
            violations.Add("Contains gluten");
        }

        return new ValidationResult(violations);
    }
}
```

---

## Search & Filtering

### Browse Filters

| Filter | Type | Options |
|--------|------|---------|
| Search | Text input | Searches name, description, ingredients |
| Category | Dropdown | breakfast, lunch, dinner, snack |
| Cuisine | Dropdown | All cuisines in cookbook |
| Tags | Multi-select | vegetarian, gluten-free, kid-friendly, etc. |
| Max Time | Slider | 0–180 minutes |
| Favorites Only | Toggle | Show only favorited recipes |
| Safe For | Dropdown | Adult 1, Adult 2, Everyone |

### Sort Options
- Name (A–Z)
- Newest first
- Shortest cook time
- Most favorited (for family use, this is a simple count of 1 or 2)

---

## Recipe Card Component

```
┌──────────────────────┐
│  [Recipe Image]       │
│  or placeholder       │
├──────────────────────┤
│  Recipe Name          │
│  ⏱ 25 min  🔥 320cal │
│  [vegetarian] [GF]   │
│                  ♡/♥  │
└──────────────────────┘
```

- Image: recipe photo or category-based placeholder
- Name: truncated to 2 lines
- Time: total time (prep + cook)
- Calories: per serving
- Tags: top 2-3 as colored badges
- Heart: filled if favorited, outline if not
