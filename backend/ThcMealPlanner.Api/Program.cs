using Amazon.Lambda.AspNetCoreServer.Hosting;
using Amazon.SimpleEmailV2;
using FluentValidation;
using ThcMealPlanner.Api.Authentication;
using ThcMealPlanner.Api.Chat;
using ThcMealPlanner.Api.GroceryLists;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Api.Notifications;
using ThcMealPlanner.Api.Profiles;
using ThcMealPlanner.Api.Recipes;
using ThcMealPlanner.Infrastructure;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);
builder.Services.AddCognitoAuthentication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IDependentProfileService, DependentProfileService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddHttpClient<IRecipeImportService, RecipeImportService>();
builder.Services.AddHttpClient<IMealPlanAiService, MealPlanAiService>();
builder.Services.AddHttpClient<IChatService, ChatService>();
builder.Services.AddScoped<IRecipeImageUploadService, RecipeImageUploadService>();
builder.Services.AddSingleton<IAmazonSimpleEmailServiceV2, AmazonSimpleEmailServiceV2Client>();
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.AddScoped<INotificationService, SesNotificationService>();
builder.Services.Configure<OpenAiOptions>(options =>
{
    builder.Configuration.GetSection(OpenAiOptions.SectionName).Bind(options);
    options.SecretArn = builder.Configuration["OPENAI_SECRET_ARN"] ?? options.SecretArn;
});
builder.Services.AddScoped<IOpenAiApiKeyProvider, OpenAiApiKeyProvider>();
builder.Services.Configure<ConstraintConfig>(builder.Configuration.GetSection(ConstraintConfig.SectionName));
builder.Services.AddScoped<IConstraintEngine, ConstraintEngine>();
builder.Services.AddScoped<IMealPlanService, MealPlanService>();
builder.Services.AddScoped<IGroceryListService, GroceryListService>();
builder.Services.AddScoped<IValidator<CreateMealPlanRequest>, CreateMealPlanRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateMealPlanRequest>, UpdateMealPlanRequestValidator>();
builder.Services.AddScoped<IValidator<GenerateMealPlanRequest>, GenerateMealPlanRequestValidator>();
builder.Services.AddScoped<IValidator<GenerateGroceryListRequest>, GenerateGroceryListRequestValidator>();
builder.Services.AddScoped<IValidator<ToggleGroceryItemRequest>, ToggleGroceryItemRequestValidator>();
builder.Services.AddScoped<IValidator<AddGroceryItemRequest>, AddGroceryItemRequestValidator>();
builder.Services.AddScoped<IValidator<SetInStockRequest>, SetInStockRequestValidator>();
builder.Services.AddScoped<IValidator<RemoveGroceryItemRequest>, RemoveGroceryItemRequestValidator>();
builder.Services.AddScoped<IValidator<ReplacePantryStaplesRequest>, ReplacePantryStaplesRequestValidator>();
builder.Services.AddScoped<IValidator<AddPantryStapleItemRequest>, AddPantryStapleItemRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateProfileRequest>, UpdateProfileRequestValidator>();
builder.Services.AddScoped<IValidator<CreateDependentRequest>, CreateDependentRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateDependentRequest>, UpdateDependentRequestValidator>();
builder.Services.AddScoped<IValidator<CreateRecipeRequest>, CreateRecipeRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateRecipeRequest>, UpdateRecipeRequestValidator>();
builder.Services.AddScoped<IValidator<FavoriteRecipeRequest>, FavoriteRecipeRequestValidator>();
builder.Services.AddScoped<IValidator<ImportRecipeFromUrlRequest>, ImportRecipeFromUrlRequestValidator>();
builder.Services.AddScoped<IValidator<CreateRecipeUploadUrlRequest>, CreateRecipeUploadUrlRequestValidator>();
builder.Services.AddScoped<IValidator<ChatMessageRequest>, ChatMessageRequestValidator>();
builder.Services.AddScoped<IValidator<SendTestNotificationRequest>, SendTestNotificationRequestValidator>();
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
    context.Response.Headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
    context.Response.Headers.TryAdd("Cross-Origin-Resource-Policy", "same-site");
    context.Response.Headers.TryAdd(
        "Content-Security-Policy",
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'");

    await next();
});
app.UseAuthentication();
app.UseMiddleware<UserProfileClaimsEnrichmentMiddleware>();
app.UseAuthorization();

app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        service = "thc-meal-planner-api",
        timestamp = DateTimeOffset.UtcNow
    });
});

var authenticatedApi = app.MapGroup("/api").RequireAuthorization();

authenticatedApi.MapGet("/session", (HttpContext httpContext) =>
{
    var user = httpContext.User;

    return Results.Ok(new
    {
        sub = user.FindFirst("sub")?.Value,
        email = user.FindFirst("email")?.Value,
        name = user.FindFirst("name")?.Value,
        authenticated = user.Identity?.IsAuthenticated ?? false
    });
});

authenticatedApi.MapProfileEndpoints();
authenticatedApi.MapDependentEndpoints();
authenticatedApi.MapRecipeEndpoints();
authenticatedApi.MapMealPlanEndpoints();
authenticatedApi.MapGroceryListEndpoints();
authenticatedApi.MapPantryEndpoints();
authenticatedApi.MapChatEndpoints();
authenticatedApi.MapNotificationEndpoints();

app.Run();

public partial class Program;
