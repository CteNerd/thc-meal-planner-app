using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using ThcMealPlanner.Infrastructure.Data;

namespace ThcMealPlanner.Api.Recipes;

public interface IRecipeImageImportQuotaService
{
    Task<bool> TryReserveImportAsync(string familyId, string userId, CancellationToken cancellationToken = default);
}

public sealed class RecipeImageImportQuotaService : IRecipeImageImportQuotaService
{
    private const int HourlyLimit = 20;

    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly IDynamoDbTableNameResolver _tableNameResolver;
    private readonly DynamoDbOptions _options;

    public RecipeImageImportQuotaService(
        IAmazonDynamoDB dynamoDb,
        IDynamoDbTableNameResolver tableNameResolver,
        IOptions<DynamoDbOptions> options)
    {
        _dynamoDb = dynamoDb;
        _tableNameResolver = tableNameResolver;
        _options = options.Value;
    }

    public async Task<bool> TryReserveImportAsync(string familyId, string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        var tableName = _tableNameResolver.Resolve<RecipeDocument>();

        var request = new UpdateItemRequest
        {
            TableName = tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [_options.PartitionKeyName] = new() { S = $"USER#{userId}" },
                [_options.SortKeyName] = new() { S = $"IMAGEIMPORT#{windowStart:yyyyMMddHH}" }
            },
            ConditionExpression = "attribute_not_exists(ImportCount) OR ImportCount < :hourlyLimit",
            UpdateExpression = "ADD ImportCount :increment SET FamilyId = :familyId, UserId = :userId, WindowStartUtc = :windowStartUtc, UpdatedAtUtc = :updatedAtUtc",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":increment"] = new() { N = "1" },
                [":hourlyLimit"] = new() { N = HourlyLimit.ToString() },
                [":familyId"] = new() { S = familyId },
                [":userId"] = new() { S = userId },
                [":windowStartUtc"] = new() { S = windowStart.UtcDateTime.ToString("O") },
                [":updatedAtUtc"] = new() { S = now.UtcDateTime.ToString("O") }
            }
        };

        try
        {
            await _dynamoDb.UpdateItemAsync(request, cancellationToken);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }
}