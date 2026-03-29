using ThcMealPlanner.Core.Data;
using ThcMealPlanner.Api.Recipes;

namespace ThcMealPlanner.Api.MealPlans;

public interface IMealPlanService
{
    Task<MealPlanDocument?> GetCurrentAsync(string familyId, CancellationToken cancellationToken = default);

    Task<MealPlanDocument?> GetByWeekAsync(string familyId, string weekStartDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealPlanDocument>> GetHistoryAsync(string familyId, int limit = 10, CancellationToken cancellationToken = default);

    Task<MealPlanDocument> CreateAsync(string familyId, string userId, CreateMealPlanRequest request, CancellationToken cancellationToken = default);

    Task<MealPlanDocument> GenerateAsync(string familyId, string userId, GenerateMealPlanRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealSwapSuggestion>> SuggestSwapOptionsAsync(
        string familyId,
        string weekStartDate,
        string day,
        string mealType,
        int limit = 5,
        CancellationToken cancellationToken = default);

    Task<MealPlanDocument?> UpdateAsync(string familyId, string weekStartDate, UpdateMealPlanRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string familyId, string weekStartDate, CancellationToken cancellationToken = default);
}

public sealed class MealPlanService : IMealPlanService
{
    private static readonly string[] OrderedDays =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    private readonly IDynamoDbRepository<MealPlanDocument> _planRepository;
    private readonly IDynamoDbRepository<FavoriteRecipeDocument> _favoriteRepository;
    private readonly IRecipeService _recipeService;
    private readonly IConstraintEngine _constraintEngine;
    private readonly IMealPlanAiService _mealPlanAiService;

    public MealPlanService(
        IDynamoDbRepository<MealPlanDocument> planRepository,
        IDynamoDbRepository<FavoriteRecipeDocument> favoriteRepository,
        IRecipeService recipeService,
        IConstraintEngine constraintEngine,
        IMealPlanAiService mealPlanAiService)
    {
        _planRepository = planRepository;
        _favoriteRepository = favoriteRepository;
        _recipeService = recipeService;
        _constraintEngine = constraintEngine;
        _mealPlanAiService = mealPlanAiService;
    }

    public async Task<MealPlanDocument?> GetCurrentAsync(string familyId, CancellationToken cancellationToken = default)
    {
        var plans = await _planRepository.QueryByPartitionKeyAsync(
            $"FAMILY#{familyId}",
            cancellationToken: cancellationToken);

        return plans
            .Where(p => string.Equals(p.Status, "active", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.WeekStartDate, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public async Task<MealPlanDocument?> GetByWeekAsync(string familyId, string weekStartDate, CancellationToken cancellationToken = default)
    {
        return await _planRepository.GetAsync(ToPlanKey(familyId, weekStartDate), cancellationToken);
    }

    public async Task<IReadOnlyList<MealPlanDocument>> GetHistoryAsync(string familyId, int limit = 10, CancellationToken cancellationToken = default)
    {
        var plans = await _planRepository.QueryByPartitionKeyAsync(
            $"FAMILY#{familyId}",
            cancellationToken: cancellationToken);

        return plans
            .OrderByDescending(p => p.WeekStartDate, StringComparer.Ordinal)
            .Take(limit)
            .ToList();
    }

    public async Task<MealPlanDocument> CreateAsync(string familyId, string userId, CreateMealPlanRequest request, CancellationToken cancellationToken = default)
    {
        var recipes = await _recipeService.ListByFamilyAsync(familyId, cancellationToken);
        var recipeMap = recipes.ToDictionary(r => r.RecipeId, StringComparer.OrdinalIgnoreCase);

        var slots = BuildSlots(request.Meals, recipeMap);
        var violations = CountViolations(slots, recipeMap);
        var qualityScore = _constraintEngine.ScorePlan(slots, violations);
        var nutritionalSummary = ComputeNutritionalSummary(slots);
        var now = DateTimeOffset.UtcNow;

        var plan = new MealPlanDocument
        {
            FamilyId = familyId,
            WeekStartDate = request.WeekStartDate,
            Status = "active",
            Meals = slots,
            NutritionalSummary = nutritionalSummary,
            ConstraintsUsed = "v1",
            GeneratedBy = "manual",
            QualityScore = qualityScore,
            CreatedAt = now,
            UpdatedAt = now,
            TTL = ComputeTTL(request.WeekStartDate)
        };

        await _planRepository.PutAsync(ToPlanKey(familyId, request.WeekStartDate), plan, cancellationToken);

        return plan;
    }

    public async Task<MealPlanDocument> GenerateAsync(string familyId, string userId, GenerateMealPlanRequest request, CancellationToken cancellationToken = default)
    {
        var recipes = await _recipeService.ListByFamilyAsync(familyId, cancellationToken);
        var userFavorites = await _favoriteRepository.QueryByPartitionKeyAsync($"USER#{userId}", cancellationToken: cancellationToken);
        var favoriteRecipeIds = userFavorites
            .Select(favorite => favorite.RecipeId)
            .Where(recipeId => !string.IsNullOrWhiteSpace(recipeId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (recipes.Count == 0)
        {
            var emptyPlan = CreateEmptyPlan(familyId, request.WeekStartDate, "ai");
            await _planRepository.PutAsync(ToPlanKey(familyId, request.WeekStartDate), emptyPlan, cancellationToken);
            return emptyPlan;
        }

        var recipesByCategory = recipes
            .GroupBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var mealTypes = new[] { "breakfast", "lunch", "dinner" };
        var slotOrder = OrderedDays
            .SelectMany(day => mealTypes.Select(mealType => (Day: day, MealType: mealType)))
            .ToList();

        var aiRecipeQueue = (await _mealPlanAiService.GenerateRecipeIdsAsync(
            request.WeekStartDate,
            slotOrder,
            recipes,
            cancellationToken)).ToList();

        var random = new Random();
        var usedRecipeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recipeUsageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var previousRecipeByMealType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var slots = new List<MealSlotDocument>();
        var violations = 0;

        foreach (var day in OrderedDays)
        {
            foreach (var mealType in mealTypes)
            {
                var recipe = TryTakeAiRecipe(
                    aiRecipeQueue,
                    recipes,
                    mealType,
                    usedRecipeIds,
                    recipesByCategory,
                    favoriteRecipeIds,
                    recipeUsageCounts,
                    previousRecipeByMealType.TryGetValue(mealType, out var previousForMealType) ? previousForMealType : null,
                    random);

                if (recipe is null)
                {
                    continue;
                }

                usedRecipeIds.Add(recipe.RecipeId);
                recipeUsageCounts[recipe.RecipeId] = recipeUsageCounts.GetValueOrDefault(recipe.RecipeId) + 1;
                previousRecipeByMealType[mealType] = recipe.RecipeId;

                var result = _constraintEngine.ValidateMealSlot(day, mealType, recipe);
                if (!result.IsValid)
                {
                    violations += result.Violations.Count;
                }

                slots.Add(new MealSlotDocument
                {
                    Day = day,
                    MealType = mealType,
                    RecipeId = recipe.RecipeId,
                    RecipeName = recipe.Name,
                    Servings = recipe.Servings,
                    PrepTime = recipe.PrepTimeMinutes,
                    CookTime = recipe.CookTimeMinutes,
                    NutritionalInfo = ToNutritionalInfo(recipe)
                });
            }
        }

        var qualityScore = _constraintEngine.ScorePlan(slots, violations);
        var nutritionalSummary = ComputeNutritionalSummary(slots);
        var now = DateTimeOffset.UtcNow;

        var plan = new MealPlanDocument
        {
            FamilyId = familyId,
            WeekStartDate = request.WeekStartDate,
            Status = "active",
            Meals = slots,
            NutritionalSummary = nutritionalSummary,
            ConstraintsUsed = "v1",
            GeneratedBy = "ai",
            QualityScore = qualityScore,
            CreatedAt = now,
            UpdatedAt = now,
            TTL = ComputeTTL(request.WeekStartDate)
        };

        await _planRepository.PutAsync(ToPlanKey(familyId, request.WeekStartDate), plan, cancellationToken);

        return plan;
    }

    public async Task<IReadOnlyList<MealSwapSuggestion>> SuggestSwapOptionsAsync(
        string familyId,
        string weekStartDate,
        string day,
        string mealType,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 10);
        var plan = await _planRepository.GetAsync(ToPlanKey(familyId, weekStartDate), cancellationToken);
        if (plan is null)
        {
            return [];
        }

        var currentSlot = plan.Meals.FirstOrDefault(m =>
            string.Equals(m.Day, day, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(m.MealType, mealType, StringComparison.OrdinalIgnoreCase));

        var recipes = await _recipeService.ListByFamilyAsync(familyId, cancellationToken);
        var typedCandidates = recipes
            .Where(r => string.Equals(r.Category, mealType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var candidates = typedCandidates.Count > 0 ? typedCandidates : recipes.ToList();

        if (!string.IsNullOrWhiteSpace(currentSlot?.RecipeId))
        {
            candidates = candidates
                .Where(r => !string.Equals(r.RecipeId, currentSlot.RecipeId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var suggestions = candidates
            .Select(recipe =>
            {
                var validation = _constraintEngine.ValidateMealSlot(day, mealType, recipe);
                var totalMinutes = (recipe.PrepTimeMinutes ?? 0) + (recipe.CookTimeMinutes ?? 0);
                var score = Math.Max(0, 100 - totalMinutes - (validation.IsValid ? 0 : 35));

                return new MealSwapSuggestion
                {
                    RecipeId = recipe.RecipeId,
                    RecipeName = recipe.Name,
                    PrepTime = recipe.PrepTimeMinutes,
                    CookTime = recipe.CookTimeMinutes,
                    ConstraintSafe = validation.IsValid,
                    Score = score,
                    Notes = validation.Violations.Select(v => v.Detail).ToList()
                };
            })
            .OrderByDescending(s => s.ConstraintSafe)
            .ThenByDescending(s => s.Score)
            .ThenBy(s => s.RecipeName, StringComparer.OrdinalIgnoreCase)
            .Take(normalizedLimit)
            .ToList();

        var rankedIds = await _mealPlanAiService.RankSwapCandidatesAsync(
            day,
            mealType,
            currentSlot?.RecipeId,
            candidates,
            cancellationToken);

        if (rankedIds.Count == 0)
        {
            return suggestions;
        }

        var rankedOrder = rankedIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index, StringComparer.OrdinalIgnoreCase);

        suggestions = suggestions
            .OrderBy(s => rankedOrder.TryGetValue(s.RecipeId, out var index) ? index : int.MaxValue)
            .ThenByDescending(s => s.ConstraintSafe)
            .ThenByDescending(s => s.Score)
            .ToList();

        return suggestions;
    }

    private static RecipeDocument? TryTakeAiRecipe(
        List<string> aiRecipeQueue,
        IReadOnlyList<RecipeDocument> recipes,
        string mealType,
        HashSet<string> usedRecipeIds,
        Dictionary<string, List<RecipeDocument>> recipesByCategory,
        HashSet<string> favoriteRecipeIds,
        Dictionary<string, int> recipeUsageCounts,
        string? previousRecipeForMealType,
        Random random)
    {
        if (aiRecipeQueue.Count > 0)
        {
            var typedCandidates = new List<(int QueueIndex, RecipeDocument Recipe)>();
            for (var i = 0; i < aiRecipeQueue.Count; i++)
            {
                var candidateId = aiRecipeQueue[i];
                var candidate = recipes.FirstOrDefault(r => string.Equals(r.RecipeId, candidateId, StringComparison.OrdinalIgnoreCase));

                if (candidate is null || usedRecipeIds.Contains(candidate.RecipeId))
                {
                    continue;
                }

                if (string.Equals(candidate.Category, mealType, StringComparison.OrdinalIgnoreCase))
                {
                    typedCandidates.Add((i, candidate));
                }
            }

            if (typedCandidates.Count > 0)
            {
                var selected = SelectBestCandidate(
                    typedCandidates.Select(candidate => candidate.Recipe),
                    favoriteRecipeIds,
                    recipeUsageCounts,
                    previousRecipeForMealType,
                    random);

                var queueIndex = typedCandidates
                    .First(candidate => string.Equals(candidate.Recipe.RecipeId, selected.RecipeId, StringComparison.OrdinalIgnoreCase))
                    .QueueIndex;

                aiRecipeQueue.RemoveAt(queueIndex);
                return selected;
            }

            var anyCandidates = new List<(int QueueIndex, RecipeDocument Recipe)>();
            for (var i = 0; i < aiRecipeQueue.Count; i++)
            {
                var candidateId = aiRecipeQueue[i];
                var candidate = recipes.FirstOrDefault(r => string.Equals(r.RecipeId, candidateId, StringComparison.OrdinalIgnoreCase));
                if (candidate is null || usedRecipeIds.Contains(candidate.RecipeId))
                {
                    continue;
                }

                anyCandidates.Add((i, candidate));
            }

            if (anyCandidates.Count > 0)
            {
                var selected = SelectBestCandidate(
                    anyCandidates.Select(candidate => candidate.Recipe),
                    favoriteRecipeIds,
                    recipeUsageCounts,
                    previousRecipeForMealType,
                    random);

                var queueIndex = anyCandidates
                    .First(candidate => string.Equals(candidate.Recipe.RecipeId, selected.RecipeId, StringComparison.OrdinalIgnoreCase))
                    .QueueIndex;

                aiRecipeQueue.RemoveAt(queueIndex);
                return selected;
            }
        }

        var candidates = recipesByCategory.TryGetValue(mealType, out var pool)
            ? pool.Where(r => !usedRecipeIds.Contains(r.RecipeId)).ToList()
            : [];

        if (candidates.Count == 0)
        {
            candidates = recipes.Where(r => !usedRecipeIds.Contains(r.RecipeId)).ToList();
        }

        if (candidates.Count == 0)
        {
            candidates = recipesByCategory.TryGetValue(mealType, out var anyPool)
                ? anyPool
                : [.. recipes];
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return SelectBestCandidate(candidates, favoriteRecipeIds, recipeUsageCounts, previousRecipeForMealType, random);
    }

    private static RecipeDocument SelectBestCandidate(
        IEnumerable<RecipeDocument> candidates,
        HashSet<string> favoriteRecipeIds,
        Dictionary<string, int> recipeUsageCounts,
        string? previousRecipeForMealType,
        Random random)
    {
        return candidates
            .Select(recipe => new
            {
                Recipe = recipe,
                IsPreviousForMealType = !string.IsNullOrWhiteSpace(previousRecipeForMealType)
                    && string.Equals(recipe.RecipeId, previousRecipeForMealType, StringComparison.OrdinalIgnoreCase),
                UsageCount = recipeUsageCounts.GetValueOrDefault(recipe.RecipeId),
                IsFavorite = favoriteRecipeIds.Contains(recipe.RecipeId),
                TieBreaker = random.Next(0, 1_000_000)
            })
            .OrderBy(candidate => candidate.IsPreviousForMealType)
            .ThenBy(candidate => candidate.UsageCount)
            .ThenByDescending(candidate => candidate.IsFavorite)
            .ThenBy(candidate => candidate.TieBreaker)
            .Select(candidate => candidate.Recipe)
            .First();
    }

    public async Task<MealPlanDocument?> UpdateAsync(string familyId, string weekStartDate, UpdateMealPlanRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _planRepository.GetAsync(ToPlanKey(familyId, weekStartDate), cancellationToken);
        if (existing is null) return null;

        var updatedMeals = existing.Meals;

        if (request.Meals is { Count: > 0 })
        {
            var recipes = await _recipeService.ListByFamilyAsync(familyId, cancellationToken);
            var recipeMap = recipes.ToDictionary(r => r.RecipeId, StringComparer.OrdinalIgnoreCase);

            var newSlots = BuildSlots(request.Meals, recipeMap);

            // Merge: replace matching day+mealType slots, keep unaffected ones
            var updatedMap = existing.Meals
                .ToDictionary(s => $"{s.Day}:{s.MealType}", s => s, StringComparer.OrdinalIgnoreCase);

            foreach (var slot in newSlots)
            {
                updatedMap[$"{slot.Day}:{slot.MealType}"] = slot;
            }

            updatedMeals = [.. updatedMap.Values];

            var violations = CountViolations(updatedMeals, recipeMap);
            var qualityScore = _constraintEngine.ScorePlan(updatedMeals, violations);
            var nutritionalSummary = ComputeNutritionalSummary(updatedMeals);

            var updated = new MealPlanDocument
            {
                FamilyId = existing.FamilyId,
                WeekStartDate = existing.WeekStartDate,
                Status = request.Status ?? existing.Status,
                Meals = updatedMeals,
                NutritionalSummary = nutritionalSummary,
                ConstraintsUsed = existing.ConstraintsUsed,
                GeneratedBy = existing.GeneratedBy,
                QualityScore = qualityScore,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow,
                TTL = existing.TTL
            };

            await _planRepository.PutAsync(ToPlanKey(familyId, weekStartDate), updated, cancellationToken);
            return updated;
        }
        else if (request.Status is not null)
        {
            var updated = new MealPlanDocument
            {
                FamilyId = existing.FamilyId,
                WeekStartDate = existing.WeekStartDate,
                Status = request.Status,
                Meals = existing.Meals,
                NutritionalSummary = existing.NutritionalSummary,
                ConstraintsUsed = existing.ConstraintsUsed,
                GeneratedBy = existing.GeneratedBy,
                QualityScore = existing.QualityScore,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow,
                TTL = existing.TTL
            };

            await _planRepository.PutAsync(ToPlanKey(familyId, weekStartDate), updated, cancellationToken);
            return updated;
        }

        return existing;
    }

    public async Task<bool> DeleteAsync(string familyId, string weekStartDate, CancellationToken cancellationToken = default)
    {
        var existing = await _planRepository.GetAsync(ToPlanKey(familyId, weekStartDate), cancellationToken);
        if (existing is null) return false;

        await _planRepository.DeleteAsync(ToPlanKey(familyId, weekStartDate), cancellationToken);
        return true;
    }

    private List<MealSlotDocument> BuildSlots(
        IEnumerable<CreateMealSlotRequest> requested,
        Dictionary<string, RecipeDocument> recipeMap)
    {
        return requested.Select(slot =>
        {
            recipeMap.TryGetValue(slot.RecipeId, out var recipe);

            return new MealSlotDocument
            {
                Day = slot.Day,
                MealType = slot.MealType,
                RecipeId = slot.RecipeId,
                RecipeName = recipe?.Name ?? slot.RecipeId,
                Servings = slot.Servings ?? recipe?.Servings,
                PrepTime = recipe?.PrepTimeMinutes,
                CookTime = recipe?.CookTimeMinutes,
                NutritionalInfo = recipe is null ? null : ToNutritionalInfo(recipe)
            };
        }).ToList();
    }

    private int CountViolations(IEnumerable<MealSlotDocument> slots, Dictionary<string, RecipeDocument> recipeMap)
    {
        var count = 0;
        foreach (var slot in slots)
        {
            if (!recipeMap.TryGetValue(slot.RecipeId, out var recipe)) continue;
            var result = _constraintEngine.ValidateMealSlot(slot.Day, slot.MealType, recipe);
            count += result.Violations.Count;
        }
        return count;
    }

    private static MealNutritionalInfo? ToNutritionalInfo(RecipeDocument recipe)
    {
        if (recipe.Nutrition is null) return null;

        return new MealNutritionalInfo
        {
            Calories = recipe.Nutrition.Calories,
            Protein = recipe.Nutrition.Protein,
            Carbohydrates = recipe.Nutrition.Carbohydrates,
            Fat = recipe.Nutrition.Fat,
            Sodium = recipe.Nutrition.Sodium
        };
    }

    private static NutritionalSummaryDocument? ComputeNutritionalSummary(IReadOnlyList<MealSlotDocument> meals)
    {
        var slotsWithNutrition = meals.Where(m => m.NutritionalInfo is not null).ToList();
        if (slotsWithNutrition.Count == 0) return null;

        // Group by day and average within days, then average across days
        var byDay = slotsWithNutrition
            .GroupBy(m => m.Day, StringComparer.OrdinalIgnoreCase)
            .Select(dayGroup => new
            {
                DailyCalories = dayGroup.Sum(m => m.NutritionalInfo!.Calories ?? 0),
                DailyProtein = dayGroup.Sum(m => m.NutritionalInfo!.Protein ?? 0),
                DailyCarbs = dayGroup.Sum(m => m.NutritionalInfo!.Carbohydrates ?? 0),
                DailyFat = dayGroup.Sum(m => m.NutritionalInfo!.Fat ?? 0)
            }).ToList();

        if (byDay.Count == 0) return null;

        return new NutritionalSummaryDocument
        {
            DailyAverages = new DailyAveragesDocument
            {
                Calories = (int)Math.Round(byDay.Average(d => d.DailyCalories)),
                Protein = (int)Math.Round(byDay.Average(d => d.DailyProtein)),
                Carbohydrates = (int)Math.Round(byDay.Average(d => d.DailyCarbs)),
                Fat = (int)Math.Round(byDay.Average(d => d.DailyFat))
            }
        };
    }

    private static MealPlanDocument CreateEmptyPlan(string familyId, string weekStartDate, string generatedBy)
    {
        var now = DateTimeOffset.UtcNow;
        return new MealPlanDocument
        {
            FamilyId = familyId,
            WeekStartDate = weekStartDate,
            Status = "active",
            Meals = [],
            ConstraintsUsed = "v1",
            GeneratedBy = generatedBy,
            QualityScore = new QualityScoreDocument
            {
                Overall = 0,
                VarietyScore = 0,
                DiversityScore = 0,
                ConstraintViolations = 0,
                Grade = "F"
            },
            CreatedAt = now,
            UpdatedAt = now,
            TTL = ComputeTTL(weekStartDate)
        };
    }

    private static long? ComputeTTL(string weekStartDate)
    {
        if (!DateOnly.TryParse(weekStartDate, out var date)) return null;

        // 90 days after weekStart + 7 days buffer
        return new DateTimeOffset(date.AddDays(97).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
    }

    private static DynamoDbKey ToPlanKey(string familyId, string weekStartDate)
        => new($"FAMILY#{familyId}", $"PLAN#{weekStartDate}");
}
