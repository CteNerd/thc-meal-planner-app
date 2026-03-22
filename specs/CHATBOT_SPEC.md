# Chatbot Specification

## Overview

The AI chatbot is the primary interface for meal planning operations. It uses OpenAI's function calling (tool use) to perform full CRUD operations on meal plans, recipes, grocery lists, and profiles. The chatbot is context-aware — it knows the family's dietary restrictions, preferences, and current state.

---

## Architecture

```
React Chat UI
     │
     ▼
POST /api/chat/message
     │
     ▼
ChatService (ASP.NET Core)
     │
     ├── Build system prompt (family context)
     ├── Append user message
     ├── Call OpenAI Chat Completions API
     │       │
     │       ▼
     │   OpenAI decides:
     │   ├── text response → return to user
     │   └── function call → execute function
     │           │
     │           ▼
     │       Execute via service layer
     │       (MealPlanService, RecipeService, etc.)
     │           │
     │           ▼
     │       Return function result to OpenAI
     │       (may trigger additional function calls)
     │           │
     │           ▼
     │       Final text response
     │
     ▼
Store conversation in ChatHistory table
Return response to frontend
```

---

## OpenAI Configuration

| Setting | Value |
|---------|-------|
| Model | `gpt-4o-mini` (cost-effective, function calling support) |
| Temperature | 0.7 (balanced creativity/consistency) |
| Max Tokens | 2048 (response limit) |
| Timeout | 30 seconds |
| Rate Limit | 20 requests/minute per user |

---

## System Prompt

The system prompt is re-built on each conversation turn with current family context:

```
You are a friendly and helpful meal planning assistant for the {familyName} family.

## Family Members
{-- Built dynamically from user profiles in DynamoDB (adults + dependents) --}
{-- Example format for each adult member: --}
- {name}: {dietaryPrefs}. Excludes {excludedIngredients}. Prefers {cuisinePreferences}. Target: {calories} kcal, {protein}g protein. {cookingConstraints}.
{-- Example format for each dependent (child): --}
- {childName} (age {age}): {eatingStyle}. Likes: {preferredFoods}. Avoids: {avoidedFoods}.

## Shopping
{-- Built dynamically from store preferences config --}
- Primary: {primaryStore} ({frequency})
- Bulk: {bulkStore} ({frequency})
- As-needed: {supplementaryStore}

## Current Week
- Active meal plan: {activePlanSummary or "None"}
- Active grocery list: {itemCount} items ({checkedCount} completed)

## Rules
1. NEVER suggest recipes containing known allergens for any family member with that allergy.
2. Always respect dietary restrictions for ALL family members.
3. Keep weeknight dinners under 45 minutes total.
4. When suggesting recipes for the whole family, ensure they work for ALL members.
5. For destructive actions (deleting plans, clearing grocery lists), always ask for confirmation.
6. Provide nutritional info when creating or suggesting recipes.
7. Be concise but warm. Use markdown formatting for recipes and lists.
```

> **Note**: The system prompt is populated at runtime from DynamoDB user profiles and app configuration. No PII is hardcoded. See `.local/profiles/` for the real family data used during development/testing.

---

## Function Definitions

### 1. generate_meal_plan

Generates a weekly meal plan respecting all dietary constraints.

```json
{
  "name": "generate_meal_plan",
  "description": "Generate a new weekly meal plan for the family",
  "parameters": {
    "type": "object",
    "properties": {
      "weekStartDate": {
        "type": "string",
        "description": "ISO date for the Monday of the target week"
      },
      "preferences": {
        "type": "object",
        "properties": {
          "focusCuisine": { "type": "string", "description": "Optional cuisine theme" },
          "maxBudget": { "type": "number", "description": "Optional weekly budget cap" },
          "excludeRecipeIds": { "type": "array", "items": { "type": "string" } }
        }
      }
    },
    "required": ["weekStartDate"]
  }
}
```

**Execution**: Calls `MealPlanService.GenerateAsync()` → invokes ConstraintEngine (checks ALL family profiles including dependents) → validates → stores plan → triggers grocery list generation (with pantry staple matching).

### 2. modify_meal_plan

Swaps individual meals within an existing plan.

```json
{
  "name": "modify_meal_plan",
  "description": "Swap a specific meal in the current plan",
  "parameters": {
    "type": "object",
    "properties": {
      "day": { "type": "string", "enum": ["Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday"] },
      "mealType": { "type": "string", "enum": ["breakfast","lunch","dinner","snack"] },
      "newRecipeId": { "type": "string", "description": "Optional — if omitted, AI suggests a replacement" },
      "reason": { "type": "string", "description": "Why the swap is needed (helps AI pick better)" }
    },
    "required": ["day", "mealType"]
  }
}
```

**Execution**: Calls `MealPlanService.SwapMealAsync()` → validates constraints → updates plan → **cascades to grocery list** (removes ingredients for old meal, adds ingredients for new meal, adjusts shared quantities, preserves manually-added items and check-off states). The AI should inform the user about grocery list changes (e.g., "I've swapped Thursday's dinner and updated your grocery list — removed broccoli, added green beans").

### 3. search_recipes

Searches the family cookbook with filters.

```json
{
  "name": "search_recipes",
  "description": "Search recipes in the family cookbook",
  "parameters": {
    "type": "object",
    "properties": {
      "query": { "type": "string", "description": "Free-text search" },
      "cuisine": { "type": "string" },
      "category": { "type": "string", "enum": ["breakfast","lunch","dinner","snack"] },
      "maxPrepTime": { "type": "number", "description": "Max prep + cook time in minutes" },
      "tags": { "type": "array", "items": { "type": "string" } },
      "forUser": { "type": "string", "description": "Filter to recipes safe for this user" }
    }
  }
}
```

