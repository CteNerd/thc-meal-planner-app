namespace ThcMealPlanner.Infrastructure.Data;

public sealed class DynamoDbOptions
{
    public const string SectionName = "DynamoDb";

    public string PartitionKeyName { get; init; } = "PK";

    public string SortKeyName { get; init; } = "SK";

    public Dictionary<string, string> Tables { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}