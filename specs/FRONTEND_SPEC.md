# Frontend Specification

## Overview

Single-page application built with React 19, TypeScript, Vite, and TailwindCSS. Responsive design targets mobile-first with three breakpoints. Installable as a PWA for quick access on mobile devices.

---

## Technology Stack

| Technology | Version | Purpose |
|-----------|---------|---------|
| React | 19.x | UI framework |
| TypeScript | 5.x | Type safety |
| Vite | 6.x | Build tooling |
| TailwindCSS | 4.x | Utility-first styling |
| React Router | 7.x | Client-side routing |
| Vitest | 2.x | Unit testing |
| Playwright | 1.x | E2E testing |
| @aws-amplify/auth | Latest | Cognito auth SDK (auth only, not Amplify hosting) |

---

## Design System

### Color Palette

| Token | Color | Hex | Usage |
|-------|-------|-----|-------|
| Primary | Soft Blue | `#60A5FA` | Buttons, links, active states |
| Secondary | Warm Yellow | `#FBBF24` | Highlights, badges, warnings |
| Accent | Gentle Green | `#A7F3D0` | Success states, nutrition indicators |
| Background | Off-White | `#FAFAFA` | Page background |
| Surface | White | `#FFFFFF` | Cards, modals |
| Text Primary | Charcoal | `#1F2937` | Body text |
| Text Secondary | Gray | `#6B7280` | Labels, metadata |
| Error | Red | `#EF4444` | Error messages, allergy warnings |
| Warning | Amber | `#F59E0B` | Caution states |

### Typography

| Element | Font | Size | Weight |
|---------|------|------|--------|
| Heading 1 | Inter | 2rem / 32px | 700 |
| Heading 2 | Inter | 1.5rem / 24px | 600 |
| Heading 3 | Inter | 1.25rem / 20px | 600 |
| Body | Inter | 1rem / 16px | 400 |
| Small | Inter | 0.875rem / 14px | 400 |
| Caption | Inter | 0.75rem / 12px | 400 |

### Responsive Breakpoints

| Breakpoint | Width | Target |
|-----------|-------|--------|
| Mobile | < 640px | Phones (primary use at grocery store) |
| Tablet | 640px – 1024px | iPad, tablet browsing |
| Desktop | > 1024px | Laptop/desktop meal planning |

---

## Page Layouts

### 1. Login Page (`/login`)
- Email + password form
- TOTP code input (appears after credentials validated)
- "Remember this device" checkbox (stores refresh token)
- Error messages for invalid credentials
- No registration link (admin-provisioned users only)

### 2. Dashboard (`/dashboard`)
- **This Week** card: current meal plan summary (Mon–Sun grid)
- **Grocery List** card: item count, completion percentage, quick link
- **Quick Actions**: "Generate New Plan", "Add Recipe", "Open Chat"
- **Recent Chat**: last chatbot interaction preview
- Responsive: cards stack vertically on mobile

### 3. Meal Plans (`/meal-plans`)
- **Current Plan** (`/meal-plans/current`): week view with day columns, meal rows (breakfast/lunch/dinner/snack), recipe cards with thumbnail + name + time + calories
- **History** (`/meal-plans/history`): list of past plans by week with quality score badge
- **Plan Detail** (`/meal-plans/:weekDate`): full plan with nutritional summary chart
- Responsive: horizontal scroll on mobile, or switch to day-by-day view

### 4. Cookbook (`/cookbook`)
- **Browse** (`/cookbook`): recipe grid with search bar, category/cuisine filters, card layout (image + name + time + tags)
- **Recipe Detail** (`/cookbook/:id`): full recipe with image, ingredients (checkable), instructions, nutrition panel, variations, "Add to Favorites" heart button
- **Add Recipe** (`/cookbook/new`): form with fields from recipe schema, image upload dropzone, "Import from URL" button
- **Favorites** (`/cookbook/favorites`): filtered grid of favorited recipes with personal notes
- Responsive: 1 column mobile, 2 columns tablet, 3-4 columns desktop

