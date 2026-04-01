using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ThcMealPlanner.Api.Recipes;

namespace ThcMealPlanner.Tests;

public sealed class RecipeImageUploadServiceTests
{
    [Fact]
    public void Constructor_WhenBucketMissing_ThrowsInvalidOperationException()
    {
        var s3 = BuildS3Client();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        var create = () => new RecipeImageUploadService(s3, configuration);

        create.Should().Throw<InvalidOperationException>()
            .WithMessage("RECIPE_IMAGES_BUCKET is not configured.*");
    }

    [Fact]
    public async Task CreateUploadUrl_WithFileExtension_UsesProvidedExtension()
    {
        var service = CreateService();

        var response = await service.CreateUploadUrlAsync("rec_123", new CreateRecipeUploadUrlRequest
        {
            FileName = "family-meal.PNG",
            ContentType = "image/png"
        });

        response.ImageKey.Should().StartWith("recipes/rec_123/");
        response.ImageKey.Should().EndWith(".png");
        response.ImageUrl.Should().Contain("thc-meal-planner-dev-recipe-images");
        response.ImageUrl.Should().Contain(response.ImageKey);
        response.UploadUrl.Should().Contain("thc-meal-planner-dev-recipe-images");
        response.UploadUrl.Should().Contain("?");
    }

    [Fact]
    public async Task CreateUploadUrl_WithoutExtension_InfersWebpExtension()
    {
        var service = CreateService();

        var response = await service.CreateUploadUrlAsync("rec_123", new CreateRecipeUploadUrlRequest
        {
            FileName = "family-meal",
            ContentType = "image/webp"
        });

        response.ImageKey.Should().EndWith(".webp");
    }

    [Fact]
    public async Task CreateUploadUrl_WithoutKnownExtension_DefaultsToJpg()
    {
        var service = CreateService();

        var response = await service.CreateUploadUrlAsync("rec_123", new CreateRecipeUploadUrlRequest
        {
            FileName = "family-meal",
            ContentType = "image/heif"
        });

        response.ImageKey.Should().EndWith(".jpg");
    }

    private static RecipeImageUploadService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RECIPE_IMAGES_BUCKET"] = "thc-meal-planner-dev-recipe-images"
            })
            .Build();

        return new RecipeImageUploadService(BuildS3Client(), configuration);
    }

    private static IAmazonS3 BuildS3Client()
    {
        return new AmazonS3Client(
            new BasicAWSCredentials("test", "test"),
            new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.USEast1
            });
    }
}
