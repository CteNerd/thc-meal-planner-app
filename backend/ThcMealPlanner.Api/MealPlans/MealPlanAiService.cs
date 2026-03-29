using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Options;
using ThcMealPlanner.Api.Recipes;

namespace ThcMealPlanner.Api.MealPlans;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string? SecretArn { get; set; }

    public string Model { get; set; } = "gpt-4o-mini";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public double Temperature { get; set; } = 0.2;
}

public interface IOpenAiApiKeyProvider
{
    Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default);
}

public sealed class OpenAiApiKeyProvider : IOpenAiApiKeyProvider
{
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiApiKeyProvider> _logger;

    private string? _cachedApiKey;

    public OpenAiApiKeyProvider(
        IAmazonSecretsManager secretsManager,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiApiKeyProvider> logger)
    {
        _secretsManager = secretsManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedApiKey))
        {
            return _cachedApiKey;
        }

        if (string.IsNullOrWhiteSpace(_options.SecretArn))
        {
            _logger.LogWarning("OpenAI secret ARN is not configured. Falling back to deterministic meal planning.");
            return null;
        }

        try
        {
            var response = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = _options.SecretArn
            }, cancellationToken);

            if (string.IsNullOrWhiteSpace(response.SecretString))
            {
                _logger.LogWarning("OpenAI secret exists but secret string is empty.");
                return null;
            }

            var parsedKey = ParseApiKey(response.SecretString);
            if (string.IsNullOrWhiteSpace(parsedKey))
            {
                _logger.LogWarning("OpenAI secret payload does not contain a valid apiKey value.");
                return null;
            }

            _cachedApiKey = parsedKey;
            return _cachedApiKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load OpenAI key from Secrets Manager. Falling back to deterministic planner.");
            return null;
        }
    }

    private static string? ParseApiKey(string secretString)
    {
        var trimmed = secretString.Trim();
        if (trimmed.StartsWith("sk-", StringComparison.Ordinal))
        {
            return trimmed;
        }

        try
        {
            using var doc = JsonDocument.Parse(secretString);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (doc.RootElement.TryGetProperty("apiKey", out var apiKeyElement) && apiKeyElement.ValueKind == JsonValueKind.String)
            {
                return apiKeyElement.GetString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}

public interface IMealPlanAiService
{
    Task<IReadOnlyList<string>> GenerateRecipeIdsAsync(
        string weekStartDate,
        IReadOnlyList<(string Day, string MealType)> slots,
        IReadOnlyList<RecipeDocument> recipes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> RankSwapCandidatesAsync(
        string day,
        string mealType,
        string? currentRecipeId,
        IReadOnlyList<RecipeDocument> candidates,
        CancellationToken cancellationToken = default);

    /// <summary>Ask the AI to suggest fresh meal ideas not currently in the cookbook, guided by family profile constraints.</summary>
    Task<IReadOnlyList<AiRecipeIdea>> SuggestFreshIdeasAsync(
        string day,
        string mealType,
        string? profileContext,
        int count,
        CancellationToken cancellationToken = default);
}

public sealed class MealPlanAiService : IMealPlanAiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IOpenAiApiKeyProvider _apiKeyProvider;
    private readonly OpenAiOptions _options;
    private readonly ILogger<MealPlanAiService> _logger;

    public MealPlanAiService(
        HttpClient httpClient,
        IOpenAiApiKeyProvider apiKeyProvider,
        IOptions<OpenAiOptions> options,
        ILogger<MealPlanAiService> logger)
    {
        _httpClient = httpClient;
        _apiKeyProvider = apiKeyProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GenerateRecipeIdsAsync(
        string weekStartDate,
        IReadOnlyList<(string Day, string MealType)> slots,
        IReadOnlyList<RecipeDocument> recipes,
        CancellationToken cancellationToken = default)
    {
        if (slots.Count == 0 || recipes.Count == 0)
        {
            return [];
        }

        var prompt = BuildGenerationPrompt(weekStartDate, slots, recipes);
        var content = await ExecutePromptAsync(prompt, cancellationToken);
        return ParseIdArray(content, "recipeIds");
    }

    public async Task<IReadOnlyList<string>> RankSwapCandidatesAsync(
        string day,
        string mealType,
        string? currentRecipeId,
        IReadOnlyList<RecipeDocument> candidates,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var prompt = BuildSwapRankingPrompt(day, mealType, currentRecipeId, candidates);
        var content = await ExecutePromptAsync(prompt, cancellationToken);
        return ParseIdArray(content, "rankedRecipeIds");
    }

    private async Task<string?> ExecutePromptAsync(string prompt, CancellationToken cancellationToken)
    {
        var apiKey = await _apiKeyProvider.GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var payload = new
        {
            model = _options.Model,
            temperature = _options.Temperature,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are a meal planning assistant. Return strict JSON only with no markdown or prose."
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI request failed with status {StatusCode}. Falling back to deterministic planner.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtractMessageContent(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI request failed. Falling back to deterministic planner.");
            return null;
        }
    }

    private static string BuildGenerationPrompt(
        string weekStartDate,
        IReadOnlyList<(string Day, string MealType)> slots,
        IReadOnlyList<RecipeDocument> recipes)
    {
        var slotJson = JsonSerializer.Serialize(
            slots.Select(s => new { day = s.Day, mealType = s.MealType }),
            JsonOptions);

        var recipesJson = JsonSerializer.Serialize(
            recipes.Select(r => new
            {
                recipeId = r.RecipeId,
                name = r.Name,
                category = r.Category,
                prepTimeMinutes = r.PrepTimeMinutes,
                cookTimeMinutes = r.CookTimeMinutes,
                cookingMethod = r.CookingMethod,
                ingredients = r.Ingredients.Select(i => i.Name).ToList(),
                tags = r.Tags
            }),
            JsonOptions);

        return string.Join('\n',
        [
            $"Generate a weekly meal plan for weekStartDate {weekStartDate}.",
            "Return strict JSON in this shape only:",
            "{\"recipeIds\": [\"recipeId1\", \"recipeId2\", ...]}",
            string.Empty,
            "Rules:",
            $"- The recipeIds array length must equal slot count: {slots.Count}.",
            "- Follow slot order exactly as provided.",
            "- Prefer category matching by mealType (breakfast/lunch/dinner).",
            "- Prefer variety and avoid repeated recipeIds when possible.",
            "- Keep total prep+cook time practical for weekdays.",
            string.Empty,
            "Slots:",
            slotJson,
            string.Empty,
            "Available recipes:",
            recipesJson
        ]);
    }

    public async Task<IReadOnlyList<AiRecipeIdea>> SuggestFreshIdeasAsync(
        string day,
        string mealType,
        string? profileContext,
        int count,
        CancellationToken cancellationToken = default)
    {
        var safeCount = Math.Clamp(count, 1, 5);
        var prompt = BuildFreshIdeasPrompt(day, mealType, profileContext, safeCount);
        var content = await ExecutePromptAsync(prompt, cancellationToken);
        return ParseIdeasArray(content);
    }

    private static string BuildFreshIdeasPrompt(string day, string mealType, string? profileContext, int count)
    {
        var contextLine = string.IsNullOrWhiteSpace(profileContext)
            ? string.Empty
            : $"Family context: {profileContext}";

        return string.Join('\n',
        [
            $"Suggest {count} fresh {mealType} meal ideas for {day} that are NOT in the family cookbook.",
            "Return strict JSON in this shape only:",
            "{\"ideas\": [{\"name\": \"Meal name\", \"reason\": \"Why it fits\", \"category\": \"breakfast|lunch|dinner\"}]}",
            string.Empty,
            "Rules:",
            $"- All ideas must be {mealType} appropriate.",
            "- Reason must be one sentence explaining why it suits the family.",
            "- Respect any allergies or avoided ingredients in the family context.",
            "- Suggest practical home-cooked meals, not restaurant dishes.",
            string.Empty,
            contextLine
        ]);
    }

    private static IReadOnlyList<AiRecipeIdea> ParseIdeasArray(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("ideas", out var ideas) || ideas.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<AiRecipeIdea>();
            foreach (var el in ideas.EnumerateArray())
            {
                var name = el.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                var reason = el.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
                var category = el.TryGetProperty("category", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    result.Add(new AiRecipeIdea(name, reason ?? string.Empty, category ?? string.Empty));
                }
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    private static string BuildSwapRankingPrompt(
        string day,
        string mealType,
        string? currentRecipeId,
        IReadOnlyList<RecipeDocument> candidates)
    {
        var candidatesJson = JsonSerializer.Serialize(
            candidates.Select(r => new
            {
                recipeId = r.RecipeId,
                name = r.Name,
                category = r.Category,
                prepTimeMinutes = r.PrepTimeMinutes,
                cookTimeMinutes = r.CookTimeMinutes,
                cookingMethod = r.CookingMethod,
                ingredients = r.Ingredients.Select(i => i.Name).ToList(),
                tags = r.Tags
            }),
            JsonOptions);

        return string.Join('\n',
        [
            "Rank the provided swap candidates for this slot:",
            $"- day: {day}",
            $"- mealType: {mealType}",
            $"- currentRecipeId: {currentRecipeId ?? "none"}",
            string.Empty,
            "Return strict JSON in this shape only:",
            "{\"rankedRecipeIds\": [\"bestFirst\", \"next\", ...]}",
            string.Empty,
            "Rules:",
            "- Only include recipeIds from provided candidates.",
            "- Best-first ordering.",
            "- Prefer category/mealType match and practical prep time.",
            "- Prefer variety and avoid current recipe when possible.",
            string.Empty,
            "Candidates:",
            candidatesJson
        ]);
    }

    private static string? ExtractMessageContent(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var first = choices[0];
            if (!first.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return content.GetString();
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ParseIdArray(string? content, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return values
                .EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