### 5. Grocery List (`/grocery-list`)
- Grouped by store section (Produce, Dairy, Protein, etc.)
- Each item: checkbox + name + quantity + unit + linked recipe + in-stock badge
- Items flagged as `inStock` show a "✓ In Stock" badge and are visually muted (not in "to buy" count)
- Users can toggle in-stock per-item (long-press or swipe action)
- Real-time sync via polling (5s interval when tab visible)
- Swipe to remove on mobile
- "Add Item" floating action button
- Completed items collapse to bottom with gray styling
- Version indicator for conflict detection
- **Pantry Staples** section: manage persistent pantry staples list (settings gear icon); items on this list are auto-marked in-stock when grocery lists are generated
- Progress bar shows "to buy" count (excludes in-stock and completed items)
- Filter options: unchecked only, section, meal association, in-stock/needs-buying
- Responsive: full-width on mobile for easy tapping at the store

### 6. Chat (`/chat`)
- Chat bubble interface (user messages right, AI left)
- Markdown rendering in AI responses (recipes, lists)
- Action cards for confirmations (approve/reject buttons)
- Input bar with send button at bottom
- Chat history sidebar on desktop, drawer on mobile
- Typing indicator during AI processing
- Responsive: full-screen on mobile

### 7. Profile (`/profile`)
- Tabbed view: Personal Info, Dietary Preferences, Family Members, Notifications
- **Personal**: name, email (read-only from Cognito)
- **Dietary**: allergies (severity selector), excluded ingredients (tag input), cuisine preferences (multi-select), macro targets (sliders), cooking constraints
- **Family Members**: list of dependent profiles (children); add/edit/remove dependents; each dependent shows: name, age, eating style, preferred/avoided foods, notes
- **Notifications**: toggle switches for email preferences
- Save button with optimistic UI update
- Responsive: single column on mobile, two-column on desktop

---

## Component Architecture

```
src/
├── components/
│   ├── ui/                      # Reusable primitives
│   │   ├── Button.tsx
│   │   ├── Card.tsx
│   │   ├── Input.tsx
│   │   ├── Badge.tsx
│   │   ├── Modal.tsx
│   │   ├── Skeleton.tsx
│   │   ├── Toast.tsx
│   │   └── DropZone.tsx
│   ├── layout/
│   │   ├── AppShell.tsx         # Main layout wrapper
│   │   ├── Sidebar.tsx          # Desktop navigation
│   │   ├── BottomNav.tsx        # Mobile navigation
│   │   └── Header.tsx
│   ├── auth/
│   │   ├── LoginForm.tsx
│   │   ├── TotpInput.tsx
│   │   └── RequireAuth.tsx
│   ├── meal-plans/
│   │   ├── WeekView.tsx
│   │   ├── DayColumn.tsx
│   │   ├── MealCard.tsx
│   │   ├── PlanHistory.tsx
│   │   └── NutritionSummary.tsx
│   ├── cookbook/
│   │   ├── RecipeGrid.tsx
│   │   ├── RecipeCard.tsx
│   │   ├── RecipeDetail.tsx
│   │   ├── RecipeForm.tsx
│   │   ├── ImportFromUrl.tsx
│   │   ├── ImageUpload.tsx
│   │   └── FavoriteButton.tsx
│   ├── grocery/
│   │   ├── GroceryList.tsx
│   │   ├── SectionGroup.tsx
│   │   ├── GroceryItem.tsx
│   │   ├── AddItemFab.tsx
│   │   ├── InStockBadge.tsx
│   │   └── PantryStaplesModal.tsx
│   ├── profile/
│   │   ├── DependentList.tsx
│   │   ├── DependentForm.tsx
│   │   └── DependentCard.tsx
│   └── chat/
│       ├── ChatWindow.tsx
│       ├── MessageBubble.tsx
│       ├── ConfirmationCard.tsx
│       ├── TypingIndicator.tsx
│       └── ChatHistory.tsx
├── hooks/
│   ├── useAuth.ts               # Auth context consumer
│   ├── useApi.ts                # Fetch wrapper with auth headers
│   ├── usePolling.ts            # Activity-based polling with Page Visibility
│   ├── useGroceryList.ts        # Grocery list state + optimistic concurrency
│   ├── usePantry.ts             # Pantry staples CRUD
│   ├── useMealPlan.ts           # Current plan data
│   ├── useDependents.ts         # Family dependent profiles CRUD
│   ├── useRecipes.ts            # Cookbook data
│   └── useChat.ts               # Chat state management
├── contexts/
│   ├── AuthContext.tsx
│   └── ToastContext.tsx
├── pages/
│   ├── DashboardPage.tsx
│   ├── MealPlansPage.tsx
│   ├── CookbookPage.tsx
│   ├── GroceryListPage.tsx
│   ├── ChatPage.tsx
│   ├── ProfilePage.tsx
│   └── LoginPage.tsx
├── services/
│   └── api.ts                   # API client (base URL, interceptors)
├── types/
│   └── index.ts                 # Shared TypeScript interfaces
├── utils/
│   ├── formatters.ts            # Date, nutrition formatting
│   └── validators.ts            # Client-side validation
├── App.tsx
├── main.tsx
└── vite-env.d.ts
```

