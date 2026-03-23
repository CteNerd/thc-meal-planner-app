using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2.DocumentModel;
using Microsoft.Extensions.Options;
using System.Text.Json;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Infrastructure.Data;

public sealed class DynamoDbRepository<TDocument> : IDynamoDbRepository<TDocument>
    where TDocument : class
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly IDynamoDbTableNameResolver _tableNameResolver;
    private readonly DynamoDbOptions _options;

    public DynamoDbRepository(
        IAmazonDynamoDB dynamoDb,
        IDynamoDbTableNameResolver tableNameResolver,
        IOptions<DynamoDbOptions> options)
    {
        _dynamoDb = dynamoDb;
        _tableNameResolver = tableNameResolver;
        _options = options.Value;
    }

    public async Task<TDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
    {
        var request = new GetItemRequest
        {
            TableName = _tableNameResolver.Resolve<TDocument>(),
            Key = BuildKey(key)
        };

        var response = await _dynamoDb.GetItemAsync(request, cancellationToken);

        return response.Item is null || response.Item.Count == 0 ? null : Deserialize(response.Item);
    }

    public async Task PutAsync(DynamoDbKey key, TDocument document, CancellationToken cancellationToken = default)
    {
        var item = Serialize(document);
        item[_options.PartitionKeyName] = new AttributeValue { S = key.PartitionKey };
        item[_options.SortKeyName] = new AttributeValue { S = key.SortKey };

        var request = new PutItemRequest
        {
            TableName = _tableNameResolver.Resolve<TDocument>(),
            Item = item
        };

        await _dynamoDb.PutItemAsync(request, cancellationToken);
    }

    public async Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
    {
        var request = new DeleteItemRequest
        {
            TableName = _tableNameResolver.Resolve<TDocument>(),
            Key = BuildKey(key)
        };

        await _dynamoDb.DeleteItemAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyList<TDocument>> QueryByPartitionKeyAsync(
        string partitionKey,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var request = new QueryRequest
        {
            TableName = _tableNameResolver.Resolve<TDocument>(),
            KeyConditionExpression = "#pk = :pk",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#pk"] = _options.PartitionKeyName
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = partitionKey }
            }
        };

        if (limit.HasValue)
        {
            request.Limit = limit.Value;
        }

        var response = await _dynamoDb.QueryAsync(request, cancellationToken);

        return response.Items.Select(Deserialize).ToList();
    }

    private Dictionary<string, AttributeValue> BuildKey(DynamoDbKey key)
    {
        return new Dictionary<string, AttributeValue>
        {
            [_options.PartitionKeyName] = new() { S = key.PartitionKey },
            [_options.SortKeyName] = new() { S = key.SortKey }
        };
    }

    private static Dictionary<string, AttributeValue> Serialize(TDocument document)
    {
        var json = JsonSerializer.Serialize(document, JsonSerializerOptions);
        var serializedDocument = Document.FromJson(json);

        return serializedDocument.ToAttributeMap();
    }

    private static TDocument Deserialize(Dictionary<string, AttributeValue> item)
    {
        var document = Document.FromAttributeMap(item);
        var json = document.ToJson();

        return JsonSerializer.Deserialize<TDocument>(json, JsonSerializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize DynamoDB document.");
    }
}