using Microsoft.Extensions.Options;

namespace ThcMealPlanner.Infrastructure.Data;

public interface IDynamoDbTableNameResolver
{
    string Resolve<TDocument>() where TDocument : class;
}

public sealed class DynamoDbTableNameResolver : IDynamoDbTableNameResolver
{
    private readonly DynamoDbOptions _options;

    public DynamoDbTableNameResolver(IOptions<DynamoDbOptions> options)
    {
        _options = options.Value;
    }

    public string Resolve<TDocument>() where TDocument : class
    {
        var type = typeof(TDocument);

        if (_options.Tables.TryGetValue(type.FullName ?? string.Empty, out var fullNameMatch))
        {
            return fullNameMatch;
        }

        if (_options.Tables.TryGetValue(type.Name, out var nameMatch))
        {
            return nameMatch;
        }

        throw new InvalidOperationException(
            $"No DynamoDB table mapping is configured for document type '{type.FullName}'. " +
            $"Add '{type.Name}' or '{type.FullName}' under '{DynamoDbOptions.SectionName}:Tables'.");
    }
}