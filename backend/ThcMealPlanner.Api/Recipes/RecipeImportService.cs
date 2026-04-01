using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ThcMealPlanner.Api.MealPlans;

namespace ThcMealPlanner.Api.Recipes;

public interface IRecipeImportService
{
    Task<ImportedRecipeDraft> ImportFromUrlAsync(string url, CancellationToken cancellationToken = default);
    Task<ImportedRecipeDraft> ImportFromImageAsync(string imageUrl, CancellationToken cancellationToken = default);
}

public sealed partial class RecipeImportService : IRecipeImportService
{
    private const int MaxResponseBytes = 1024 * 1024;
    private const int MaxAiInputCharacters = 24000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IOpenAiApiKeyProvider? _apiKeyProvider;
    private readonly OpenAiOptions _openAiOptions;
    private readonly ILogger<RecipeImportService>? _logger;

    public RecipeImportService(
        HttpClient httpClient,
        IOpenAiApiKeyProvider? apiKeyProvider,
        IOptions<OpenAiOptions>? openAiOptions,
        ILogger<RecipeImportService>? logger)
    {
        _httpClient = httpClient;
        _apiKeyProvider = apiKeyProvider;
        _openAiOptions = openAiOptions?.Value ?? new OpenAiOptions();
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("THCMealPlannerRecipeImporter/1.0");
    }

    public async Task<ImportedRecipeDraft> ImportFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        var uri = ValidateUrl(url);
        await EnsureSafeAddressAsync(uri, cancellationToken);

        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int bytesRead;
        var responseTruncated = false;

        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            var remainingBytes = MaxResponseBytes - (int)memory.Length;
            if (remainingBytes <= 0)
            {
                responseTruncated = true;
                break;
            }

            var bytesToWrite = Math.Min(bytesRead, remainingBytes);
            await memory.WriteAsync(buffer.AsMemory(0, bytesToWrite), cancellationToken);

