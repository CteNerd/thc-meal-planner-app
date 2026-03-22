# API Specification

Base URL: `https://{cloudfront-domain}/api`

All endpoints require JWT Bearer token in `Authorization` header unless noted.

Responses follow RFC 9457 Problem Details for errors.

---

## Authentication

Authentication is handled by AWS Cognito (see [AUTH_SPEC.md](AUTH_SPEC.md)). The API validates Cognito JWTs on every request.

---

## Endpoints

### Profile

#### `GET /api/profile`

Get the current user's profile.

**Response** `200 OK`

```json
{
  "userId": "string (Cognito sub)",
  "name": "string",
  "email": "string",
  "familyId": "string",
  "role": "head_of_household | member",
  "dietaryPrefs": ["vegetarian", "gluten-free"],
  "allergies": [
    {
      "allergen": "tree nuts",
      "severity": "severe",
      "reaction": "<severity-level>",
      "crossContamination": true
    }
  ],
  "excludedIngredients": ["mushrooms", "peanuts"],
  "macroTargets": {
    "calories": 2200,
    "protein": 80,
    "carbohydrates": 275,
    "fat": 73,
    "fiber": 30,
    "sodium": null
  },
  "cuisinePreferences": ["Mediterranean", "Asian", "Mexican"],
  "cookingConstraints": {
    "maxWeekdayPrepMinutes": 45,
    "maxWeekendPrepMinutes": 180,
    "prefersBatchCooking": true,
    "batchCookDay": "Sunday"
  },
  "flavorPreferences": {
    "spiceLevel": "high",
    "prefersSavory": true,
    "favoriteHerbs": ["cilantro", "basil", "mint"]
  },
  "defaultServings": 2,
  "familyMembers": [
    {
      "name": "Child 1",
      "age": "<age>",
      "preferences": "picky eater, mild flavors"
    },
    {
      "name": "Child 2",
      "age": "<age>",
      "preferences": "adventurous, loves vegetables"
    }
  ],
  "doctorNotes": ["Increase iron intake", "B12 supplementation"],
  "createdAt": "2026-01-15T00:00:00Z",
  "updatedAt": "2026-03-22T00:00:00Z"
}
```

#### `PUT /api/profile`

Update the current user's profile. Partial updates supported (merge semantics).

**Request Body**: Same shape as GET response (omit read-only fields: userId, createdAt).

**Response** `200 OK` — Updated profile.

#### `GET /api/family/members`

List all family member profiles (adults + dependents). Requires `head_of_household` role.

**Response** `200 OK` — Array of profile objects.

#### `GET /api/family/dependents`

List dependent profiles only. Requires `head_of_household` role.

**Response** `200 OK`

```json
[
  {
    "userId": "dep_abc123",
    "name": "Child 1",
    "familyId": "string",
    "role": "dependent",
    "ageGroup": "preschool",
    "dietaryPrefs": [],
    "allergies": [],
    "eatingStyle": "picky eater, prefers mild flavors",
    "preferredFoods": ["pasta", "rice", "cheese"],
    "avoidedFoods": ["spicy food", "mixed textures"],
    "macroTargets": { "calories": 1200 },
    "notes": "Prefers simple, familiar foods",
    "createdAt": "2026-01-20T00:00:00Z",
    "updatedAt": "2026-01-20T00:00:00Z"
  }
]
```

#### `POST /api/family/dependents`

Create a new dependent profile. Requires `head_of_household` role.

**Request Body**: Dependent profile object (omit userId, createdAt).

**Response** `201 Created` — Full dependent profile with generated userId (`dep_{shortId}`).

#### `PUT /api/family/dependents/{userId}`

Update a dependent profile. Requires `head_of_household` role.

**Request Body**: Partial dependent profile object (merge semantics).

**Response** `200 OK` — Updated dependent profile.

#### `DELETE /api/family/dependents/{userId}`

Delete a dependent profile. Requires `head_of_household` role.

**Response** `204 No Content`

---

### Meal Plans

#### `GET /api/meal-plans/current`

Get the active meal plan for the current family.

**Response** `200 OK`

```json
{
  "familyId": "string",
  "weekStartDate": "2026-01-20",
  "status": "active",
  "meals": [
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
  ],
  "nutritionalSummary": {
    "dailyAverages": {
      "calories": 1950,
      "protein": 82,
      "carbohydrates": 240,
      "fat": 65
    }
  },
  "constraintsUsed": "enhanced_v1",
  "generatedBy": "ai",
  "qualityScore": {
    "overall": 85,
    "varietyScore": 30,
    "diversityScore": 35,
    "constraintViolations": 0,
    "grade": "B+"
  },
  "createdAt": "2026-01-19T21:00:00Z"
}
```

