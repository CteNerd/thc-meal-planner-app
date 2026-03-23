using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThcMealPlanner.Core.Data;
using ThcMealPlanner.Infrastructure.Data;

namespace ThcMealPlanner.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DynamoDbOptions>(configuration.GetSection(DynamoDbOptions.SectionName));

        services.AddAWSService<IAmazonDynamoDB>();
        services.AddAWSService<IAmazonS3>();
        services.AddSingleton<IDynamoDbTableNameResolver, DynamoDbTableNameResolver>();
        services.AddScoped(typeof(IDynamoDbRepository<>), typeof(DynamoDbRepository<>));

        return services;
    }
}