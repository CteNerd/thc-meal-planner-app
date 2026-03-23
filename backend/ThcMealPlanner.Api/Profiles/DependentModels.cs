namespace ThcMealPlanner.Api.Profiles;

public sealed class DependentProfileDocument
{
    public string UserId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string FamilyId { get; init; } = string.Empty;

    public string Role { get; init; } = "dependent";

    public string? AgeGroup { get; init; }

    public List<string> DietaryPrefs { get; init; } = [];

    public List<AllergyModel> Allergies { get; init; } = [];

    public string? EatingStyle { get; init; }

    public List<string> PreferredFoods { get; init; } = [];

    public List<string> AvoidedFoods { get; init; } = [];

    public MacroTargetsModel? MacroTargets { get; init; }

    public string? Notes { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class CreateDependentRequest
{
    public string Name { get; init; } = string.Empty;

    public string? AgeGroup { get; init; }

    public List<string>? DietaryPrefs { get; init; }

    public List<AllergyModel>? Allergies { get; init; }

    public string? EatingStyle { get; init; }

    public List<string>? PreferredFoods { get; init; }

    public List<string>? AvoidedFoods { get; init; }

    public MacroTargetsModel? MacroTargets { get; init; }

    public string? Notes { get; init; }
}

public sealed class UpdateDependentRequest
{
    public string? Name { get; init; }

    public string? AgeGroup { get; init; }

    public List<string>? DietaryPrefs { get; init; }

    public List<AllergyModel>? Allergies { get; init; }

    public string? EatingStyle { get; init; }

    public List<string>? PreferredFoods { get; init; }

    public List<string>? AvoidedFoods { get; init; }

    public MacroTargetsModel? MacroTargets { get; init; }

    public string? Notes { get; init; }
}