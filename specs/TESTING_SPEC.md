# Testing Specification

## Overview

Testing follows the testing pyramid with an 80% code coverage minimum enforced in CI. Three layers: unit tests (foundation), integration tests (service + data), and E2E tests (critical user journeys).

---

## Testing Pyramid

```
        ╱╲
       ╱  ╲        E2E (Playwright)
      ╱ 15% ╲      ~20 tests — critical paths
     ╱────────╲
    ╱          ╲    Integration (xUnit / Vitest)
   ╱    25%     ╲   ~80 tests — service + API
  ╱──────────────╲
 ╱                ╲  Unit (xUnit / Vitest)
╱      60%         ╲ ~200 tests — logic + components
╱────────────────────╲
```

| Layer | Backend | Frontend | Count (est.) |
|-------|---------|----------|-------------|
| Unit | xUnit + FluentAssertions + NSubstitute | Vitest + React Testing Library | ~200 |
| Integration | xUnit + WebApplicationFactory | Vitest + MSW (Mock Service Worker) | ~80 |
| E2E | — | Playwright (TypeScript) | ~20 |

---

## Backend Testing (.NET)

### Frameworks

| Package | Purpose |
|---------|---------|
| xUnit | Test framework |
| FluentAssertions | Readable assertions |
| NSubstitute | Mocking (interfaces) |
| Coverlet | Code coverage collection |
| Microsoft.AspNetCore.Mvc.Testing | Integration test host |

### Project Structure

```
tests/
├── ThcMealPlanner.Api.UnitTests/
│   ├── Services/
│   │   ├── MealPlanServiceTests.cs
│   │   ├── RecipeServiceTests.cs
│   │   ├── GroceryListServiceTests.cs
│   │   ├── ChatServiceTests.cs
│   │   ├── ConstraintEngineTests.cs
│   │   └── RecipeSafetyValidatorTests.cs
│   ├── Validators/
│   │   ├── UpdateProfileValidatorTests.cs
│   │   ├── CreateRecipeValidatorTests.cs
│   │   └── ChatMessageValidatorTests.cs
│   └── ThcMealPlanner.Api.UnitTests.csproj
├── ThcMealPlanner.Api.IntegrationTests/
│   ├── Endpoints/
│   │   ├── ProfileEndpointsTests.cs
│   │   ├── MealPlanEndpointsTests.cs
│   │   ├── RecipeEndpointsTests.cs
│   │   ├── GroceryListEndpointsTests.cs
│   │   └── ChatEndpointsTests.cs
│   ├── Fixtures/
│   │   ├── TestWebApplicationFactory.cs
│   │   └── DynamoDbLocalFixture.cs
│   └── ThcMealPlanner.Api.IntegrationTests.csproj
```

### Unit Test Patterns

```csharp
public class ConstraintEngineTests
{
    private readonly ConstraintEngine _sut;
    private readonly IOptions<ConstraintOptions> _options;

    public ConstraintEngineTests()
    {
        _options = Options.Create(new ConstraintOptions
        {
            NoCookNights = ["Wednesday"],
            MaxRepeatDays = 7,
            MinCuisineVariety = 3,
        });
        _sut = new ConstraintEngine(_options);
    }

    [Fact]
    public void Validate_WednesdayDinner_RejectsRequiringCooking()
    {
        var meal = new PlannedMeal
        {
            Day = DayOfWeek.Wednesday,
            MealType = MealType.Dinner,
            Recipe = new Recipe { CookingMethod = "Grilled" },
        };

        var result = _sut.Validate(meal);

        result.Should().ContainSingle()
            .Which.Should().Contain("no-cook night");
    }

    [Fact]
    public void Validate_NoCookRecipeOnWednesday_Passes()
    {
        var meal = new PlannedMeal
        {
            Day = DayOfWeek.Wednesday,
            MealType = MealType.Dinner,
            Recipe = new Recipe { CookingMethod = "No-cook" },
        };

        var result = _sut.Validate(meal);

        result.Should().BeEmpty();
    }
}
```

