namespace ThcMealPlanner.Infrastructure.Data;

public sealed class DynamoDbOptions
{
    public const string SectionName = "DynamoDb";

    public string PartitionKeyName { get; init; } = "pk";

    public string SortKeyName { get; init; } = "sk";

    public Dictionary<string, string> Tables { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}