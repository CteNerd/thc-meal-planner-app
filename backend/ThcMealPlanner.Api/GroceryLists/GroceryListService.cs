using System.Globalization;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Api.Recipes;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Api.GroceryLists;

public interface IGroceryListService
{
    Task<GroceryListDocument?> GetCurrentAsync(string familyId, CancellationToken cancellationToken = default);

    Task<GroceryListDocument> GenerateAsync(
        string familyId,
        string userId,
        string? userName,
        GenerateGroceryListRequest request,
        CancellationToken cancellationToken = default);

    Task<GroceryItemMutationResult> ToggleItemAsync(
        string familyId,
        string itemId,
        string userId,
        string? userName,
        ToggleGroceryItemRequest request,
        CancellationToken cancellationToken = default);

    Task<GroceryItemMutationResult> AddItemAsync(
        string familyId,
        AddGroceryItemRequest request,
        CancellationToken cancellationToken = default);

    Task<GroceryItemMutationResult> SetInStockAsync(
        string familyId,
        string itemId,
        SetInStockRequest request,
        CancellationToken cancellationToken = default);

    Task<GroceryItemMutationResult> RemoveItemAsync(
        string familyId,
        string itemId,
        RemoveGroceryItemRequest request,
        CancellationToken cancellationToken = default);

    Task<GroceryListPollResult> PollAsync(
        string familyId,
        DateTimeOffset? since,
        CancellationToken cancellationToken = default);

    Task<PantryStaplesDocument> GetPantryStaplesAsync(string familyId, CancellationToken cancellationToken = default);

    Task<PantryStaplesDocument> ReplacePantryStaplesAsync(
        string familyId,
        ReplacePantryStaplesRequest request,
        CancellationToken cancellationToken = default);

    Task<PantryStaplesDocument> AddPantryStapleAsync(
        string familyId,
        AddPantryStapleItemRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeletePantryStapleAsync(string familyId, string name, CancellationToken cancellationToken = default);
}

public sealed class GroceryListService : IGroceryListService
{
    private static readonly TimeSpan CompletedItemRetention = TimeSpan.FromDays(7);
    private static readonly string[] DefaultSectionOrder =
        ["produce", "protein", "dairy", "pantry", "frozen", "household", "other"];

    private readonly IDynamoDbRepository<GroceryListDocument> _groceryListRepository;
    private readonly IDynamoDbRepository<PantryStaplesDocument> _pantryRepository;
    private readonly IDynamoDbRepository<MealPlanDocument> _mealPlanRepository;
    private readonly IDynamoDbRepository<RecipeDocument> _recipeRepository;

    public GroceryListService(
        IDynamoDbRepository<GroceryListDocument> groceryListRepository,
        IDynamoDbRepository<PantryStaplesDocument> pantryRepository,
        IDynamoDbRepository<MealPlanDocument> mealPlanRepository,
        IDynamoDbRepository<RecipeDocument> recipeRepository)
    {
        _groceryListRepository = groceryListRepository;
        _pantryRepository = pantryRepository;
        _mealPlanRepository = mealPlanRepository;
        _recipeRepository = recipeRepository;
    }

    public async Task<GroceryListDocument?> GetCurrentAsync(string familyId, CancellationToken cancellationToken = default)
    {
        var list = await _groceryListRepository.GetAsync(ToListKey(familyId), cancellationToken);
        if (list is null)
        {
            return null;
        }

        var cleaned = await CleanupCompletedItemsAsync(list, cancellationToken);
        return cleaned;
    }

    public async Task<GroceryListDocument> GenerateAsync(
        string familyId,
        string userId,
        string? userName,
        GenerateGroceryListRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await _groceryListRepository.GetAsync(ToListKey(familyId), cancellationToken);

        var activePlan = await ResolvePlanAsync(familyId, request.WeekStartDate, cancellationToken);
        var recipes = await _recipeRepository.QueryByPartitionKeyAsync($"FAMILY#{familyId}", cancellationToken: cancellationToken);
        var recipeById = recipes.ToDictionary(r => r.RecipeId, StringComparer.OrdinalIgnoreCase);

        var pantry = await _pantryRepository.GetAsync(ToPantryKey(familyId), cancellationToken);
        var stapleNames = pantry?.Items
            .Select(i => NormalizeName(i.Name))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var nextItems = BuildGeneratedItems(activePlan, recipeById, stapleNames);

        if (!request.ClearExisting && existing is not null)
        {
            nextItems = MergeWithExisting(nextItems, existing.Items);
        }

        var version = (existing?.Version ?? 0) + 1;
        var createdAt = existing?.CreatedAt ?? now;

        var nextList = new GroceryListDocument
        {
            FamilyId = familyId,
            ListId = GroceryListConstants.ActiveListId,
            Items = nextItems,
            Version = version,
            CreatedAt = createdAt,
            UpdatedAt = now,
            Progress = ComputeProgress(nextItems)
        };

        await _groceryListRepository.PutAsync(ToListKey(familyId), nextList, cancellationToken);

        return nextList;
    }