```csharp
public class RecipeSafetyValidatorTests
{
    [Fact]
    public void ValidateForUser_AllergenRecipeForAllergicUser_ReturnsViolation()
    {
        var recipe = new Recipe
        {
            Ingredients = [new Ingredient { Name = "cashews" }],
        };
        var user = new UserProfile
        {
            Allergies =
            [
                new Allergy
                {
                    Allergen = "tree nuts",
                    Severity = "severe",
                },
            ],
        };

        var result = new RecipeSafetyValidator().ValidateForUser(recipe, user);

        result.Violations.Should().ContainSingle()
            .Which.Should().Contain("tree nuts");
    }

    [Fact]
    public void ValidateForUser_DairyRecipeForVegetarianUser_Passes()
    {
        var recipe = new Recipe
        {
            Ingredients = [new Ingredient { Name = "cheddar cheese" }],
            DietaryInfo = new DietaryInfo { Vegetarian = true },
        };
        var user = new UserProfile
        {
            DietaryPrefs = ["vegetarian"],
            Allergies = [], // Adult 1 has no allergies
        };

        var result = new RecipeSafetyValidator().ValidateForUser(recipe, user);

        result.Violations.Should().BeEmpty();
    }
}
```

### Integration Test Patterns

```csharp
public class ProfileEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProfileEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        // Factory seeds test user + JWT token
    }

    [Fact]
    public async Task GetProfile_ReturnsCurrentUserProfile()
    {
        var response = await _client.GetAsync("/api/profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content
            .ReadFromJsonAsync<ProfileResponse>();
        profile!.Name.Should().NotBeEmpty();
        profile.FamilyId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateProfile_InvalidMacros_Returns400()
    {
        var request = new { MacroTargets = new { Calories = -100 } };

        var response = await _client.PutAsJsonAsync("/api/profile", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

**DynamoDB Local**: Integration tests use DynamoDB Local (Docker container) to test actual data access patterns without hitting AWS.

---

## Frontend Testing (React)

### Frameworks

| Package | Purpose |
|---------|---------|
| Vitest | Test framework (Vite-native) |
| @testing-library/react | Component testing |
| @testing-library/user-event | User interaction simulation |
| @vitest/coverage-v8 | Code coverage |
| MSW (Mock Service Worker) | API mocking |

### Test Structure

```
frontend/
├── src/
│   ├── components/
│   │   ├── cookbook/
│   │   │   ├── RecipeCard.tsx
│   │   │   ├── RecipeCard.test.tsx
│   │   │   ├── FavoriteButton.tsx
│   │   │   └── FavoriteButton.test.tsx
│   │   ├── grocery/
│   │   │   ├── GroceryItem.tsx
│   │   │   └── GroceryItem.test.tsx
│   │   └── ...
│   ├── hooks/
│   │   ├── usePolling.ts
│   │   ├── usePolling.test.ts
│   │   ├── useGroceryList.ts
│   │   └── useGroceryList.test.ts
│   └── ...
├── tests/
│   ├── mocks/
│   │   ├── handlers.ts          # MSW request handlers
│   │   └── server.ts            # MSW server setup
│   └── setup.ts                 # Vitest global setup
```

### Unit Test Patterns

```typescript
// RecipeCard.test.tsx
describe('RecipeCard', () => {
  it('renders recipe name and cooking time', () => {
    render(<RecipeCard recipe={mockRecipe} />);

    expect(screen.getByText('Veggie Stir Fry')).toBeInTheDocument();
    expect(screen.getByText('25 min')).toBeInTheDocument();
  });

  it('shows filled heart when favorited', () => {
    render(<RecipeCard recipe={mockRecipe} isFavorited={true} />);

    expect(screen.getByLabelText('Remove from favorites'))
      .toBeInTheDocument();
  });

  it('displays allergy warning badge for unsafe recipes', () => {
    render(<RecipeCard recipe={nutRecipe} userAllergies={['tree nuts']} />);

    expect(screen.getByText('⚠️ Contains allergen'))
      .toBeInTheDocument();
  });
});
```

```typescript
// usePolling.test.ts
describe('usePolling', () => {
  it('polls at specified interval when tab is visible', async () => {
    const fetcher = vi.fn().mockResolvedValue({ data: 'test' });

    const { result } = renderHook(() =>
      usePolling(fetcher, 5000)
    );

    await waitFor(() => {
      expect(fetcher).toHaveBeenCalledTimes(1); // initial fetch
    });

    vi.advanceTimersByTime(5000);

    await waitFor(() => {
      expect(fetcher).toHaveBeenCalledTimes(2); // after interval
    });
  });

  it('pauses polling when tab is hidden', async () => {
    const fetcher = vi.fn().mockResolvedValue({ data: 'test' });

    renderHook(() => usePolling(fetcher, 5000));

    // Simulate tab hidden
    Object.defineProperty(document, 'visibilityState', {
      value: 'hidden', writable: true,
    });
    document.dispatchEvent(new Event('visibilitychange'));

    vi.advanceTimersByTime(15000);

    expect(fetcher).toHaveBeenCalledTimes(1); // No additional calls
  });
});
```

---

## E2E Testing (Playwright)

### Structure

```
e2e/
├── tests/
│   ├── auth.spec.ts              # Login/logout flows
│   ├── meal-plans.spec.ts        # Plan generation and viewing
│   ├── cookbook.spec.ts           # Recipe CRUD
│   ├── grocery-list.spec.ts      # Grocery list interactions
│   └── chat.spec.ts              # Chatbot conversations
├── pages/
│   ├── LoginPage.ts              # Page object model
│   ├── DashboardPage.ts
│   ├── MealPlansPage.ts
│   ├── CookbookPage.ts
│   ├── GroceryListPage.ts
│   └── ChatPage.ts
├── fixtures/
│   └── auth.fixture.ts           # Authenticated session setup
├── playwright.config.ts
└── package.json
```

### Key E2E Tests

```typescript
// auth.spec.ts
test('user can login with email and TOTP', async ({ page }) => {
  const loginPage = new LoginPage(page);
  await loginPage.goto();
  await loginPage.fillEmail('testuser@example.com');
  await loginPage.fillPassword('TestPassword123!');
  await loginPage.submit();

  // TOTP step appears
  await expect(loginPage.totpInput).toBeVisible();
  await loginPage.fillTotp(generateTOTP(testSecretKey));
  await loginPage.submitTotp();

  // Redirected to dashboard
  await expect(page).toHaveURL('/dashboard');
});

