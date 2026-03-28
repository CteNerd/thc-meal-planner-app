namespace ThcMealPlanner.Api.GroceryLists;

public sealed record class GroceryListDocument
{
    public string FamilyId { get; init; } = string.Empty;

    public string ListId { get; init; } = GroceryListConstants.ActiveListId;

    public List<GroceryItemDocument> Items { get; init; } = [];

    public int Version { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public GroceryProgressDocument Progress { get; init; } = new();
}

public sealed record class GroceryItemDocument
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Section { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public string? Unit { get; init; }

    public List<MealAssociationDocument> MealAssociations { get; init; } = [];

    public bool CheckedOff { get; init; }

    public string? CheckedOffBy { get; init; }

    public string? CheckedOffByName { get; init; }

    public DateTimeOffset? CheckedOffTimestamp { get; init; }

    public long? CompletedTTL { get; init; }

    public bool InStock { get; init; }
}

public sealed class MealAssociationDocument
{
    public string RecipeId { get; init; } = string.Empty;

    public string RecipeName { get; init; } = string.Empty;

    public string MealDay { get; init; } = string.Empty;
}

public sealed class GroceryProgressDocument
{
    public int Total { get; init; }

    public int Completed { get; init; }

    public int Percentage { get; init; }
}

public sealed record class PantryStaplesDocument
{
    public string FamilyId { get; init; } = string.Empty;

    public List<PantryStapleItemDocument> Items { get; init; } = [];

    public List<string> PreferredSectionOrder { get; init; } = [];

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class PantryStapleItemDocument
{
    public string Name { get; init; } = string.Empty;

    public string? Section { get; init; }
}

public sealed class GenerateGroceryListRequest
{
    public string? WeekStartDate { get; init; }

    public bool ClearExisting { get; init; }
}

public sealed class ToggleGroceryItemRequest
{
    public int Version { get; init; }
}

public sealed class AddGroceryItemRequest
{
    public string Name { get; init; } = string.Empty;

    public string Section { get; init; } = string.Empty;

    public decimal? Quantity { get; init; }

    public string? Unit { get; init; }

    public int Version { get; init; }
}

public sealed class SetInStockRequest
{
    public bool InStock { get; init; }

    public int Version { get; init; }
}

public sealed class RemoveGroceryItemRequest
{
    public int Version { get; init; }
}

public sealed class ReplacePantryStaplesRequest
{
    public List<PantryStapleItemDocument> Items { get; init; } = [];

    public List<string>? PreferredSectionOrder { get; init; }
}

public sealed class AddPantryStapleItemRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Section { get; init; }
}

public sealed class GroceryItemMutationResponse
{
    public GroceryItemDocument Item { get; init; } = new();

    public int Version { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public GroceryProgressDocument Progress { get; init; } = new();
}

public sealed class GroceryListPollResponse
{
    public bool HasChanges { get; init; }

    public List<GroceryListChangeDocument> Changes { get; init; } = [];

    public int Version { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class GroceryListChangeDocument
{
    public string ItemId { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public GroceryItemDocument Item { get; init; } = new();
}

internal static class GroceryListConstants
{
    public const string ActiveListId = "LIST#ACTIVE";
    public const string PantrySortKey = "PANTRY#STAPLES";
}
