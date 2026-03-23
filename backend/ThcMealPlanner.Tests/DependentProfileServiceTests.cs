using FluentAssertions;
using ThcMealPlanner.Api.Profiles;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Tests;

public sealed class DependentProfileServiceTests
{
    [Fact]
    public async Task ListByFamilyAsync_UsesFamilyIndexAndRoleFilter()
    {
        var repository = new RecordingDependentRepository
        {
            QueryByIndexResult =
            [
                new DependentProfileDocument
                {
                    UserId = "dep_a",
                    FamilyId = "FAM#test",
                    Role = "dependent",
                    Name = "Child A",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            ]
        };

        var service = new DependentProfileService(repository);

        var result = await service.ListByFamilyAsync("FAM#test");

        result.Should().HaveCount(1);
        repository.LastIndexName.Should().Be("FamilyIndex");
        repository.LastIndexPartitionKeyName.Should().Be("familyId");
        repository.LastIndexPartitionKeyValue.Should().Be("FAM#test");
        repository.LastIndexFilters.Should().NotBeNull();
        repository.LastIndexFilters!.Should().ContainKey("role");
        repository.LastIndexFilters["role"].Should().Be("dependent");
    }

    [Fact]
    public async Task CreateAsync_SetsDefaultsAndPersistsDependent()
    {
        var repository = new RecordingDependentRepository();
        var service = new DependentProfileService(repository);

        var created = await service.CreateAsync(
            "FAM#test",
            new CreateDependentRequest
            {
                Name = "Child New"
            });

        created.UserId.Should().StartWith("dep_");
        created.FamilyId.Should().Be("FAM#test");
        created.Role.Should().Be("dependent");
        created.DietaryPrefs.Should().BeEmpty();
        created.Allergies.Should().BeEmpty();
        created.PreferredFoods.Should().BeEmpty();
        created.AvoidedFoods.Should().BeEmpty();

        repository.StoredDocuments.Should().ContainSingle();
        repository.StoredDocuments.Values.Single().Name.Should().Be("Child New");
    }

    [Fact]
    public async Task UpdateAsync_WhenFamilyMismatch_ReturnsNullAndDoesNotPersist()
    {
        var repository = new RecordingDependentRepository();
        var key = new DynamoDbKey("USER#dep_x", "PROFILE");
        await repository.PutAsync(
            key,
            new DependentProfileDocument
            {
                UserId = "dep_x",
                FamilyId = "FAM#other",
                Role = "dependent",
                Name = "Child",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var service = new DependentProfileService(repository);

        var result = await service.UpdateAsync(
            "FAM#test",
            "dep_x",
            new UpdateDependentRequest
            {
                Name = "Updated"
            });

        result.Should().BeNull();
        repository.PutCalls.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_WhenFamilyMatches_UpdatesAndPreservesIdentityFields()
    {
        var repository = new RecordingDependentRepository();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);
        await repository.PutAsync(
            new DynamoDbKey("USER#dep_ok", "PROFILE"),
            new DependentProfileDocument
            {
                UserId = "dep_ok",
                FamilyId = "FAM#test",
                Role = "dependent",
                Name = "Original",
                AgeGroup = "child",
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });

        var service = new DependentProfileService(repository);

        var result = await service.UpdateAsync(
            "FAM#test",
            "dep_ok",
            new UpdateDependentRequest
            {
                Name = "Updated"
            });

        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated");
        result.UserId.Should().Be("dep_ok");
        result.FamilyId.Should().Be("FAM#test");
        result.Role.Should().Be("dependent");
        result.CreatedAt.Should().Be(createdAt);
        result.UpdatedAt.Should().BeAfter(createdAt);
    }

    [Fact]
    public async Task DeleteAsync_WhenFamilyMismatch_ReturnsFalse()
    {
        var repository = new RecordingDependentRepository();
        await repository.PutAsync(
            new DynamoDbKey("USER#dep_del", "PROFILE"),
            new DependentProfileDocument
            {
                UserId = "dep_del",
                FamilyId = "FAM#other",
                Role = "dependent",
                Name = "Child",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var service = new DependentProfileService(repository);

        var deleted = await service.DeleteAsync("FAM#test", "dep_del");

        deleted.Should().BeFalse();
        repository.DeleteCalls.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_WhenFamilyMatches_DeletesAndReturnsTrue()
    {
        var repository = new RecordingDependentRepository();
        await repository.PutAsync(
            new DynamoDbKey("USER#dep_del", "PROFILE"),
            new DependentProfileDocument
            {
                UserId = "dep_del",
                FamilyId = "FAM#test",
                Role = "dependent",
                Name = "Child",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        var service = new DependentProfileService(repository);

        var deleted = await service.DeleteAsync("FAM#test", "dep_del");

        deleted.Should().BeTrue();
        repository.DeleteCalls.Should().Be(1);
        repository.StoredDocuments.Should().BeEmpty();
    }

    private sealed class RecordingDependentRepository : IDynamoDbRepository<DependentProfileDocument>
    {
        public Dictionary<string, DependentProfileDocument> StoredDocuments { get; } = new(StringComparer.Ordinal);

        public IReadOnlyList<DependentProfileDocument> QueryByIndexResult { get; init; } = [];

        public string? LastIndexName { get; private set; }

        public string? LastIndexPartitionKeyName { get; private set; }

        public string? LastIndexPartitionKeyValue { get; private set; }

        public IReadOnlyDictionary<string, string>? LastIndexFilters { get; private set; }

        public int PutCalls { get; private set; }

        public int DeleteCalls { get; private set; }

        public Task<DependentProfileDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            StoredDocuments.TryGetValue(ToMapKey(key), out var document);
            return Task.FromResult(document);
        }

        public Task PutAsync(DynamoDbKey key, DependentProfileDocument document, CancellationToken cancellationToken = default)
        {
            PutCalls++;
            StoredDocuments[ToMapKey(key)] = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            StoredDocuments.Remove(ToMapKey(key));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DependentProfileDocument>> QueryByPartitionKeyAsync(
            string partitionKey,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var items = StoredDocuments.Values
                .Where(item => string.Equals($"USER#{item.UserId}", partitionKey, StringComparison.Ordinal))
                .ToList();

            if (limit.HasValue)
            {
                items = items.Take(limit.Value).ToList();
            }

            return Task.FromResult<IReadOnlyList<DependentProfileDocument>>(items);
        }

        public Task<IReadOnlyList<DependentProfileDocument>> QueryByIndexPartitionKeyAsync(
            string indexName,
            string partitionKeyName,
            string partitionKeyValue,
            IReadOnlyDictionary<string, string>? equalsFilters = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            LastIndexName = indexName;
            LastIndexPartitionKeyName = partitionKeyName;
            LastIndexPartitionKeyValue = partitionKeyValue;
            LastIndexFilters = equalsFilters;

            var items = QueryByIndexResult;

            if (limit.HasValue)
            {
                items = items.Take(limit.Value).ToList();
            }

            return Task.FromResult(items);
        }

        private static string ToMapKey(DynamoDbKey key)
        {
            return $"{key.PartitionKey}|{key.SortKey}";
        }
    }
}