#### `GET /api/meal-plans?week={yyyy-MM-dd}`

Get a specific week's meal plan. The `week` parameter should be the Monday of the target week.

**Response** `200 OK` — Same shape as above. `404` if no plan exists for that week.

#### `POST /api/meal-plans`

Create a new meal plan (manual).

**Request Body**:

```json
{
  "weekStartDate": "2026-01-27",
  "meals": [
    {
      "day": "Monday",
      "mealType": "dinner",
      "recipeId": "rec_abc123",
      "servings": 4
    }
  ]
}
```

**Response** `201 Created` — Full meal plan object.

#### `PUT /api/meal-plans/{weekStartDate}`

Update an existing meal plan. Supports partial meal updates.

> **Grocery List Cascade**: When meals are added, removed, or swapped, the service automatically recalculates the grocery list. Items exclusively associated with removed meals are deleted; new items are added from replacement recipes; shared ingredient quantities are adjusted. Manually-added items and checked-off states are preserved. The grocery list `version` is incremented to trigger polling updates for all connected clients.

**Response** `200 OK` — Updated plan.

#### `DELETE /api/meal-plans/{weekStartDate}`

Delete a meal plan.

**Response** `204 No Content`

---

### Recipes (Cookbook)

#### `GET /api/recipes`

List all recipes with optional filtering.

**Query Parameters**:

| Param | Type | Description |
|-------|------|-------------|
| `category` | string | Filter by: breakfast, lunch, dinner, snack |
| `tags` | string (comma-separated) | Filter by tags: gluten-free, vegetarian, kid-friendly |
| `cuisine` | string | Filter by cuisine: Mexican, Mediterranean, Asian |
| `search` | string | Full-text search on name and description |
| `favoritesOnly` | boolean | Only return user's favorites |
| `limit` | number | Page size (default: 50) |
| `nextToken` | string | Pagination token |

**Response** `200 OK`

```json
{
  "recipes": [
    {
      "recipeId": "rec_abc123",
      "name": "Veggie Stir Fry",
      "category": "dinner",
      "cuisine": "Asian",
      "servings": 4,
      "prepTime": 10,
      "cookTime": 15,
      "totalTime": 25,
      "tags": ["vegetarian", "gluten-free", "quick-weeknight"],
      "proteinSource": "Tofu",
      "thumbnailUrl": "https://...",
      "isFavorite": false,
      "nutritionalInfo": {
        "calories": 320,
        "protein": 15,
        "carbohydrates": 42,
        "fat": 12,
        "sodium": 680
      }
    }
  ],
  "nextToken": "string | null"
}
```

#### `GET /api/recipes/{recipeId}`

Get full recipe detail.

**Response** `200 OK`

```json
{
  "recipeId": "rec_abc123",
  "name": "Veggie Stir Fry",
  "description": "A quick and healthy stir fry packed with colorful vegetables.",
  "category": "dinner",
  "cuisine": "Asian",
  "servings": 4,
  "prepTime": 10,
  "cookTime": 15,
  "totalTime": 25,
  "proteinSource": "Tofu",
  "cookingMethod": "Stir-fried",
  "difficulty": "Easy",
  "tags": ["vegetarian", "gluten-free", "quick-weeknight", "dairy-free"],
  "ingredients": [
    {
      "name": "firm tofu",
      "quantity": 1,
      "unit": "block",
      "section": "protein",
      "notes": "pressed to remove excess water"
    },
    {
      "name": "mixed vegetables",
      "quantity": 2,
      "unit": "cups",
      "section": "produce",
      "notes": "bell peppers, broccoli, snap peas, carrots"
    }
  ],
  "instructions": [
    "Press tofu to remove excess water, then cut into cubes",
    "Heat vegetable oil in a large wok or skillet over high heat",
    "Add tofu and cook until golden on all sides (5-7 minutes)"
  ],
  "nutritionalInfo": {
    "calories": 320,
    "protein": 15,
    "carbohydrates": 42,
    "fat": 12,
    "fiber": null,
    "sodium": 680,
    "sugar": null
  },
  "dietaryInfo": {
    "vegetarian": true,
    "vegan": true,
    "glutenFree": true,
    "dairyFree": true,
    "nutFree": true,
    "lowSodium": false
  },
  "variations": ["Use tempeh instead of tofu for a nuttier flavor"],
  "storage": {
    "refrigerator": "3-4 days in airtight container",
    "freezer": "Not recommended (vegetables get soggy)"
  },
  "imageUrl": "https://... (pre-signed S3 URL)",
  "imageKey": "recipes/rec_abc123/main.jpg",
  "sourceUrl": null,
  "sourceType": "manual",
  "isFavorite": false,
  "favoriteNotes": null,
  "favoritePortionOverride": null,
  "createdAt": "2026-01-15T00:00:00Z",
  "updatedAt": "2026-03-20T00:00:00Z"
}
```

