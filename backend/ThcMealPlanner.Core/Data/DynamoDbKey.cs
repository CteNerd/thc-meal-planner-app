namespace ThcMealPlanner.Core.Data;

public sealed record DynamoDbKey(string PartitionKey, string SortKey);