            if (bytesToWrite < bytesRead)
            {
                responseTruncated = true;
                break;
            }
        }

        var html = System.Text.Encoding.UTF8.GetString(memory.ToArray());
        try
        {
            var draft = await ParseImportedRecipeDraftAsync(uri, html, cancellationToken);
            if (responseTruncated)
            {
                draft.Warnings.Add("Source page was larger than 1 MB; import used a truncated snapshot. Review before saving.");
            }

            return draft;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException)
        {
            _logger?.LogWarning(ex, "Recipe parsing failed; falling back to best-effort text extraction.");
            var fallbackDraft = ParseDraftFromText(uri, html);
            if (responseTruncated)
            {
                fallbackDraft.Warnings.Add("Source page was larger than 1 MB; import used a truncated snapshot. Review before saving.");
            }

            fallbackDraft.Warnings.Add("Structured parsing failed; used best-effort text extraction.");
            return fallbackDraft;
        }
    }

    public async Task<ImportedRecipeDraft> ImportFromImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new InvalidOperationException("Image URL is required.");
        }

        if (_apiKeyProvider is null)
        {
            throw new InvalidOperationException("AI image extraction is not configured.");
        }

        var apiKey = await _apiKeyProvider.GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("AI image extraction API key is unavailable.");
        }

        var payload = new
        {
            model = _openAiOptions.Model,
            temperature = 0.1,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "Extract one recipe from an image. Return strict JSON only with no markdown or prose."
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = BuildAiImageExtractionPrompt()
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = imageUrl,
                                detail = "auto"
                            }
                        }
                    }
                }
            }
        };

        using var response = await SendOpenAiRequestWithRetryAsync(payload, apiKey, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"AI image extraction failed with status {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var content = ExtractAiMessageContent(body);

        var sourceUrl = Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            ? uri
            : new Uri("https://image-upload.local/recipe");

        var draft = ParseAiDraftContent(
            sourceUrl,
            content,
            sourceType: "image_upload",
            reviewWarning: "AI vision extraction was used. Verify ingredients and steps before saving.");

        if (draft is null)
        {
            throw new InvalidOperationException("AI image extraction did not return a usable recipe.");
        }

        return draft;
    }

    private async Task<HttpResponseMessage> SendOpenAiRequestWithRetryAsync(object payload, string apiKey, CancellationToken cancellationToken)
    {
        var endpoint = $"{_openAiOptions.BaseUrl.TrimEnd('/')}/chat/completions";

        async Task<HttpResponseMessage> SendAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return await _httpClient.SendAsync(request, cancellationToken);
        }

        var firstResponse = await SendAsync();
        if (firstResponse.StatusCode != HttpStatusCode.TooManyRequests)
        {
            return firstResponse;
        }

        firstResponse.Dispose();
        _logger?.LogWarning("OpenAI image extraction hit rate limit (429). Retrying once.");
        await Task.Delay(TimeSpan.FromSeconds(1.5), cancellationToken);

        return await SendAsync();
    }

    internal async Task<ImportedRecipeDraft> ParseImportedRecipeDraftAsync(
        Uri sourceUrl,
        string html,
        CancellationToken cancellationToken = default)
    {
        if (TryParseJsonLdDraft(sourceUrl, html, out var jsonLdDraft))
        {
            return jsonLdDraft;
        }

        var aiDraft = await TryParseAiDraftAsync(sourceUrl, html, cancellationToken);
        if (aiDraft is not null)
        {
            return aiDraft;
        }

        return ParseDraftFromText(sourceUrl, html);
    }

    internal static ImportedRecipeDraft ParseImportedRecipeDraft(Uri sourceUrl, string html)
    {
        if (TryParseJsonLdDraft(sourceUrl, html, out var jsonLdDraft))
        {
            return jsonLdDraft;
        }

        return ParseDraftFromText(sourceUrl, html);
    }

    private async Task<ImportedRecipeDraft?> TryParseAiDraftAsync(Uri sourceUrl, string html, CancellationToken cancellationToken)
    {
        if (_apiKeyProvider is null)
        {
            return null;
        }

        var apiKey = await _apiKeyProvider.GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var pageText = NormalizeWhitespace(StripHtml(html));
        if (string.IsNullOrWhiteSpace(pageText))
        {
            return null;
        }

        if (pageText.Length > MaxAiInputCharacters)
        {
            pageText = pageText[..MaxAiInputCharacters];
        }

        var payload = new
        {
            model = _openAiOptions.Model,
            temperature = 0.1,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "Extract a recipe from webpage text. Return strict JSON only with no markdown or prose."
                },
                new
                {
                    role = "user",
                    content = BuildAiExtractionPrompt(sourceUrl, pageText)
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_openAiOptions.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("AI recipe extraction request failed with status {StatusCode}. Falling back to heuristic parser.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var content = ExtractAiMessageContent(body);
            return ParseAiDraftContent(sourceUrl, content);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "AI recipe extraction failed. Falling back to heuristic parser.");
            return null;
        }
    }

    private static string BuildAiExtractionPrompt(Uri sourceUrl, string pageText)
    {
        return string.Join('\n',
        [
            "Extract one recipe from this page text.",
            "Return strict JSON in this shape only:",
            "{\"name\":\"\",\"description\":\"\",\"category\":\"breakfast|lunch|dinner|snack\",\"cuisine\":\"\",\"servings\":0,\"prepTimeMinutes\":0,\"cookTimeMinutes\":0,\"tags\":[\"\"],\"ingredients\":[\"\"],\"instructions\":[\"\"]}",
            "Rules:",
            "- Use null for unknown scalar values.",
            "- Keep ingredients as concise strings.",
            "- Keep instructions as ordered steps.",
            "- Do not invent data not supported by the page.",
            $"Source URL: {sourceUrl}",
            "Page text:",
            pageText
        ]);
    }

    private static string BuildAiImageExtractionPrompt()
    {
        return string.Join('\n',
        [
            "Extract exactly one recipe from this image.",
            "Return strict JSON in this shape only:",
            "{\"name\":\"\",\"description\":\"\",\"category\":\"breakfast|lunch|dinner|snack\",\"cuisine\":\"\",\"servings\":0,\"prepTimeMinutes\":0,\"cookTimeMinutes\":0,\"tags\":[\"\"],\"ingredients\":[\"\"],\"instructions\":[\"\"]}",
            "Rules:",
            "- Capture ingredient quantities and units as written.",
            "- Keep instruction order exactly as shown.",
            "- If uncertain, leave scalar values null and add concise placeholders only for ingredients/instructions.",
            "- Do not invent nutrition or details not visible in the image.",
            "- Output JSON only, no markdown."
        ]);
    }

    private static ImportedRecipeDraft? ParseAiDraftContent(
        Uri sourceUrl,
        string? content,
        string sourceType = "url",
        string reviewWarning = "AI-assisted extraction was used. Review before saving.")
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var root = doc.RootElement;
            var name = GetString(root, "name")?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var ingredients = ParseAiIngredientList(root, "ingredients");
            var instructions = ParseAiStepList(root, "instructions");
            var warnings = new List<string> { reviewWarning };

            if (ingredients.Count == 0)
            {
                warnings.Add("AI extraction did not find ingredients. Review source before saving.");
                ingredients = [new RecipeIngredientModel { Name = "Review source and add ingredients." }];
            }

            if (instructions.Count == 0)
            {
                warnings.Add("AI extraction did not find instructions. Review source before saving.");
                instructions = ["Review source and add preparation steps."];
            }

            return new ImportedRecipeDraft
            {
                Name = name,
                Description = GetString(root, "description"),
                Category = NormalizeCategory(GetString(root, "category")),
                Cuisine = GetString(root, "cuisine"),
                Servings = ParseFirstInt(GetString(root, "servings")) ?? ParseInt(root, "servings"),
                PrepTimeMinutes = ParseFirstInt(GetString(root, "prepTimeMinutes")) ?? ParseInt(root, "prepTimeMinutes"),
                CookTimeMinutes = ParseFirstInt(GetString(root, "cookTimeMinutes")) ?? ParseInt(root, "cookTimeMinutes"),
                Tags = ParseAiStringList(root, "tags"),
                Ingredients = ingredients,
                Instructions = instructions,
                SourceType = sourceType,
                SourceUrl = sourceUrl.ToString(),
                Warnings = warnings
            };
        }
        catch
        {
            return null;
        }
    }

    private static List<string> ParseAiStringList(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return [];
        }

        return ExtractStringValues(property, "text", "name", "item", "itemListElement")
            .SelectMany(ParseDelimitedList)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }

    private static List<RecipeIngredientModel> ParseAiIngredientList(JsonElement root, string propertyName)
    {
        var values = ParseAiStringList(root, propertyName);
        return values
            .Select(v => new RecipeIngredientModel { Name = v.Trim() })
            .Take(30)
            .ToList();
    }

    private static List<string> ParseAiStepList(JsonElement root, string propertyName)
    {
        return ParseAiStringList(root, propertyName)
            .Select(CleanListLine)
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .Take(30)
            .ToList();
    }

    private static int? ParseInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var number) => number,
            JsonValueKind.String => ParseFirstInt(property.GetString()),
            _ => null
        };
    }

    private static string? ExtractAiMessageContent(string responseBody)
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

    private static Uri ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("The provided URL is invalid.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only HTTP and HTTPS URLs are allowed.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("URLs with embedded credentials are not allowed.");
        }

        return uri;
    }

    private static async Task EnsureSafeAddressAsync(Uri uri, CancellationToken cancellationToken)
    {
        var host = uri.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Localhost URLs are not allowed.");
        }

        if (IPAddress.TryParse(host, out var hostAddress) && IsBlockedAddress(hostAddress))
        {
            throw new InvalidOperationException("Private or link-local IP addresses are not allowed.");
        }

        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        if (addresses.Any(IsBlockedAddress))
        {
            throw new InvalidOperationException("The target URL resolves to a blocked address.");
        }
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();

        return address.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork =>
                bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168),
            System.Net.Sockets.AddressFamily.InterNetworkV6 => address.IsIPv6LinkLocal || address.IsIPv6SiteLocal,
            _ => false
        };
    }

    private static ImportedRecipeDraft ParseDraftFromText(Uri sourceUrl, string html)
    {
        var normalizedText = NormalizeWhitespace(StripHtml(html));
        var lines = normalizedText
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        var title = ExtractTitle(html) ?? lines.FirstOrDefault() ?? "Imported recipe";
        var description = ExtractDescription(html) ?? lines.Skip(1).FirstOrDefault();
        var ingredients = ExtractSection(lines, new[] { "ingredients", "ingredient" }, new[] { "instructions", "method", "directions", "notes", "nutrition" })
            .Select(line => new RecipeIngredientModel { Name = line })
            .Take(20)
            .ToList();
        var instructions = ExtractSection(lines, new[] { "instructions", "method", "directions" }, new[] { "notes", "nutrition" })
            .Take(20)
            .ToList();

        var warnings = new List<string>();
        if (ingredients.Count == 0)
        {
            warnings.Add("Ingredient extraction was low confidence. Review before saving.");
            ingredients = lines.Skip(2).Take(5).Select(line => new RecipeIngredientModel { Name = line }).ToList();
        }

        if (instructions.Count == 0)
        {
            warnings.Add("Instruction extraction was low confidence. Review before saving.");
            instructions = lines.Skip(Math.Min(lines.Count, 7)).Take(4).ToList();
        }

        if (instructions.Count == 0)
        {
            instructions = ["Review imported source and add preparation steps."];
        }

        return new ImportedRecipeDraft
        {
            Name = title,
            Description = description,
            Category = GuessCategory(lines),
            Ingredients = ingredients,
            Instructions = instructions,
            SourceType = "url",
            SourceUrl = sourceUrl.ToString(),
            Tags = [],
            Warnings = warnings
        };
    }

    private static string GuessCategory(IReadOnlyList<string> lines)
    {
        var text = string.Join(' ', lines).ToLowerInvariant();
        if (text.Contains("breakfast")) return "breakfast";
        if (text.Contains("snack")) return "snack";
        if (text.Contains("lunch")) return "lunch";
        return "dinner";
    }

    private static List<string> ExtractSection(IReadOnlyList<string> lines, IReadOnlyList<string> startHeadings, IReadOnlyList<string> stopHeadings)
    {
        var startIndex = lines.ToList().FindIndex(line => IsHeadingMatch(line, startHeadings));
        if (startIndex < 0)
        {
            return [];
        }

        var sectionLines = new List<string>();
        for (var index = startIndex + 1; index < lines.Count; index++)
        {
            var line = lines[index];
            if (IsHeadingMatch(line, stopHeadings))
            {
                break;
            }

            if (line.Length == 0)
            {
                continue;
            }

            sectionLines.Add(CleanListLine(line));
        }

        return sectionLines;
    }

    private static bool TryParseJsonLdDraft(Uri sourceUrl, string html, out ImportedRecipeDraft draft)
    {
        var scripts = JsonLdScriptRegex().Matches(html);
        foreach (Match scriptMatch in scripts)
        {
            if (!scriptMatch.Success)
            {
                continue;
            }

            var scriptBody = scriptMatch.Groups[1].Value;
            if (TryFindRecipeJsonElement(scriptBody, out var recipeElement))
            {
                draft = BuildDraftFromJsonLd(sourceUrl, recipeElement);
                return true;
            }
        }

        draft = default!;
        return false;
    }

    private static bool TryFindRecipeJsonElement(string json, out JsonElement recipeElement)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (TryFindRecipeJsonElement(doc.RootElement, out var foundRecipeElement))
            {
                recipeElement = foundRecipeElement.Clone();
                return true;
            }
        }
        catch (JsonException)
        {
            recipeElement = default;
            return false;
        }

        recipeElement = default;
        return false;
    }

    private static bool TryFindRecipeJsonElement(JsonElement element, out JsonElement recipeElement)
    {
        if (IsRecipeType(element))
        {
            recipeElement = element;
            return true;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("@graph") && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var graphItem in property.Value.EnumerateArray())
                        {
                            if (TryFindRecipeJsonElement(graphItem, out recipeElement))
                            {
                                return true;
                            }
                        }
                    }

                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array &&
                        TryFindRecipeJsonElement(property.Value, out recipeElement))
                    {
                        return true;
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var arrayItem in element.EnumerateArray())
                {
                    if (TryFindRecipeJsonElement(arrayItem, out recipeElement))
                    {
                        return true;
                    }
                }
                break;
        }

        recipeElement = default;
        return false;
    }

    private static bool IsRecipeType(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty("@type", out var typeElement))
        {
            return false;
        }

        return typeElement.ValueKind switch
        {
            JsonValueKind.String => typeElement.GetString()?.Contains("Recipe", StringComparison.OrdinalIgnoreCase) == true,
            JsonValueKind.Array => typeElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Any(type => type?.Contains("Recipe", StringComparison.OrdinalIgnoreCase) == true),
            _ => false
        };
    }

    private static ImportedRecipeDraft BuildDraftFromJsonLd(Uri sourceUrl, JsonElement recipe)
    {
        var name = GetString(recipe, "name") ?? "Imported recipe";
        var description = GetString(recipe, "description");
        var cuisine = GetFirstStringLike(recipe, "recipeCuisine");
        var category = NormalizeCategory(GetFirstStringLike(recipe, "recipeCategory"));
        var servings = ParseFirstInt(GetFirstStringLike(recipe, "recipeYield"));
        var prepTimeMinutes = ParseDurationMinutes(GetString(recipe, "prepTime"));
        var cookTimeMinutes = ParseDurationMinutes(GetString(recipe, "cookTime"));
        var proteinSource = GetStringListLike(recipe, "proteinSource");
        var cookingMethod = GetStringListLike(recipe, "cookingMethod");
        var tags = GetStringListLike(recipe, "keywords");
        var ingredients = ParseJsonLdIngredients(recipe);
        var instructions = ParseJsonLdInstructions(recipe);

        var warnings = new List<string>();
        if (ingredients.Count == 0)
        {
            warnings.Add("JSON-LD recipe did not include ingredients. Review before saving.");
            ingredients = [new RecipeIngredientModel { Name = "Review source and add ingredients." }];
        }

        if (instructions.Count == 0)
        {
            warnings.Add("JSON-LD recipe did not include instructions. Review before saving.");
            instructions = ["Review source and add preparation steps."];
        }

        return new ImportedRecipeDraft
        {
            Name = name,
            Description = description,
            Category = category,
            Cuisine = cuisine,
            Servings = servings,
            PrepTimeMinutes = prepTimeMinutes,
            CookTimeMinutes = cookTimeMinutes,
            ProteinSource = proteinSource,
            CookingMethod = cookingMethod,
            Tags = tags,
            Ingredients = ingredients,
            Instructions = instructions,
            SourceType = "url",
            SourceUrl = sourceUrl.ToString(),
            Warnings = warnings
        };
    }

    private static List<RecipeIngredientModel> ParseJsonLdIngredients(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("recipeIngredient", out var ingredientsElement))
        {
            return [];
        }

        var rawItems = ExtractStringValues(ingredientsElement, "text", "name", "itemListElement");
        return rawItems
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => new RecipeIngredientModel { Name = value.Trim() })
            .Take(30)
            .ToList();
    }

    private static List<string> ParseJsonLdInstructions(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("recipeInstructions", out var instructionsElement))
        {
            return [];
        }

        var instructions = new List<string>();

        switch (instructionsElement.ValueKind)
        {
            case JsonValueKind.String:
                instructions.AddRange(instructionsElement.GetString()?
                    .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    ?? []);
                break;
            case JsonValueKind.Array:
                foreach (var item in instructionsElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            instructions.Add(value.Trim());
                        }

                        continue;
                    }

                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        var text = GetString(item, "text") ?? GetString(item, "name");
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            instructions.Add(text.Trim());
                        }
                    }
                }
                break;
            case JsonValueKind.Object:
                // Some sites wrap steps in HowToSection/itemListElement objects.
                instructions.AddRange(ExtractStringValues(instructionsElement, "text", "name", "itemListElement"));
                break;
        }

        return instructions.Take(30).ToList();
    }

    private static string? GetFirstStringLike(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return ExtractStringValues(property, "text", "name").FirstOrDefault();
    }

    private static List<string> GetStringListLike(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return [];
        }

        return ExtractStringValues(property, "text", "name")
            .SelectMany(ParseDelimitedList)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static List<string> ExtractStringValues(JsonElement element, params string[] objectPropertyNames)
    {
        var result = new List<string>();

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value.Trim());
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    result.AddRange(ExtractStringValues(item, objectPropertyNames));
                }
                break;
            case JsonValueKind.Object:
                foreach (var propertyName in objectPropertyNames)
                {
                    if (element.TryGetProperty(propertyName, out var nested))
                    {
                        result.AddRange(ExtractStringValues(nested, objectPropertyNames));
                    }
                }
                break;
        }

        return result;
    }

    private static string NormalizeCategory(string? rawCategory)
    {
        if (string.IsNullOrWhiteSpace(rawCategory))
        {
            return "dinner";
        }

        var normalized = rawCategory.Trim().ToLowerInvariant();
        if (normalized.Contains("breakfast")) return "breakfast";
        if (normalized.Contains("lunch")) return "lunch";
        if (normalized.Contains("snack")) return "snack";
        return "dinner";
    }

    private static List<string> ParseDelimitedList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static int? ParseFirstInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = FirstIntRegex().Match(value);
        if (!digits.Success)
        {
            return null;
        }

        return int.TryParse(digits.Value, out var parsed) ? parsed : null;
    }

    private static int? ParseDurationMinutes(string? isoDuration)
    {
        if (string.IsNullOrWhiteSpace(isoDuration))
        {
            return null;
        }

        try
        {
            var duration = System.Xml.XmlConvert.ToTimeSpan(isoDuration);
            return (int)Math.Round(duration.TotalMinutes);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private static bool IsHeadingMatch(string line, IReadOnlyList<string> headings)
    {
        var normalizedLine = line.Trim().ToLowerInvariant();

        return headings.Any(heading =>
            normalizedLine.Equals(heading, StringComparison.OrdinalIgnoreCase) ||
            normalizedLine.Equals($"{heading}:", StringComparison.OrdinalIgnoreCase) ||
            normalizedLine.StartsWith($"{heading} ", StringComparison.OrdinalIgnoreCase) ||
            normalizedLine.StartsWith($"{heading}:", StringComparison.OrdinalIgnoreCase));
    }

    private static string CleanListLine(string line)
    {
        var cleaned = line.Trim();
        cleaned = BulletPrefixRegex().Replace(cleaned, string.Empty);
        cleaned = NumberPrefixRegex().Replace(cleaned, string.Empty);
        return cleaned.Trim();
    }

    private static string? ExtractTitle(string html)
    {
        var match = TitleRegex().Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value.Trim()) : null;
    }

    private static string? ExtractDescription(string html)
    {
        var match = MetaDescriptionRegex().Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value.Trim()) : null;
    }

    private static string StripHtml(string html)
    {
        var withoutScript = ScriptRegex().Replace(html, " ");
        var withoutStyle = StyleRegex().Replace(withoutScript, " ");
        var withLineBreaks = BreakRegex().Replace(withoutStyle, "\n");
        var stripped = TagRegex().Replace(withLineBreaks, " ");
        return WebUtility.HtmlDecode(stripped);
    }

    private static string NormalizeWhitespace(string input)
    {
        return MultiWhitespaceRegex().Replace(input.Replace("\r", string.Empty), " ")
            .Replace(" \n", "\n")
            .Replace("\n ", "\n");
    }

    [GeneratedRegex("<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("<meta[^>]*name=[\"']description[\"'][^>]*content=[\"'](.*?)[\"'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MetaDescriptionRegex();

    [GeneratedRegex("<script[\\s\\S]*?</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptRegex();

    [GeneratedRegex("<style[\\s\\S]*?</style>", RegexOptions.IgnoreCase)]
    private static partial Regex StyleRegex();

    [GeneratedRegex("<(br|p|div|li|h1|h2|h3|h4|section|article)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.IgnoreCase)]
    private static partial Regex TagRegex();

    [GeneratedRegex("[\\t ]+")]
    private static partial Regex MultiWhitespaceRegex();

    [GeneratedRegex("<script[^>]*type=[\"']application/ld\\+json[\"'][^>]*>([\\s\\S]*?)</script>", RegexOptions.IgnoreCase)]
    private static partial Regex JsonLdScriptRegex();

    [GeneratedRegex("\\d+")]
    private static partial Regex FirstIntRegex();

    [GeneratedRegex("^[-*•]+\\s*")]
    private static partial Regex BulletPrefixRegex();

    [GeneratedRegex("^\\d+[.)]\\s*")]
    private static partial Regex NumberPrefixRegex();
}