#### `POST /api/recipes`

Create a recipe manually.

**Request Body**: Recipe object (omit recipeId, createdAt, updatedAt, imageUrl, isFavorite fields).

**Response** `201 Created` — Full recipe with generated recipeId.

#### `POST /api/recipes/import`

Import a recipe from a URL.

**Request Body**:

```json
{
  "url": "https://www.example.com/recipes/chicken-tikka",
  "overrides": {
    "servings": 4,
    "tags": ["kid-friendly"]
  }
}
```

**Response** `201 Created` — Parsed recipe object. `sourceType` = `"url"`, `sourceUrl` populated.

#### `POST /api/recipes/{recipeId}/image`

Get a pre-signed URL for uploading a recipe image.

**Request Body**:

```json
{
  "contentType": "image/jpeg",
  "fileName": "chicken-tikka.jpg"
}
```

**Response** `200 OK`

```json
{
  "uploadUrl": "https://s3.amazonaws.com/... (pre-signed PUT URL, 5-min expiry)",
  "imageKey": "recipes/rec_abc123/main.jpg"
}
```

#### `POST /api/recipes/from-image`

Upload a photo of a physical recipe for OCR/AI parsing.

**Request Body**:

```json
{
  "imageKey": "uploads/temp_abc123.jpg"
}
```

**Response** `201 Created` — Parsed recipe object. `sourceType` = `"image_upload"`.

#### `PUT /api/recipes/{recipeId}`

Update a recipe.

**Response** `200 OK` — Updated recipe.

#### `DELETE /api/recipes/{recipeId}`

Delete a recipe.

**Response** `204 No Content`

#### `POST /api/recipes/{recipeId}/favorite`

Toggle favorite status for the current user.

**Request Body**:

```json
{
  "notes": "Double the garlic, use extra firm tofu",
  "portionOverride": 2
}
```

**Response** `200 OK`

```json
{
  "recipeId": "rec_abc123",
  "isFavorite": true,
  "notes": "Double the garlic, use extra firm tofu",
  "portionOverride": 2,
  "addedAt": "2026-03-22T00:00:00Z"
}
```

#### `GET /api/recipes/favorites`

Get the current user's favorite recipes.

**Response** `200 OK` — Array of recipe summaries with favorite metadata.

---

### Grocery Lists

#### `GET /api/grocery-lists/current`

Get the active grocery list for the current family.

**Response** `200 OK`

```json
{
  "familyId": "string",
  "listId": "LIST#ACTIVE",
  "items": [
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
      "checkedOffTimestamp": null,
      "inStock": false
    }
  ],
  "version": 42,
  "updatedAt": "2026-03-22T15:30:00Z",
  "progress": {
    "total": 24,
    "completed": 8,
    "percentage": 33
  }
}
```

#### `POST /api/grocery-lists/generate`

Auto-generate grocery list from the active meal plan. Aggregates ingredients across all recipes, groups by store section, handles unit conversion.

**Request Body** (optional):

```json
{
  "weekStartDate": "2026-01-20",
  "clearExisting": false
}
```

**Response** `201 Created` — Full grocery list object.

#### `PUT /api/grocery-lists/items/{itemId}/toggle`

Quick toggle checked state for an item.

**Request Body**:

```json
{
  "version": 42
}
```

**Response** `200 OK` — Updated item + new version number.

**Response** `409 Conflict` — Version mismatch (another user modified the list). Client should re-fetch and retry.

#### `PUT /api/grocery-lists/items/{itemId}`

Update an item (modify quantity, section, etc).

**Request Body**: Item object with version.

**Response** `200 OK` — Updated item + new version.

#### `POST /api/grocery-lists/items`

Add a manual item to the grocery list.

**Request Body**:

```json
{
  "name": "Paper towels",
  "section": "household",
  "quantity": 1,
  "unit": "pack",
  "version": 42
}
```

