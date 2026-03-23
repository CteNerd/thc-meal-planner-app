using FluentAssertions;
using Microsoft.Extensions.Options;
using ThcMealPlanner.Infrastructure.Data;

namespace ThcMealPlanner.Tests;

public sealed class DynamoDbTableNameResolverTests
{
    [Fact]
    public void Resolve_WithTypeNameMapping_ReturnsTableName()
    {
        var options = Options.Create(new DynamoDbOptions
        {
            Tables = new Dictionary<string, string>
            {
                [nameof(TestProfileDocument)] = "UsersTable"
            }
        });

        var resolver = new DynamoDbTableNameResolver(options);

        var tableName = resolver.Resolve<TestProfileDocument>();

        tableName.Should().Be("UsersTable");
    }

    [Fact]
    public void Resolve_WithoutMapping_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new DynamoDbOptions());
        var resolver = new DynamoDbTableNameResolver(options);

        var action = () => resolver.Resolve<TestProfileDocument>();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*TestProfileDocument*");
    }

    private sealed class TestProfileDocument
    {
        public string Id { get; init; } = string.Empty;
    }
}