using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace ThcMealPlanner.Api.Recipes;

public interface IRecipeImageUploadService
{
    Task<RecipeUploadUrlResponse> CreateUploadUrlAsync(
        string recipeId,
        CreateRecipeUploadUrlRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RecipeImageUploadService : IRecipeImageUploadService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;

    public RecipeImageUploadService(IAmazonS3 s3, IConfiguration configuration)
    {
        _s3 = s3;
        _bucketName = configuration["RECIPE_IMAGES_BUCKET"]
            ?? throw new InvalidOperationException("RECIPE_IMAGES_BUCKET is not configured.");
    }

    public Task<RecipeUploadUrlResponse> CreateUploadUrlAsync(
        string recipeId,
        CreateRecipeUploadUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(request.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = request.ContentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        var imageKey = $"recipes/{recipeId}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var uploadUrl = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = imageKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(15),
            ContentType = request.ContentType,
            Protocol = Protocol.HTTPS
        });

        return Task.FromResult(new RecipeUploadUrlResponse
        {
            UploadUrl = uploadUrl,
            ImageKey = imageKey,
            ImageUrl = $"/images/{imageKey}"
        });
    }
}