---

## Key Hooks

### usePolling

Activity-based polling using the Page Visibility API:

```typescript
function usePolling<T>(
  fetcher: () => Promise<T>,
  intervalMs: number,
  options?: { enabled?: boolean }
): { data: T | null; isLoading: boolean; error: Error | null } {
  // Poll every intervalMs when document.visibilityState === "visible"
  // Pause polling when tab is hidden
  // Resume immediately when tab becomes visible again
  // Use If-None-Match / If-Modified-Since for 304 responses
}
```

Used by: `useGroceryList` (5s interval), `useMealPlan` (30s interval).

### useGroceryList

Manages optimistic concurrency for the grocery list:

```typescript
function useGroceryList() {
  // Maintains local version number
  // On toggle: optimistic UI update → PATCH request with version
  // On 409 Conflict: refetch latest version, re-apply local changes
  // On 304 Not Modified: skip state update (no changes on server)
}
```

---

## PWA Configuration

```json
// vite.config.ts — vite-plugin-pwa
{
  "name": "THC Meal Planner",
  "short_name": "MealPlanner",
  "start_url": "/dashboard",
  "display": "standalone",
  "background_color": "#FAFAFA",
  "theme_color": "#60A5FA",
  "icons": [
    { "src": "/icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/icons/icon-512.png", "sizes": "512x512", "type": "image/png" }
  ]
}
```

- **Caching Strategy**: Network-first for API calls, cache-first for static assets
- **Offline**: Show cached dashboard/grocery list, queue changes for sync when online
- **Install Prompt**: Show "Add to Home Screen" banner on first mobile visit

---

## Navigation

### Desktop (> 1024px)
- Left sidebar: vertical icon + label links
- Persistent, collapsible to icons only

### Mobile (< 640px)
- Bottom navigation bar: 5 icons (Dashboard, Meals, Cookbook, Grocery, Chat)
- Profile accessible via header avatar

### Tablet (640–1024px)
- Collapsible sidebar (default collapsed to icons)

---

## State Management

No external state library needed. React 19 with:
- `useContext` for auth state and toast notifications
- Custom hooks for data fetching and caching
- URL state via React Router for filters/pagination
- Component-local state for forms and UI interactions

---

## API Integration

```typescript
// services/api.ts
const api = {
  baseUrl: import.meta.env.VITE_API_BASE_URL,

  async request<T>(path: string, options?: RequestInit): Promise<T> {
    const token = getAccessToken(); // from AuthContext
    const response = await fetch(`${this.baseUrl}${path}`, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
        ...options?.headers,
      },
    });

    if (response.status === 401) {
      // Attempt token refresh, retry once
    }

    if (!response.ok) {
      // Parse RFC 9457 problem details
      throw new ApiError(await response.json());
    }

    if (response.status === 204) return undefined as T;
    return response.json();
  },
};
```

---

## Accessibility

- Semantic HTML elements (`nav`, `main`, `section`, `article`)
- ARIA labels on all interactive elements
- Keyboard navigation for all actions
- Focus management on route changes
- Color contrast: WCAG AA minimum (4.5:1 for text)
- Screen reader support for grocery list checkbox states
