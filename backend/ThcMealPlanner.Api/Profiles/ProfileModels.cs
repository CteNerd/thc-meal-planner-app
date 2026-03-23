namespace ThcMealPlanner.Api.Profiles;

public sealed class UserProfileDocument
{
    public string UserId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string FamilyId { get; init; } = string.Empty;

    public string Role { get; init; } = "member";

    public List<string> DietaryPrefs { get; init; } = [];

    public List<AllergyModel> Allergies { get; init; } = [];

    public List<string> ExcludedIngredients { get; init; } = [];

    public MacroTargetsModel? MacroTargets { get; init; }

    public List<string> CuisinePreferences { get; init; } = [];

    public CookingConstraintsModel? CookingConstraints { get; init; }

    public FlavorPreferencesModel? FlavorPreferences { get; init; }

    public int? DefaultServings { get; init; }

    public List<FamilyMemberModel> FamilyMembers { get; init; } = [];

    public List<string> DoctorNotes { get; init; } = [];

    public NotificationPreferencesModel? NotificationPrefs { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class UpdateProfileRequest
{
    public string? Name { get; init; }

    public string? Email { get; init; }

    public string? Role { get; init; }

    public List<string>? DietaryPrefs { get; init; }

    public List<AllergyModel>? Allergies { get; init; }

    public List<string>? ExcludedIngredients { get; init; }

    public MacroTargetsModel? MacroTargets { get; init; }

    public List<string>? CuisinePreferences { get; init; }

    public CookingConstraintsModel? CookingConstraints { get; init; }

    public FlavorPreferencesModel? FlavorPreferences { get; init; }

    public int? DefaultServings { get; init; }

    public List<FamilyMemberModel>? FamilyMembers { get; init; }

    public List<string>? DoctorNotes { get; init; }

    public NotificationPreferencesModel? NotificationPrefs { get; init; }
}

public sealed class AllergyModel
{
    public string Allergen { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string? Reaction { get; init; }

    public bool? CrossContamination { get; init; }
}

public sealed class MacroTargetsModel
{
    public int? Calories { get; init; }

    public int? Protein { get; init; }

    public int? Carbohydrates { get; init; }

    public int? Fat { get; init; }

    public int? Fiber { get; init; }

    public int? Sodium { get; init; }
}

public sealed class CookingConstraintsModel
{
    public int? MaxWeekdayPrepMinutes { get; init; }

    public int? MaxWeekendPrepMinutes { get; init; }

    public bool? PrefersBatchCooking { get; init; }

    public string? BatchCookDay { get; init; }
}

public sealed class FlavorPreferencesModel
{
    public string? SpiceLevel { get; init; }

    public bool? PrefersSavory { get; init; }

    public List<string>? FavoriteHerbs { get; init; }
}

public sealed class FamilyMemberModel
{
    public string Name { get; init; } = string.Empty;

    public int? Age { get; init; }

    public string? Preferences { get; init; }
}

public sealed class NotificationPreferencesModel
{
    public bool? MealPlanEmail { get; init; }

    public bool? WeeklyDigest { get; init; }

    public bool? SecurityAlerts { get; init; }
}