// grocery-list.spec.ts
test('checking off item updates list in real-time', async ({ browser }) => {
  // Two browser contexts (simulating two users)
  const contextA = await browser.newContext({ storageState: 'adult1-auth.json' });
  const contextB = await browser.newContext({ storageState: 'adult2-auth.json' });

  const pageA = await contextA.newPage();
  const pageB = await contextB.newPage();

  await pageA.goto('/grocery-list');
  await pageB.goto('/grocery-list');

  // User A checks off "firm tofu"
  await pageA.getByLabel('firm tofu').check();

  // User B sees the update within polling interval
  await expect(pageB.getByLabel('firm tofu')).toBeChecked({ timeout: 10000 });
});
```

### Playwright Configuration

```typescript
// playwright.config.ts
export default defineConfig({
  testDir: './tests',
  timeout: 30000,
  retries: 1,
  use: {
    baseURL: process.env.E2E_BASE_URL || 'http://localhost:5173',
    screenshot: 'only-on-failure',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile-chrome', use: { ...devices['Pixel 5'] } },
  ],
});
```

---

## Coverage Configuration

### .NET (Coverlet)

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <CollectCoverage>true</CollectCoverage>
  <CoverletOutputFormat>cobertura</CoverletOutputFormat>
  <Threshold>80</Threshold>
  <ThresholdType>line</ThresholdType>
  <ThresholdStat>total</ThresholdStat>
</PropertyGroup>
```

### React (Vitest)

```typescript
// vitest.config.ts
export default defineConfig({
  test: {
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov', 'cobertura'],
      thresholds: {
        lines: 80,
        functions: 80,
        branches: 80,
        statements: 80,
      },
      exclude: [
        'src/types/**',
        'src/vite-env.d.ts',
        '**/*.test.*',
      ],
    },
  },
});
```

---

## CI Coverage Gate

```yaml
# In GitHub Actions workflow
- name: Backend tests with coverage
  run: |
    dotnet test --collect:"XPlat Code Coverage" \
      --results-directory ./coverage
    # Fail if below 80%
    dotnet tool run reportgenerator \
      -reports:./coverage/**/coverage.cobertura.xml \
      -targetdir:./coverage/report \
      -reporttypes:TextSummary

- name: Frontend tests with coverage
  working-directory: ./frontend
  run: |
    npm run test -- --coverage --reporter=default
    # vitest.config.ts thresholds enforce 80% — exits non-zero if below
```

---

## What to Test (Priority Guide)

### Must Test (Critical Safety)
- Allergy validation: tree nut detection, cross-contamination
- Dietary restriction enforcement: vegetarian, gluten-free
- Constraint engine: no-cook nights, repeat prevention
- Auth: JWT validation, family-scoped access
- Grocery list concurrency: version conflicts, optimistic updates
- Input validation: all FluentValidation rules

### Should Test (Core Features)
- Meal plan generation flow
- Recipe CRUD operations
- Chatbot function calling execution
- Grocery list generation from meal plan
- Favorites toggle

### Nice to Test (UI Polish)
- Responsive layouts at breakpoints
- PWA install flow
- Toast notifications
- Skeleton loading states
