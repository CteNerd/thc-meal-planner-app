using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Api.Profiles;

public interface IDependentProfileService
{
    Task<IReadOnlyList<DependentProfileDocument>> ListByFamilyAsync(string familyId, CancellationToken cancellationToken = default);

    Task<DependentProfileDocument> CreateAsync(string familyId, CreateDependentRequest request, CancellationToken cancellationToken = default);

    Task<DependentProfileDocument?> UpdateAsync(string familyId, string userId, UpdateDependentRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string familyId, string userId, CancellationToken cancellationToken = default);
}

public sealed class DependentProfileService : IDependentProfileService
{
    private readonly IDynamoDbRepository<DependentProfileDocument> _repository;

    public DependentProfileService(IDynamoDbRepository<DependentProfileDocument> repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<DependentProfileDocument>> ListByFamilyAsync(string familyId, CancellationToken cancellationToken = default)
    {
        return _repository.QueryByIndexPartitionKeyAsync(
            indexName: "FamilyIndex",
            partitionKeyName: "familyId",
            partitionKeyValue: familyId,
            equalsFilters: new Dictionary<string, string>
            {
                ["role"] = "dependent"
            },
            cancellationToken: cancellationToken);
    }

    public async Task<DependentProfileDocument> CreateAsync(string familyId, CreateDependentRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = $"dep_{Guid.NewGuid().ToString("N")[..8]}";

        var dependent = new DependentProfileDocument
        {
            UserId = userId,
            Name = request.Name,
            FamilyId = familyId,
            Role = "dependent",
            AgeGroup = request.AgeGroup,
            DietaryPrefs = request.DietaryPrefs ?? [],
            Allergies = request.Allergies ?? [],
            EatingStyle = request.EatingStyle,
            PreferredFoods = request.PreferredFoods ?? [],
            AvoidedFoods = request.AvoidedFoods ?? [],
            MacroTargets = request.MacroTargets,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.PutAsync(ToDependentKey(userId), dependent, cancellationToken);

        return dependent;
    }

    public async Task<DependentProfileDocument?> UpdateAsync(
        string familyId,
        string userId,
        UpdateDependentRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetAsync(ToDependentKey(userId), cancellationToken);
        if (existing is null || !string.Equals(existing.FamilyId, familyId, StringComparison.Ordinal))
        {
            return null;
        }

        var updated = new DependentProfileDocument
        {
            UserId = existing.UserId,
            Name = request.Name ?? existing.Name,
            FamilyId = existing.FamilyId,
            Role = existing.Role,
            AgeGroup = request.AgeGroup ?? existing.AgeGroup,
            DietaryPrefs = request.DietaryPrefs ?? existing.DietaryPrefs,
            Allergies = request.Allergies ?? existing.Allergies,
            EatingStyle = request.EatingStyle ?? existing.EatingStyle,
            PreferredFoods = request.PreferredFoods ?? existing.PreferredFoods,
            AvoidedFoods = request.AvoidedFoods ?? existing.AvoidedFoods,
            MacroTargets = request.MacroTargets ?? existing.MacroTargets,
            Notes = request.Notes ?? existing.Notes,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _repository.PutAsync(ToDependentKey(userId), updated, cancellationToken);

        return updated;
    }

    public async Task<bool> DeleteAsync(string familyId, string userId, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetAsync(ToDependentKey(userId), cancellationToken);
        if (existing is null || !string.Equals(existing.FamilyId, familyId, StringComparison.Ordinal))
        {
            return false;
        }

        await _repository.DeleteAsync(ToDependentKey(userId), cancellationToken);

        return true;
    }

    private static DynamoDbKey ToDependentKey(string userId) => new($"USER#{userId}", "PROFILE");
}