    public async Task<GroceryItemMutationResult> ToggleItemAsync(
        string familyId,
        string itemId,
        string userId,
        string? userName,
        ToggleGroceryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentAsync(familyId, cancellationToken);
        if (current is null)
        {
            return GroceryItemMutationResult.NotFoundList;
        }

        if (current.Version != request.Version)
        {
            return GroceryItemMutationResult.Conflict;
        }

        var existingItem = current.Items.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.Ordinal));
        if (existingItem is null)
        {
            return GroceryItemMutationResult.NotFoundItem;
        }

        var now = DateTimeOffset.UtcNow;
        var toggledTo = !existingItem.CheckedOff;
        var updatedItem = existingItem with
        {
            CheckedOff = toggledTo,
            CheckedOffBy = toggledTo ? userId : null,
            CheckedOffByName = toggledTo ? userName : null,
            CheckedOffTimestamp = toggledTo ? now : null,
            CompletedTTL = toggledTo ? ToUnixTimeSeconds(now + CompletedItemRetention) : null
        };

        var updatedItems = current.Items
            .Select(i => string.Equals(i.Id, itemId, StringComparison.Ordinal) ? updatedItem : i)
            .ToList();

        var updatedList = current with
        {
            Items = updatedItems,
            Version = current.Version + 1,
            UpdatedAt = now,
            Progress = ComputeProgress(updatedItems)
        };

        await _groceryListRepository.PutAsync(ToListKey(familyId), updatedList, cancellationToken);

