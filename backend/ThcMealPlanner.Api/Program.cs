using Amazon.Lambda.AspNetCoreServer.Hosting;
using ThcMealPlanner.Api.Authentication;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
builder.Services.AddCognitoAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
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

app.Run();

public partial class Program;
