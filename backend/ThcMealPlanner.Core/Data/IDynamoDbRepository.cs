namespace ThcMealPlanner.Core.Data;

public interface IDynamoDbRepository<TDocument>
    where TDocument : class
{
    Task<TDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default);

    Task PutAsync(DynamoDbKey key, TDocument document, CancellationToken cancellationToken = default);

    Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TDocument>> QueryByPartitionKeyAsync(
        string partitionKey,
        int? limit = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TDocument>> QueryByIndexPartitionKeyAsync(
        string indexName,
        string partitionKeyName,
        string partitionKeyValue,
        IReadOnlyDictionary<string, string>? equalsFilters = null,
        int? limit = null,
        CancellationToken cancellationToken = default);
}