        return GroceryItemMutationResult.Success(updatedItem, updatedList);
    }

    public async Task<GroceryItemMutationResult> AddItemAsync(
        string familyId,
        AddGroceryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentAsync(familyId, cancellationToken);
        if (current is null)
        {
            return GroceryItemMutationResult.NotFoundList;
        }

        if (current.Version != request.Version)
        {
            return GroceryItemMutationResult.Conflict;
        }

        var now = DateTimeOffset.UtcNow;
        var item = new GroceryItemDocument
        {
            Id = $"item_{Guid.NewGuid().ToString("N")[..8]}",
            Name = request.Name.Trim(),
            Section = request.Section.Trim(),
            Quantity = request.Quantity ?? 1,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
            MealAssociations = [],
            CheckedOff = false,
            InStock = false
        };

        var updatedItems = current.Items
            .Append(item)
            .OrderBy(i => i.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var updatedList = current with
        {
            Items = updatedItems,
            Version = current.Version + 1,
            UpdatedAt = now,
            Progress = ComputeProgress(updatedItems)
        };

        await _groceryListRepository.PutAsync(ToListKey(familyId), updatedList, cancellationToken);
        return GroceryItemMutationResult.Success(item, updatedList);
    }

    public async Task<GroceryItemMutationResult> SetInStockAsync(
        string familyId,
        string itemId,
        SetInStockRequest request,
        CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentAsync(familyId, cancellationToken);
        if (current is null)
        {
            return GroceryItemMutationResult.NotFoundList;
        }

        if (current.Version != request.Version)
        {
            return GroceryItemMutationResult.Conflict;
        }

        var existingItem = current.Items.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.Ordinal));
        if (existingItem is null)
        {
            return GroceryItemMutationResult.NotFoundItem;
        }

        var now = DateTimeOffset.UtcNow;
        var updatedItem = existingItem with { InStock = request.InStock };
        var updatedItems = current.Items
            .Select(i => string.Equals(i.Id, itemId, StringComparison.Ordinal) ? updatedItem : i)
            .ToList();

        var updatedList = current with
        {
            Items = updatedItems,
            Version = current.Version + 1,
            UpdatedAt = now,
            Progress = ComputeProgress(updatedItems)
        };

        await _groceryListRepository.PutAsync(ToListKey(familyId), updatedList, cancellationToken);
        return GroceryItemMutationResult.Success(updatedItem, updatedList);
    }

    public async Task<GroceryItemMutationResult> RemoveItemAsync(
        string familyId,
        string itemId,
        RemoveGroceryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentAsync(familyId, cancellationToken);
        if (current is null)
        {
            return GroceryItemMutationResult.NotFoundList;
        }

        if (current.Version != request.Version)
        {
            return GroceryItemMutationResult.Conflict;
        }

        var existingItem = current.Items.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.Ordinal));
        if (existingItem is null)
        {
            return GroceryItemMutationResult.NotFoundItem;
        }

        var now = DateTimeOffset.UtcNow;
        var updatedItems = current.Items
            .Where(i => !string.Equals(i.Id, itemId, StringComparison.Ordinal))
            .ToList();

        var updatedList = current with
        {
            Items = updatedItems,
            Version = current.Version + 1,
            UpdatedAt = now,
            Progress = ComputeProgress(updatedItems)
        };

        await _groceryListRepository.PutAsync(ToListKey(familyId), updatedList, cancellationToken);
        return GroceryItemMutationResult.Success(existingItem, updatedList);
    }

    public async Task<GroceryListPollResult> PollAsync(
        string familyId,
        DateTimeOffset? since,
        CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentAsync(familyId, cancellationToken);
        if (current is null)
        {
            return GroceryListPollResult.NotFound;
        }

        if (since.HasValue && current.UpdatedAt <= since.Value)
        {
            return GroceryListPollResult.NoChanges;
        }

        var response = new GroceryListPollResponse
        {
            HasChanges = true,
            Version = current.Version,
            UpdatedAt = current.UpdatedAt,
            Changes = current.Items.Select(i => new GroceryListChangeDocument
            {
                ItemId = i.Id,
                Action = "updated",
                Item = i
            }).ToList()
        };

        return GroceryListPollResult.HasChanges(response);
    }

    public async Task<PantryStaplesDocument> GetPantryStaplesAsync(string familyId, CancellationToken cancellationToken = default)
    {
        var pantry = await _pantryRepository.GetAsync(ToPantryKey(familyId), cancellationToken);
        return pantry ?? new PantryStaplesDocument
        {
            FamilyId = familyId,
            Items = [],
            PreferredSectionOrder = [.. DefaultSectionOrder],
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<PantryStaplesDocument> ReplacePantryStaplesAsync(
        string familyId,
        ReplacePantryStaplesRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = new PantryStaplesDocument
        {
            FamilyId = familyId,
            Items = request.Items
                .Select(i => new PantryStapleItemDocument
                {
                    Name = i.Name.Trim(),
                    Section = string.IsNullOrWhiteSpace(i.Section) ? null : i.Section.Trim()
                })
                .DistinctBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            PreferredSectionOrder = NormalizeSectionOrder(request.PreferredSectionOrder),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _pantryRepository.PutAsync(ToPantryKey(familyId), updated, cancellationToken);
        return updated;
    }

    public async Task<PantryStaplesDocument> AddPantryStapleAsync(
        string familyId,
        AddPantryStapleItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var pantry = await GetPantryStaplesAsync(familyId, cancellationToken);
        var nextItems = pantry.Items
            .Where(i => !string.Equals(i.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            .Append(new PantryStapleItemDocument
            {
                Name = request.Name.Trim(),
                Section = string.IsNullOrWhiteSpace(request.Section) ? null : request.Section.Trim()
            })
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var updated = pantry with
        {
            Items = nextItems,
            PreferredSectionOrder = NormalizeSectionOrder(pantry.PreferredSectionOrder),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _pantryRepository.PutAsync(ToPantryKey(familyId), updated, cancellationToken);
        return updated;
    }

    public async Task<bool> DeletePantryStapleAsync(string familyId, string name, CancellationToken cancellationToken = default)
    {
        var pantry = await _pantryRepository.GetAsync(ToPantryKey(familyId), cancellationToken);
        if (pantry is null)
        {
            return false;
        }

        var nextItems = pantry.Items
            .Where(i => !string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (nextItems.Count == pantry.Items.Count)
        {
            return false;
        }

        var updated = pantry with
        {
            Items = nextItems,
            PreferredSectionOrder = NormalizeSectionOrder(pantry.PreferredSectionOrder),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _pantryRepository.PutAsync(ToPantryKey(familyId), updated, cancellationToken);
        return true;
    }

    private async Task<MealPlanDocument?> ResolvePlanAsync(
        string familyId,
        string? weekStartDate,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(weekStartDate))
        {
            return await _mealPlanRepository.GetAsync(ToMealPlanKey(familyId, weekStartDate), cancellationToken);
        }

        var plans = await _mealPlanRepository.QueryByPartitionKeyAsync($"FAMILY#{familyId}", cancellationToken: cancellationToken);
        return plans
            .Where(p => string.Equals(p.Status, "active", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.WeekStartDate, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static List<GroceryItemDocument> BuildGeneratedItems(
        MealPlanDocument? plan,
        IReadOnlyDictionary<string, RecipeDocument> recipeById,
        HashSet<string> stapleNames)
    {
        if (plan is null)
        {
            return [];
        }

        var aggregate = new Dictionary<string, AggregatedItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var slot in plan.Meals)
        {
            if (!recipeById.TryGetValue(slot.RecipeId, out var recipe))
            {
                continue;
            }

            foreach (var ingredient in recipe.Ingredients)
            {
                if (string.IsNullOrWhiteSpace(ingredient.Name))
                {
                    continue;
                }

                var normalizedName = NormalizeName(ingredient.Name);
                var unit = ingredient.Unit?.Trim();
                var key = $"{normalizedName}|{unit}";

                if (!aggregate.TryGetValue(key, out var item))
                {
                    item = new AggregatedItem
                    {
                        DisplayName = ingredient.Name.Trim(),
                        Section = string.IsNullOrWhiteSpace(ingredient.Section) ? "pantry" : ingredient.Section!.Trim(),
                        Unit = unit,
                        Quantity = 0,
                        InStock = stapleNames.Contains(normalizedName)
                    };
                    aggregate[key] = item;
                }

                item.Quantity += ParseQuantity(ingredient.Quantity);
                item.Associations.Add(new MealAssociationDocument
                {
                    RecipeId = recipe.RecipeId,
                    RecipeName = recipe.Name,
                    MealDay = slot.Day
                });
            }
        }

        return aggregate.Values
            .Select(item => new GroceryItemDocument
            {
                Id = $"item_{Guid.NewGuid().ToString("N")[..8]}",
                Name = item.DisplayName,
                Section = item.Section,
                Quantity = item.Quantity,
                Unit = item.Unit,
                MealAssociations = item.Associations
                    .DistinctBy(a => $"{a.RecipeId}:{a.MealDay}", StringComparer.OrdinalIgnoreCase)
                    .OrderBy(a => a.MealDay, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(a => a.RecipeName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                CheckedOff = false,
                InStock = item.InStock
            })
            .OrderBy(i => i.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<GroceryItemDocument> MergeWithExisting(
        List<GroceryItemDocument> generatedItems,
        List<GroceryItemDocument> existingItems)
    {
        var generatedByKey = generatedItems
            .ToDictionary(ComposeStableKey, StringComparer.OrdinalIgnoreCase);

        foreach (var existing in existingItems)
        {
            var key = ComposeStableKey(existing);

            if (generatedByKey.TryGetValue(key, out var generated))
            {
                generatedByKey[key] = generated with
                {
                    CheckedOff = existing.CheckedOff,
                    CheckedOffBy = existing.CheckedOffBy,
                    CheckedOffByName = existing.CheckedOffByName,
                    CheckedOffTimestamp = existing.CheckedOffTimestamp,
                    CompletedTTL = existing.CompletedTTL,
                    InStock = existing.InStock
                };
            }
            else if (existing.MealAssociations.Count == 0)
            {
                generatedByKey[key] = existing;
            }
        }

        return generatedByKey.Values
            .OrderBy(i => i.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<GroceryListDocument> CleanupCompletedItemsAsync(
        GroceryListDocument list,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var keptItems = list.Items
            .Where(i => !i.CheckedOffTimestamp.HasValue || now - i.CheckedOffTimestamp.Value <= CompletedItemRetention)
            .ToList();

        if (keptItems.Count == list.Items.Count)
        {
            return list;
        }

        var cleaned = list with
        {
            Items = keptItems,
            Version = list.Version + 1,
            UpdatedAt = now,
            Progress = ComputeProgress(keptItems)
        };

        await _groceryListRepository.PutAsync(ToListKey(list.FamilyId), cleaned, cancellationToken);
        return cleaned;
    }

    private static GroceryProgressDocument ComputeProgress(IReadOnlyCollection<GroceryItemDocument> items)
    {
        var total = items.Count;
        var completed = items.Count(i => i.CheckedOff);
        var percentage = total == 0 ? 0 : (int)Math.Round((double)completed * 100 / total, MidpointRounding.AwayFromZero);

        return new GroceryProgressDocument
        {
            Total = total,
            Completed = completed,
            Percentage = percentage
        };
    }

    private static decimal ParseQuantity(string? quantity)
    {
        if (string.IsNullOrWhiteSpace(quantity))
        {
            return 1;
        }

        if (decimal.TryParse(quantity.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return 1;
    }

    private static long ToUnixTimeSeconds(DateTimeOffset value) =>
        value.ToUnixTimeSeconds();

    private static string NormalizeName(string value) =>
        value.Trim().ToLowerInvariant();

    private static string ComposeStableKey(GroceryItemDocument item) =>
        $"{NormalizeName(item.Name)}|{item.Unit?.Trim()}";

    private static List<string> NormalizeSectionOrder(IEnumerable<string>? values)
    {
        var explicitValues = (values ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var defaultSection in DefaultSectionOrder)
        {
            if (!explicitValues.Contains(defaultSection, StringComparer.OrdinalIgnoreCase))
            {
                explicitValues.Add(defaultSection);
            }
        }

        return explicitValues;
    }

    private static DynamoDbKey ToListKey(string familyId) =>
        new($"FAMILY#{familyId}", GroceryListConstants.ActiveListId);

    private static DynamoDbKey ToPantryKey(string familyId) =>
        new($"FAMILY#{familyId}", GroceryListConstants.PantrySortKey);

    private static DynamoDbKey ToMealPlanKey(string familyId, string weekStartDate) =>
        new($"FAMILY#{familyId}", $"PLAN#{weekStartDate}");

    private sealed class AggregatedItem
    {
        public string DisplayName { get; init; } = string.Empty;

        public string Section { get; init; } = string.Empty;

        public decimal Quantity { get; set; }

        public string? Unit { get; init; }

        public bool InStock { get; init; }

        public List<MealAssociationDocument> Associations { get; } = [];
    }
}

public sealed class GroceryItemMutationResult
{
    public static GroceryItemMutationResult NotFoundList { get; } = new() { Status = GroceryItemMutationStatus.NotFoundList };

    public static GroceryItemMutationResult NotFoundItem { get; } = new() { Status = GroceryItemMutationStatus.NotFoundItem };

    public static GroceryItemMutationResult Conflict { get; } = new() { Status = GroceryItemMutationStatus.Conflict };

    public GroceryItemMutationStatus Status { get; init; }

    public GroceryItemDocument? Item { get; init; }

    public GroceryListDocument? List { get; init; }

    public static GroceryItemMutationResult Success(GroceryItemDocument item, GroceryListDocument list) =>
        new()
        {
            Status = GroceryItemMutationStatus.Success,
            Item = item,
            List = list
        };
}

public enum GroceryItemMutationStatus
{
    Success,
    NotFoundList,
    NotFoundItem,
    Conflict
}

public sealed class GroceryListPollResult
{
    public static GroceryListPollResult NotFound { get; } = new() { Status = GroceryListPollStatus.NotFound };

    public static GroceryListPollResult NoChanges { get; } = new() { Status = GroceryListPollStatus.NoChanges };

    public GroceryListPollStatus Status { get; init; }

    public GroceryListPollResponse? Response { get; init; }

    public static GroceryListPollResult HasChanges(GroceryListPollResponse response) =>
        new()
        {
            Status = GroceryListPollStatus.HasChanges,
            Response = response
        };
}

public enum GroceryListPollStatus
{
    HasChanges,
    NoChanges,
    NotFound
}