### 4. create_recipe

Creates a new recipe in the cookbook.

```json
{
  "name": "create_recipe",
  "description": "Add a new recipe to the family cookbook",
  "parameters": {
    "type": "object",
    "properties": {
      "name": { "type": "string" },
      "category": { "type": "string", "enum": ["breakfast","lunch","dinner","snack"] },
      "cuisine": { "type": "string" },
      "servings": { "type": "number" },
      "prepTime": { "type": "number" },
      "cookTime": { "type": "number" },
      "proteinSource": { "type": "string" },
      "cookingMethod": { "type": "string" },
      "ingredients": {
        "type": "array",
        "items": {
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "quantity": { "type": "number" },
            "unit": { "type": "string" }
          }
        }
      },
      "instructions": { "type": "array", "items": { "type": "string" } },
      "tags": { "type": "array", "items": { "type": "string" } }
    },
    "required": ["name", "category", "ingredients", "instructions"]
  }
}
```

### 5. manage_grocery_list

Performs operations on the active grocery list.

```json
{
  "name": "manage_grocery_list",
  "description": "Add, remove, or check off items on the grocery list",
  "parameters": {
    "type": "object",
    "properties": {
      "action": { "type": "string", "enum": ["add_items", "remove_items", "check_off", "uncheck", "clear_completed"] },
      "items": {
        "type": "array",
        "items": {
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "quantity": { "type": "number" },
            "unit": { "type": "string" },
            "section": { "type": "string" }
          }
        }
      }
    },
    "required": ["action"]
  }
}
```

### 6. update_profile

Updates user dietary preferences or constraints.

```json
{
  "name": "update_profile",
  "description": "Update a user's dietary preferences, allergies, or constraints",
  "parameters": {
    "type": "object",
    "properties": {
      "userId": { "type": "string", "description": "Which user to update" },
      "updates": {
        "type": "object",
        "description": "Fields to update — any profile field",
        "additionalProperties": true
      }
    },
    "required": ["userId", "updates"]
  }
}
```

### 7. get_nutritional_info

Calculates nutrition for a recipe or meal combination.

```json
{
  "name": "get_nutritional_info",
  "description": "Get nutritional breakdown for recipes or a day's meals",
  "parameters": {
    "type": "object",
    "properties": {
      "recipeIds": { "type": "array", "items": { "type": "string" } },
      "day": { "type": "string", "description": "Get full day nutrition from current plan" }
    }
  }
}
```

### 8. manage_pantry

Manages the family's pantry staples list.

```json
{
  "name": "manage_pantry",
  "description": "Add or remove items from the family's pantry staples list. Items in the pantry are auto-marked as 'in stock' on grocery lists.",
  "parameters": {
    "type": "object",
    "properties": {
      "action": { "type": "string", "enum": ["add_items", "remove_items", "list"] },
      "items": {
        "type": "array",
        "items": {
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "section": { "type": "string" }
          }
        }
      }
    },
    "required": ["action"]
  }
}
```

**Execution**: Calls `PantryService` → updates `PANTRY#STAPLES` record → if items were added/removed, rechecks active grocery list to update `inStock` flags.

---

## Confirmation Flow

Destructive actions require user confirmation before execution:

```
User: "Clear the grocery list"
  │
  ▼
AI: "I'll clear all items from the grocery list. This will remove 23 items.
     Should I proceed? (yes/no)"
  │
  ▼
User: "yes"
  │
  ▼
POST /api/chat/confirm
  { "conversationId": "...", "confirmed": true }
  │
  ▼
ChatService executes pending action
  │
  ▼
AI: "Done! The grocery list has been cleared."
```

**Destructive actions requiring confirmation**:
- Deleting a meal plan
- Clearing the grocery list
- Deleting a recipe from the cookbook
- Bulk profile changes

The `pendingConfirmation` field in ChatHistory stores the action awaiting confirmation.

---

## Conversation Storage

- Each message (user + assistant) stored as separate items in ChatHistory table
- `conversationId` groups messages in a session (new conversation started after 30 min idle)
- 30-day TTL auto-deletes old messages
- Function call details stored in `actions` field for auditability

---

## Error Handling

| Scenario | Behavior |
|----------|----------|
| OpenAI API timeout | Return friendly error: "I'm having trouble thinking right now. Please try again." |
| OpenAI rate limit | Queue and retry with exponential backoff (max 3 attempts) |
| Function execution fails | Return error context to OpenAI to generate helpful message |
| Invalid function args | Validate before execution, return validation errors to OpenAI |
| Safety guardrail triggered | Return: "I can only help with meal planning topics." |

---

## Safety Guardrails

1. **Topic restriction**: System prompt constrains responses to meal planning, nutrition, and cooking
2. **Allergy safety**: Any recipe suggestion is validated against all family members' allergies before returning
3. **Input sanitization**: User messages are sanitized before storage (strip HTML/script tags)
4. **Token budget**: Max 4096 tokens per conversation turn (system + history + user + response)
5. **History truncation**: Only last 20 messages included in context window; older messages summarized

---

## Cost Optimization

- Use `gpt-4o-mini` ($0.15/1M input, $0.60/1M output) instead of `gpt-4o`
- Cache system prompt template; only rebuild dynamic sections per request
- Limit conversation history to 20 messages in context
- Estimated cost: ~$1-3/month for a 2-person household with moderate use