**Response** `201 Created` — New item + updated version.

#### `GET /api/grocery-lists/poll?since={timestamp}`

Polling endpoint for real-time sync. Returns only changes since the given ISO 8601 timestamp.

**Response** `200 OK`

```json
{
  "hasChanges": true,
  "changes": [
    {
      "itemId": "item_001",
      "action": "updated",
      "item": { "...full item object..." }
    }
  ],
  "version": 43,
  "updatedAt": "2026-03-22T15:31:00Z"
}
```

**Response** `304 Not Modified` — No changes since timestamp.

#### `PUT /api/grocery-lists/items/{itemId}/in-stock`

Toggle the in-stock status for an item.

**Request Body**:

```json
{
  "inStock": true,
  "version": 42
}
```

**Response** `200 OK` — Updated item + new version number.

---

### Pantry Staples

#### `GET /api/pantry/staples`

Get the family's persistent pantry staples list.

**Response** `200 OK`

```json
{
  "familyId": "string",
  "items": [
    { "name": "salt", "section": "spices" },
    { "name": "black pepper", "section": "spices" },
    { "name": "olive oil", "section": "pantry" },
    { "name": "garlic", "section": "produce" }
  ],
  "updatedAt": "2026-03-22T00:00:00Z"
}
```

#### `PUT /api/pantry/staples`

Replace the full pantry staples list.

**Request Body**:

```json
{
  "items": [
    { "name": "salt", "section": "spices" },
    { "name": "black pepper", "section": "spices" },
    { "name": "olive oil", "section": "pantry" }
  ]
}
```

**Response** `200 OK` — Updated staples list.

#### `POST /api/pantry/staples/items`

Add a single item to pantry staples.

**Request Body**:

```json
{
  "name": "cumin",
  "section": "spices"
}
```

**Response** `201 Created` — Updated staples list.

#### `DELETE /api/pantry/staples/items/{name}`

Remove an item from pantry staples.

**Response** `204 No Content`

---

### Chat

#### `POST /api/chat`

Send a message to the AI chatbot.

**Request Body**:

```json
{
  "message": "Generate a meal plan for next week. Make sure Thursday is a no-cook night.",
  "conversationId": "conv_abc123"
}
```

**Response** `200 OK` (streamed via chunked transfer encoding)

```json
{
  "conversationId": "conv_abc123",
  "response": "I've created a meal plan for the week of March 23-29...",
  "actions": [
    {
      "type": "generate_meal_plan",
      "status": "completed",
      "result": {
        "weekStartDate": "2026-03-23",
        "mealsGenerated": 18
      }
    }
  ],
  "requiresConfirmation": false
}
```

For destructive operations, `requiresConfirmation: true` and the client must call confirm:

#### `POST /api/chat/confirm`

Confirm a pending destructive action.

**Request Body**:

```json
{
  "conversationId": "conv_abc123",
  "actionId": "action_xyz",
  "confirmed": true
}
```

#### `GET /api/chat/history?limit={n}&nextToken={token}`

Get paginated chat history.

**Response** `200 OK` — Array of messages with role, content, actions, timestamps.

#### `DELETE /api/chat/history`

Clear all chat history for the current user.

**Response** `204 No Content`

---

### Health

#### `GET /api/health`

Health check endpoint (no auth required).

**Response** `200 OK`

```json
{
  "status": "healthy",
  "version": "1.0.0",
  "timestamp": "2026-03-22T00:00:00Z"
}
```

---

## Error Response Format (RFC 9457)

All error responses use the Problem Details format:

```json
{
  "type": "https://thc-meal-planner.app/errors/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "The 'servings' field must be a positive integer.",
  "instance": "/api/recipes",
  "errors": {
    "servings": ["Must be greater than 0"]
  }
}
```

## HTTP Status Codes

| Code | Usage |
|------|-------|
| 200 | Successful retrieval or update |
| 201 | Successful creation |
| 204 | Successful deletion |
| 304 | No changes (polling) |
| 400 | Validation error |
| 401 | Missing or invalid JWT |
| 403 | Insufficient role permissions |
| 404 | Resource not found |
| 409 | Optimistic concurrency conflict |
| 429 | Rate limit exceeded |
| 500 | Internal server error |

## Rate Limits

| Endpoint | Limit | Window |
|----------|-------|--------|
| `POST /api/chat` | 30 requests | Per minute per user |
| All other endpoints | 100 requests | Per second (API Gateway burst) |
| Sustained rate | 50 requests | Per second (API Gateway) |
