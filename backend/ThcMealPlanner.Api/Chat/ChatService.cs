using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ThcMealPlanner.Api.GroceryLists;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Api.Profiles;
using ThcMealPlanner.Api.Recipes;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Api.Chat;

public interface IChatService
{
    Task<ChatMessageResponse> SendMessageAsync(
        string familyId,
        string userId,
        string userName,
        ChatMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(
        string userId,
        string? conversationId,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class ChatService : IChatService
{
        private const string GenerateMealPlanToolName = "generate_meal_plan";
        private const string SearchRecipesToolName = "search_recipes";
        private const string CreateRecipeToolName = "create_recipe";
        private const string ManageGroceryListToolName = "manage_grocery_list";
        private const string UpdateProfileToolName = "update_profile";
        private const string GetNutritionalInfoToolName = "get_nutritional_info";
        private const string ManagePantryToolName = "manage_pantry";

        private const string ToolsJson = """
                [
                    {
                        "type":"function",
                        "function":{
                            "name":"generate_meal_plan",
                            "description":"Generate a weekly meal plan for the family.",
                            "parameters":{
                                "type":"object",
                                "properties":{
                                    "weekStartDate":{"type":"string","description":"ISO date (Monday) for plan week"}
                                },
                                "required":["weekStartDate"]
                            }
                        }
                    },
                    {
                        "type":"function",
                        "function":{
                            "name":"search_recipes",
                            "description":"Search family recipes.",
                            "parameters":{
                                "type":"object",
                                "properties":{
                                    "query":{"type":"string"},
                                    "cuisine":{"type":"string"},
                                    "category":{"type":"string"},
                                    "maxPrepTime":{"type":"number"},
                                    "tags":{"type":"array","items":{"type":"string"}}
                                }
                            }
                        }
                    },
                    {
                        "type":"function",
                        "function":{
                            "name":"create_recipe",
                            "description":"Create a new recipe.",
                            "parameters":{
                                "type":"object",
                                "properties":{
                                    "name":{"type":"string"},
                                    "category":{"type":"string"},
                                    "cuisine":{"type":"string"},
                                    "servings":{"type":"number"},
                                    "prepTime":{"type":"number"},
                                    "cookTime":{"type":"number"},
                                    "ingredients":{"type":"array","items":{"type":"object"}},
                                    "instructions":{"type":"array","items":{"type":"string"}},
                                    "tags":{"type":"array","items":{"type":"string"}}
                                },
                                "required":["name","category","ingredients","instructions"]
                            }
                        }
                    },
                    {
                        "type":"function",
                        "function":{
                            "name":"manage_grocery_list",
                            "description":"Manage grocery list items.",
                            "parameters":{
                                "type":"object",
                                "properties":{
                                    "action":{"type":"string","enum":["add_items","list","clear_completed"]},
                                    "items":{"type":"array","items":{"type":"object"}}
                                },
                                "required":["action"]
                            }
                        }
                    },
                    {
                        "type":"function",
                        "function":{
                            "name":"update_profile",
                            "description":"Update user profile fields.",
                            "parameters":{
                                "type":"object",
                                "properties":{
                                    "userId":{"type":"string"},
                                    "updates":{"type":"object"}
                                },
                                "required":["updates"]
                            }
                        }
                    },
                    {
                        "type":"function",
                        "function":{
                            "name":"get_nutritional_info",
                            "description":"Get nutrition summary for recipe ids.",
                            "parameters":{
                                "type":"object",
                                "properties":{
                                    "recipeIds":{"type":"array","items":{"type":"string"}}
                                }
                            }
                        }
                    },
                    {
                        "type":"function",
                        "function":{
                            "name":"manage_pantry",
                            "description":"Manage pantry staples.",
                            "parameters":{
                                "type":"object",
                                "properties":{
                                    "action":{"type":"string","enum":["add_items","list"]},
                                    "items":{"type":"array","items":{"type":"object"}}
                                },
                                "required":["action"]
                            }
                        }
                    }
                ]
                """;

    private static readonly HashSet<string> DomainKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "meal", "meals", "plan", "recipe", "recipes", "cookbook", "grocery", "groceries", "pantry", "dinner", "lunch", "breakfast", "allergy", "nutrition"
    };

    private readonly IDynamoDbRepository<ChatHistoryMessageDocument> _chatHistoryRepository;
    private readonly IOpenAiApiKeyProvider _apiKeyProvider;
    private readonly OpenAiOptions _options;
    private readonly IMealPlanService _mealPlanService;
    private readonly IRecipeService _recipeService;
    private readonly IGroceryListService _groceryListService;
    private readonly IDynamoDbRepository<UserProfileDocument> _profileRepository;
    private readonly IDependentProfileService _dependentProfileService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IDynamoDbRepository<ChatHistoryMessageDocument> chatHistoryRepository,
        IOpenAiApiKeyProvider apiKeyProvider,
        Microsoft.Extensions.Options.IOptions<OpenAiOptions> options,
        IMealPlanService mealPlanService,
        IRecipeService recipeService,
        IGroceryListService groceryListService,
        IDynamoDbRepository<UserProfileDocument> profileRepository,
        IDependentProfileService dependentProfileService,
        HttpClient httpClient,
        ILogger<ChatService> logger)
    {
        _chatHistoryRepository = chatHistoryRepository;
        _apiKeyProvider = apiKeyProvider;
        _options = options.Value;
        _mealPlanService = mealPlanService;
        _recipeService = recipeService;
        _groceryListService = groceryListService;
        _profileRepository = profileRepository;
        _dependentProfileService = dependentProfileService;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ChatMessageResponse> SendMessageAsync(
        string familyId,
        string userId,
        string userName,
        ChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? $"conv_{Guid.NewGuid():N}"
            : request.ConversationId!.Trim();

        var userMessage = request.Message.Trim();
        var isConfirmationResponse = IsConfirmationResponse(userMessage);
        var isCancellationResponse = IsCancellationResponse(userMessage);
        var requiresConfirmation = !isConfirmationResponse && !isCancellationResponse && IsDestructiveIntent(userMessage);

        var toolActions = new List<ChatActionDocument>();
        PendingConfirmationDocument? pendingConfirmation;
        string assistantContent;

        if (isConfirmationResponse || isCancellationResponse)
        {
            var pending = await TryGetLatestPendingConfirmationAsync(userId, conversationId, cancellationToken);
            assistantContent = await HandleConfirmationReplyAsync(
                familyId,
                userId,
                userName,
                conversationId,
                userMessage,
                pending,
                toolActions,
                cancellationToken);

            requiresConfirmation = false;
            pendingConfirmation = null;
        }
        else
        {
            assistantContent = await BuildAssistantContentAsync(
                familyId,
                userId,
                userName,
                userMessage,
                requiresConfirmation,
                toolActions,
                cancellationToken);

            pendingConfirmation = requiresConfirmation
                ? new PendingConfirmationDocument
                {
                    ActionType = "destructive_action",
                    Prompt = "Please confirm this destructive action."
                }
                : null;
        }

        var assistantRole = ChatConstants.AssistantRole;

        await PersistMessageAsync(
            familyId,
            userId,
            conversationId,
            ChatConstants.UserRole,
            userMessage,
            now,
            pendingConfirmation: null,
            actions: null,
            cancellationToken);

        var assistantTimestamp = DateTimeOffset.UtcNow;
        await PersistMessageAsync(
            familyId,
            userId,
            conversationId,
            assistantRole,
            assistantContent,
            assistantTimestamp,
            pendingConfirmation,
            toolActions,
            cancellationToken);

        return new ChatMessageResponse
        {
            ConversationId = conversationId,
            AssistantMessage = new ChatMessage
            {
                Role = assistantRole,
                Content = assistantContent,
                Timestamp = assistantTimestamp,
                RequiresConfirmation = requiresConfirmation,
                PendingActionType = pendingConfirmation?.ActionType
            }
        };
    }

    public async Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(
        string userId,
        string? conversationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var partitionKey = $"USER#{userId}";
        var records = await _chatHistoryRepository.QueryByPartitionKeyAsync(partitionKey, cancellationToken: cancellationToken);

        var filtered = records
            .Where(record => string.IsNullOrWhiteSpace(conversationId) || string.Equals(record.ConversationId, conversationId, StringComparison.Ordinal))
            .OrderBy(record => record.CreatedAt)
            .ToList();

        if (filtered.Count > limit)
        {
            filtered = filtered.TakeLast(limit).ToList();
        }

        return filtered.Select(record => new ChatMessage
        {
            Role = record.Role,
            Content = record.Content,
            Timestamp = record.CreatedAt,
            RequiresConfirmation = record.PendingConfirmation is not null,
            PendingActionType = record.PendingConfirmation?.ActionType
        }).ToList();
    }

    private async Task<string> BuildAssistantContentAsync(
        string familyId,
        string userId,
        string userName,
        string userMessage,
        bool requiresConfirmation,
        List<ChatActionDocument> toolActions,
        CancellationToken cancellationToken)
    {
        if (requiresConfirmation)
        {
            return "This looks like a destructive action. Reply with **Confirm** to proceed, or tell me what you want to change.";
        }

        if (!IsInDomain(userMessage))
        {
            return "I can help with meal planning, recipes, grocery lists, pantry staples, and nutrition. Tell me what you want to plan or change.";
        }

        var openAiReply = await TryGetOpenAiReplyAsync(
            familyId,
            userId,
            userName,
            userMessage,
            toolActions,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(openAiReply))
        {
            return openAiReply.Trim();
        }

        return "I can help with that. Try asking for a weekly meal plan, grocery list updates, recipe ideas, or pantry management.";
    }

    private async Task<string?> TryGetOpenAiReplyAsync(
        string familyId,
        string userId,
        string userName,
        string userMessage,
        List<ChatActionDocument> toolActions,
        CancellationToken cancellationToken)
    {
        var apiKey = await _apiKeyProvider.GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var systemContent = await BuildSystemPromptAsync(familyId, userId, cancellationToken);

        var userContent = $"User: {userName}\\nMessage: {userMessage}";
        var payload = BuildChatPayloadJson(_options.Model, _options.Temperature, systemContent, userContent, ToolsJson);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Chat completion request failed with status {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var toolCalls = ExtractToolCalls(body);
            if (toolCalls.Count > 0)
            {
                var execution = await ExecuteToolCallAsync(
                    toolCalls[0],
                    familyId,
                    userId,
                    userName,
                    cancellationToken);

                toolActions.Add(execution.Action);
                return execution.UserFacingMessage;
            }

            return ExtractMessageContent(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat completion request failed. Falling back to deterministic assistant response.");
            return null;
        }
    }

    private async Task PersistMessageAsync(
        string familyId,
        string userId,
        string conversationId,
        string role,
        string content,
        DateTimeOffset createdAt,
        PendingConfirmationDocument? pendingConfirmation,
        List<ChatActionDocument>? actions,
        CancellationToken cancellationToken)
    {
        var key = new DynamoDbKey(
            $"USER#{userId}",
            $"MSG#{createdAt.UtcDateTime:O}#{Guid.NewGuid():N}");

        var doc = new ChatHistoryMessageDocument
        {
            FamilyId = familyId,
            UserId = userId,
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAt = createdAt,
            TTL = createdAt.AddDays(30).ToUnixTimeSeconds(),
            PendingConfirmation = pendingConfirmation,
            Actions = actions ?? []
        };

        await _chatHistoryRepository.PutAsync(key, doc, cancellationToken);
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

    private static bool IsInDomain(string message)
    {
        return DomainKeywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDestructiveIntent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.ToLowerInvariant();
        var hasAction = normalized.Contains("delete", StringComparison.Ordinal)
            || normalized.Contains("clear", StringComparison.Ordinal)
            || normalized.Contains("remove", StringComparison.Ordinal);

        if (!hasAction)
        {
            return false;
        }

        return normalized.Contains("meal plan", StringComparison.Ordinal)
            || normalized.Contains("grocery", StringComparison.Ordinal)
            || normalized.Contains("conversation", StringComparison.Ordinal)
            || normalized.Contains("history", StringComparison.Ordinal)
            || normalized.Contains("recipe", StringComparison.Ordinal);
    }

    private static bool IsConfirmationResponse(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        return normalized is "confirm" or "yes" or "proceed";
    }

    private static bool IsCancellationResponse(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        return normalized is "cancel" or "no" or "stop";
    }

    private async Task<ChatHistoryMessageDocument?> TryGetLatestPendingConfirmationAsync(
        string userId,
        string conversationId,
        CancellationToken cancellationToken)
    {
        var partitionKey = $"USER#{userId}";
        var records = await _chatHistoryRepository.QueryByPartitionKeyAsync(partitionKey, cancellationToken: cancellationToken);

        return records
            .Where(record => string.Equals(record.ConversationId, conversationId, StringComparison.Ordinal))
            .Where(record => string.Equals(record.Role, ChatConstants.AssistantRole, StringComparison.Ordinal))
            .Where(record => record.PendingConfirmation is not null)
            .OrderByDescending(record => record.CreatedAt)
            .FirstOrDefault();
    }

    private async Task<string> HandleConfirmationReplyAsync(
        string familyId,
        string userId,
        string userName,
        string conversationId,
        string userMessage,
        ChatHistoryMessageDocument? pendingMessage,
        List<ChatActionDocument> toolActions,
        CancellationToken cancellationToken)
    {
        if (pendingMessage?.PendingConfirmation is null)
        {
            return "There is no pending action to confirm right now.";
        }

        if (IsCancellationResponse(userMessage))
        {
            toolActions.Add(new ChatActionDocument
            {
                Type = pendingMessage.PendingConfirmation.ActionType,
                Status = "canceled",
                Result = "User canceled action"
            });

            return "Canceled. I did not apply that action.";
        }

        if (!string.Equals(pendingMessage.PendingConfirmation.ActionType, "destructive_action", StringComparison.Ordinal))
        {
            return "I cannot execute that pending action yet.";
        }

        var partitionKey = $"USER#{userId}";
        var records = await _chatHistoryRepository.QueryByPartitionKeyAsync(partitionKey, cancellationToken: cancellationToken);
        var priorUserMessage = records
            .Where(record => string.Equals(record.ConversationId, conversationId, StringComparison.Ordinal))
            .Where(record => string.Equals(record.Role, ChatConstants.UserRole, StringComparison.Ordinal))
            .Where(record => record.CreatedAt <= pendingMessage.CreatedAt)
            .OrderByDescending(record => record.CreatedAt)
            .Select(record => record.Content)
            .FirstOrDefault();

        if (priorUserMessage is not null && MentionsClearCompletedGrocery(priorUserMessage))
        {
            var removedCount = await ClearCompletedGroceryItemsAsync(familyId, cancellationToken);
            toolActions.Add(new ChatActionDocument
            {
                Type = "clear_completed_grocery",
                Status = "succeeded",
                Result = $"Removed {removedCount}"
            });

            return removedCount == 0
                ? "No completed grocery items needed clearing."
                : $"Cleared {removedCount} completed grocery item(s).";
        }

        toolActions.Add(new ChatActionDocument
        {
            Type = "destructive_action",
            Status = "failed",
            Result = "Pending action not executable"
        });

        return "I need a more specific action before I can execute that confirmation.";
    }

    private static bool MentionsClearCompletedGrocery(string message)
    {
        var normalized = message.ToLowerInvariant();
        return (normalized.Contains("grocery", StringComparison.Ordinal) || normalized.Contains("list", StringComparison.Ordinal))
            && (normalized.Contains("clear", StringComparison.Ordinal) || normalized.Contains("remove", StringComparison.Ordinal))
            && normalized.Contains("complete", StringComparison.Ordinal);
    }

    private async Task<int> ClearCompletedGroceryItemsAsync(string familyId, CancellationToken cancellationToken)
    {
        var current = await _groceryListService.GetCurrentAsync(familyId, cancellationToken);
        if (current is null)
        {
            return 0;
        }

        var completedItems = current.Items.Where(item => item.CheckedOff).ToList();
        if (completedItems.Count == 0)
        {
            return 0;
        }

        var removedCount = 0;
        var latest = current;

        foreach (var item in completedItems)
        {
            var mutation = await _groceryListService.RemoveItemAsync(
                familyId,
                item.Id,
                new RemoveGroceryItemRequest
                {
                    Version = latest.Version
                },
                cancellationToken);

            if (mutation.Status == GroceryItemMutationStatus.Success && mutation.List is not null)
            {
                latest = mutation.List;
                removedCount += 1;
            }
        }

        return removedCount;
    }

        private static string BuildChatPayloadJson(
                string model,
                double temperature,
                string systemContent,
                string userContent,
                string toolsJson)
        {
                return string.Create(CultureInfo.InvariantCulture, $$"""
                {
                    "model":"{{EscapeJson(model)}}",
                    "temperature":{{temperature}},
                    "messages":[
                        {
                            "role":"system",
                            "content":"{{EscapeJson(systemContent)}}"
                        },
                        {
                            "role":"user",
                            "content":"{{EscapeJson(userContent)}}"
                        }
                    ],
                    "tools":{{toolsJson}},
                    "tool_choice":"auto"
                }
                """);
        }

        private static string EscapeJson(string value)
        {
                return JavaScriptEncoder.Default.Encode(value);
        }

        private async Task<string> BuildSystemPromptAsync(string familyId, string userId, CancellationToken cancellationToken)
        {
            var profile = await _profileRepository.GetAsync(new DynamoDbKey($"USER#{userId}", "PROFILE"), cancellationToken);
            var dependents = await _dependentProfileService.ListByFamilyAsync(familyId, cancellationToken);
            var currentPlan = await _mealPlanService.GetCurrentAsync(familyId, cancellationToken);
            var currentGrocery = await _groceryListService.GetCurrentAsync(familyId, cancellationToken);

            var profileLine = profile is null
                ? "Primary profile: not configured yet."
                : $"Primary profile: {profile.Name}. Dietary prefs: {FormatList(profile.DietaryPrefs)}. Exclusions: {FormatList(profile.ExcludedIngredients)}. Allergies: {FormatAllergies(profile.Allergies)}.";

            var dependentLines = dependents.Count == 0
                ? "Dependents: none configured."
                : "Dependents:\n" + string.Join('\n', dependents.Select(dep =>
                    $"- {dep.Name} ({dep.AgeGroup ?? "age unknown"}): dietary prefs {FormatList(dep.DietaryPrefs)}, avoids {FormatList(dep.AvoidedFoods)}, allergies {FormatAllergies(dep.Allergies)}."));

            var mealPlanLine = currentPlan is null
                ? "Active meal plan: none."
                : $"Active meal plan: week {currentPlan.WeekStartDate}, {currentPlan.Meals.Count} scheduled meals.";

            var groceryLine = currentGrocery is null
                ? "Active grocery list: none."
                : $"Active grocery list: {currentGrocery.Items.Count} items, {currentGrocery.Progress.Completed} completed.";

            return string.Join('\n',
            [
                "You are a family meal planning assistant.",
                "Only help with meal planning, recipes, grocery lists, pantry staples, and nutrition.",
                "Never execute destructive actions without explicit user confirmation.",
                "Always respect allergies and excluded ingredients for all family members.",
                "Respond in concise markdown.",
                string.Empty,
                profileLine,
                dependentLines,
                mealPlanLine,
                groceryLine
            ]);
        }

        private static string FormatList(IReadOnlyCollection<string>? values)
        {
            if (values is null || values.Count == 0)
            {
                return "none";
            }

            return string.Join(", ", values);
        }

        private static string FormatAllergies(IReadOnlyCollection<AllergyModel>? allergies)
        {
            if (allergies is null || allergies.Count == 0)
            {
                return "none";
            }

            return string.Join(", ", allergies.Select(allergy =>
                string.IsNullOrWhiteSpace(allergy.Severity)
                    ? allergy.Allergen
                    : $"{allergy.Allergen} ({allergy.Severity})"));
        }

        private static List<ChatToolCall> ExtractToolCalls(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                {
                    return [];
                }

                var first = choices[0];
                if (!first.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
                {
                    return [];
                }

                if (!message.TryGetProperty("tool_calls", out var toolCallsElement) || toolCallsElement.ValueKind != JsonValueKind.Array)
                {
                    return [];
                }

                var results = new List<ChatToolCall>();
                foreach (var toolCallElement in toolCallsElement.EnumerateArray())
                {
                    if (!toolCallElement.TryGetProperty("function", out var functionElement) || functionElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!functionElement.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var name = nameElement.GetString();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var arguments = functionElement.TryGetProperty("arguments", out var argumentsElement) && argumentsElement.ValueKind == JsonValueKind.String
                        ? argumentsElement.GetString() ?? "{}"
                        : "{}";

                    results.Add(new ChatToolCall(name, arguments));
                }

                return results;
            }
            catch
            {
                return [];
            }
        }

        private async Task<ChatToolExecutionResult> ExecuteToolCallAsync(
            ChatToolCall toolCall,
            string familyId,
            string userId,
            string userName,
            CancellationToken cancellationToken)
        {
            try
            {
                return toolCall.Name switch
                {
                    GenerateMealPlanToolName => await ExecuteGenerateMealPlanAsync(toolCall.ArgumentsJson, familyId, userId, userName, cancellationToken),
                    SearchRecipesToolName => await ExecuteSearchRecipesAsync(toolCall.ArgumentsJson, familyId, cancellationToken),
                    CreateRecipeToolName => await ExecuteCreateRecipeAsync(toolCall.ArgumentsJson, familyId, userId, cancellationToken),
                    ManageGroceryListToolName => await ExecuteManageGroceryListAsync(toolCall.ArgumentsJson, familyId, userId, userName, cancellationToken),
                    UpdateProfileToolName => await ExecuteUpdateProfileAsync(toolCall.ArgumentsJson, familyId, userId, cancellationToken),
                    GetNutritionalInfoToolName => await ExecuteGetNutritionalInfoAsync(toolCall.ArgumentsJson, familyId, cancellationToken),
                    ManagePantryToolName => await ExecuteManagePantryAsync(toolCall.ArgumentsJson, familyId, cancellationToken),
                    _ => new ChatToolExecutionResult(
                        $"I cannot execute the requested function `{toolCall.Name}` yet.",
                        new ChatActionDocument { Type = toolCall.Name, Status = "ignored", Result = "Unsupported function." })
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tool execution failed for {ToolName}", toolCall.Name);
                return new ChatToolExecutionResult(
                    "I couldn't complete that action right now. Please try again.",
                    new ChatActionDocument { Type = toolCall.Name, Status = "failed", Result = "Execution error." });
            }
        }

        private async Task<ChatToolExecutionResult> ExecuteGenerateMealPlanAsync(
            string argumentsJson,
            string familyId,
            string userId,
            string userName,
            CancellationToken cancellationToken)
        {
            using var argsDoc = ParseArguments(argumentsJson);
            var root = argsDoc.RootElement;

            var weekStartDate = root.TryGetProperty("weekStartDate", out var weekElement) && weekElement.ValueKind == JsonValueKind.String
                ? weekElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(weekStartDate) || !DateOnly.TryParse(weekStartDate, out var dateOnly) || dateOnly.DayOfWeek != DayOfWeek.Monday)
            {
                return new ChatToolExecutionResult(
                    "Please provide a valid Monday date in yyyy-MM-dd format for meal plan generation.",
                    new ChatActionDocument { Type = GenerateMealPlanToolName, Status = "failed", Result = "Invalid weekStartDate." });
            }

            var plan = await _mealPlanService.GenerateAsync(
                familyId,
                userId,
                new GenerateMealPlanRequest
                {
                    WeekStartDate = weekStartDate,
                    ReplaceExisting = true
                },
                cancellationToken);

            await _groceryListService.GenerateAsync(
                familyId,
                userId,
                userName,
                new GenerateGroceryListRequest
                {
                    WeekStartDate = weekStartDate,
                    ClearExisting = false
                },
                cancellationToken);

            return new ChatToolExecutionResult(
                $"Generated a meal plan for **{weekStartDate}** with {plan.Meals.Count} meals and refreshed the grocery list.",
                new ChatActionDocument { Type = GenerateMealPlanToolName, Status = "succeeded", Result = weekStartDate });
        }

        private async Task<ChatToolExecutionResult> ExecuteSearchRecipesAsync(
            string argumentsJson,
            string familyId,
            CancellationToken cancellationToken)
        {
            using var argsDoc = ParseArguments(argumentsJson);
            var root = argsDoc.RootElement;

            var query = root.TryGetProperty("query", out var queryElement) && queryElement.ValueKind == JsonValueKind.String
                ? queryElement.GetString()
                : null;
            var cuisine = root.TryGetProperty("cuisine", out var cuisineElement) && cuisineElement.ValueKind == JsonValueKind.String
                ? cuisineElement.GetString()
                : null;
            var category = root.TryGetProperty("category", out var categoryElement) && categoryElement.ValueKind == JsonValueKind.String
                ? categoryElement.GetString()
                : null;
            var maxPrepTime = root.TryGetProperty("maxPrepTime", out var maxPrepElement) && maxPrepElement.TryGetInt32(out var prep)
                ? prep
                : (int?)null;
            var tags = root.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array
                ? tagsElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList()
                : [];

            var recipes = await _recipeService.ListByFamilyAsync(familyId, cancellationToken);
            var filtered = recipes.Where(recipe =>
                (string.IsNullOrWhiteSpace(query) || recipe.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || (recipe.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)) &&
                (string.IsNullOrWhiteSpace(cuisine) || string.Equals(recipe.Cuisine, cuisine, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(category) || string.Equals(recipe.Category, category, StringComparison.OrdinalIgnoreCase)) &&
                (!maxPrepTime.HasValue || ((recipe.PrepTimeMinutes ?? 0) + (recipe.CookTimeMinutes ?? 0)) <= maxPrepTime.Value) &&
                (tags.Count == 0 || tags.All(tag => recipe.Tags.Any(recipeTag => string.Equals(recipeTag, tag, StringComparison.OrdinalIgnoreCase)))))
                .Take(8)
                .ToList();

            if (filtered.Count == 0)
            {
                return new ChatToolExecutionResult(
                    "I couldn't find matching recipes with those filters.",
                    new ChatActionDocument { Type = SearchRecipesToolName, Status = "succeeded", Result = "0 matches" });
            }

            var lines = filtered.Select(recipe =>
            {
                var totalMinutes = (recipe.PrepTimeMinutes ?? 0) + (recipe.CookTimeMinutes ?? 0);
                return $"- **{recipe.Name}** ({recipe.Category}, {totalMinutes} min)";
            });

            var result = "Here are matching recipes:\n" + string.Join("\n", lines);
            return new ChatToolExecutionResult(
                result,
                new ChatActionDocument { Type = SearchRecipesToolName, Status = "succeeded", Result = $"{filtered.Count} matches" });
        }

        private async Task<ChatToolExecutionResult> ExecuteCreateRecipeAsync(
            string argumentsJson,
            string familyId,
            string userId,
            CancellationToken cancellationToken)
        {
            using var argsDoc = ParseArguments(argumentsJson);
            var root = argsDoc.RootElement;

            var name = root.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString()
                : null;
            var category = root.TryGetProperty("category", out var categoryElement) && categoryElement.ValueKind == JsonValueKind.String
                ? categoryElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category))
            {
                return new ChatToolExecutionResult(
                    "To create a recipe, I need at least a name and category.",
                    new ChatActionDocument { Type = CreateRecipeToolName, Status = "failed", Result = "Missing name or category." });
            }

            var ingredients = ParseRecipeIngredients(root);
            var instructions = root.TryGetProperty("instructions", out var instructionsElement) && instructionsElement.ValueKind == JsonValueKind.Array
                ? instructionsElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList()
                : [];

            if (ingredients.Count == 0 || instructions.Count == 0)
            {
                return new ChatToolExecutionResult(
                    "To create a recipe, please include both ingredients and instructions.",
                    new ChatActionDocument { Type = CreateRecipeToolName, Status = "failed", Result = "Missing ingredients or instructions." });
            }

            var request = new CreateRecipeRequest
            {
                Name = name,
                Category = category,
                Cuisine = root.TryGetProperty("cuisine", out var cuisineElement) && cuisineElement.ValueKind == JsonValueKind.String ? cuisineElement.GetString() : null,
                Servings = TryReadInt(root, "servings"),
                PrepTimeMinutes = TryReadInt(root, "prepTime"),
                CookTimeMinutes = TryReadInt(root, "cookTime"),
                Ingredients = ingredients,
                Instructions = instructions,
                Tags = root.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array
                    ? tagsElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList()
                    : []
            };

            var recipe = await _recipeService.CreateAsync(familyId, userId, request, cancellationToken);

            return new ChatToolExecutionResult(
                $"Created recipe **{recipe.Name}** in the {recipe.Category} category.",
                new ChatActionDocument { Type = CreateRecipeToolName, Status = "succeeded", Result = recipe.RecipeId });
        }

        private async Task<ChatToolExecutionResult> ExecuteManageGroceryListAsync(
            string argumentsJson,
            string familyId,
            string userId,
            string userName,
            CancellationToken cancellationToken)
        {
            using var argsDoc = ParseArguments(argumentsJson);
            var root = argsDoc.RootElement;

            var action = root.TryGetProperty("action", out var actionElement) && actionElement.ValueKind == JsonValueKind.String
                ? actionElement.GetString()
                : "list";

            if (string.Equals(action, "list", StringComparison.OrdinalIgnoreCase))
            {
                var current = await _groceryListService.GetCurrentAsync(familyId, cancellationToken);
                if (current is null)
                {
                    return new ChatToolExecutionResult(
                        "There is no active grocery list yet.",
                        new ChatActionDocument { Type = ManageGroceryListToolName, Status = "succeeded", Result = "No active list" });
                }

                var lines = current.Items.Take(12).Select(item => $"- {item.Name} ({item.Section})");
                var content = "Current grocery list:\n" + string.Join("\n", lines);
                return new ChatToolExecutionResult(
                    content,
                    new ChatActionDocument { Type = ManageGroceryListToolName, Status = "succeeded", Result = $"{current.Items.Count} items" });
            }

            if (!string.Equals(action, "add_items", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(action, "clear_completed", StringComparison.OrdinalIgnoreCase))
                {
                    var removedCount = await ClearCompletedGroceryItemsAsync(familyId, cancellationToken);
                    return new ChatToolExecutionResult(
                        removedCount == 0
                            ? "No completed grocery items needed clearing."
                            : $"Cleared {removedCount} completed grocery item(s).",
                        new ChatActionDocument { Type = ManageGroceryListToolName, Status = "succeeded", Result = $"Cleared {removedCount}" });
                }

                return new ChatToolExecutionResult(
                    "I currently support grocery list actions: add_items and list.",
                    new ChatActionDocument { Type = ManageGroceryListToolName, Status = "ignored", Result = "Unsupported action" });
            }

            var currentList = await _groceryListService.GetCurrentAsync(familyId, cancellationToken)
                ?? await _groceryListService.GenerateAsync(
                    familyId,
                    userId,
                    userName,
                    new GenerateGroceryListRequest { ClearExisting = false },
                    cancellationToken);

            if (!root.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
            {
                return new ChatToolExecutionResult(
                    "Please provide items to add.",
                    new ChatActionDocument { Type = ManageGroceryListToolName, Status = "failed", Result = "Missing items" });
            }

            var addedCount = 0;
            foreach (var itemElement in itemsElement.EnumerateArray())
            {
                if (itemElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = itemElement.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var section = itemElement.TryGetProperty("section", out var sectionElement) && sectionElement.ValueKind == JsonValueKind.String
                    ? sectionElement.GetString()
                    : "other";

                var quantity = itemElement.TryGetProperty("quantity", out var quantityElement) && quantityElement.TryGetDecimal(out var parsedQuantity)
                    ? parsedQuantity
                    : 1m;

                var unit = itemElement.TryGetProperty("unit", out var unitElement) && unitElement.ValueKind == JsonValueKind.String
                    ? unitElement.GetString()
                    : null;

                var mutation = await _groceryListService.AddItemAsync(
                    familyId,
                    new AddGroceryItemRequest
                    {
                        Name = name,
                        Section = string.IsNullOrWhiteSpace(section) ? "other" : section,
                        Quantity = quantity,
                        Unit = unit,
                        Version = currentList.Version
                    },
                    cancellationToken);

                if (mutation.Status == GroceryItemMutationStatus.Success)
                {
                    addedCount += 1;
                    currentList = mutation.List!;
                }
            }

            return new ChatToolExecutionResult(
                $"Added {addedCount} item(s) to the grocery list.",
                new ChatActionDocument { Type = ManageGroceryListToolName, Status = "succeeded", Result = $"Added {addedCount}" });
        }

        private async Task<ChatToolExecutionResult> ExecuteGetNutritionalInfoAsync(
            string argumentsJson,
            string familyId,
            CancellationToken cancellationToken)
        {
            using var argsDoc = ParseArguments(argumentsJson);
            var root = argsDoc.RootElement;

            if (!root.TryGetProperty("recipeIds", out var recipeIdsElement) || recipeIdsElement.ValueKind != JsonValueKind.Array)
            {
                return new ChatToolExecutionResult(
                    "Please provide recipeIds to summarize nutrition.",
                    new ChatActionDocument { Type = GetNutritionalInfoToolName, Status = "failed", Result = "Missing recipeIds" });
            }

            var recipeIds = recipeIdsElement
                .EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? string.Empty)
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipeIds.Count == 0)
            {
                return new ChatToolExecutionResult(
                    "Please provide at least one recipe id.",
                    new ChatActionDocument { Type = GetNutritionalInfoToolName, Status = "failed", Result = "No recipe ids" });
            }

            var recipes = await _recipeService.ListByFamilyAsync(familyId, cancellationToken);
            var selected = recipes.Where(r => recipeIds.Contains(r.RecipeId, StringComparer.OrdinalIgnoreCase)).ToList();

            if (selected.Count == 0)
            {
                return new ChatToolExecutionResult(
                    "I couldn't find those recipes in your cookbook.",
                    new ChatActionDocument { Type = GetNutritionalInfoToolName, Status = "succeeded", Result = "0 matches" });
            }

            var calories = selected.Sum(r => r.Nutrition?.Calories ?? 0);
            var protein = selected.Sum(r => r.Nutrition?.Protein ?? 0);
            var carbs = selected.Sum(r => r.Nutrition?.Carbohydrates ?? 0);
            var fat = selected.Sum(r => r.Nutrition?.Fat ?? 0);

            return new ChatToolExecutionResult(
                $"Nutrition summary for {selected.Count} recipe(s): {calories} kcal, {protein}g protein, {carbs}g carbs, {fat}g fat.",
                new ChatActionDocument { Type = GetNutritionalInfoToolName, Status = "succeeded", Result = $"{selected.Count} recipes" });
        }

        private async Task<ChatToolExecutionResult> ExecuteUpdateProfileAsync(
            string argumentsJson,
            string familyId,
            string currentUserId,
            CancellationToken cancellationToken)
        {
            using var argsDoc = ParseArguments(argumentsJson);
            var root = argsDoc.RootElement;

            if (!root.TryGetProperty("updates", out var updatesElement) || updatesElement.ValueKind != JsonValueKind.Object)
            {
                return new ChatToolExecutionResult(
                    "Please include an updates object for profile changes.",
                    new ChatActionDocument { Type = UpdateProfileToolName, Status = "failed", Result = "Missing updates object" });
            }

            var targetUserId = root.TryGetProperty("userId", out var userIdElement) && userIdElement.ValueKind == JsonValueKind.String
                ? userIdElement.GetString()
                : currentUserId;

            if (string.IsNullOrWhiteSpace(targetUserId))
            {
                return new ChatToolExecutionResult(
                    "I could not determine which profile to update.",
                    new ChatActionDocument { Type = UpdateProfileToolName, Status = "failed", Result = "Missing userId" });
            }

            var key = new DynamoDbKey($"USER#{targetUserId}", "PROFILE");
            var existing = await _profileRepository.GetAsync(key, cancellationToken);
            if (existing is not null && !string.Equals(existing.FamilyId, familyId, StringComparison.Ordinal))
            {
                return new ChatToolExecutionResult(
                    "I can only update profiles within your family.",
                    new ChatActionDocument { Type = UpdateProfileToolName, Status = "failed", Result = "Family scope mismatch" });
            }

            var now = DateTimeOffset.UtcNow;
            var updated = new UserProfileDocument
            {
                UserId = existing?.UserId ?? targetUserId,
                Name = ReadOptionalString(updatesElement, "name") ?? existing?.Name ?? "",
                Email = ReadOptionalString(updatesElement, "email") ?? existing?.Email ?? "",
                FamilyId = existing?.FamilyId ?? familyId,
                Role = existing?.Role ?? "member",
                DietaryPrefs = ReadOptionalStringList(updatesElement, "dietaryPrefs") ?? existing?.DietaryPrefs ?? [],
                Allergies = existing?.Allergies ?? [],
                ExcludedIngredients = ReadOptionalStringList(updatesElement, "excludedIngredients") ?? existing?.ExcludedIngredients ?? [],
                MacroTargets = existing?.MacroTargets,
                CuisinePreferences = ReadOptionalStringList(updatesElement, "cuisinePreferences") ?? existing?.CuisinePreferences ?? [],
                CookingConstraints = existing?.CookingConstraints,
                FlavorPreferences = existing?.FlavorPreferences,
                DefaultServings = existing?.DefaultServings,
                FamilyMembers = existing?.FamilyMembers ?? [],
                DoctorNotes = ReadOptionalStringList(updatesElement, "doctorNotes") ?? existing?.DoctorNotes ?? [],
                NotificationPrefs = existing?.NotificationPrefs,
                CreatedAt = existing?.CreatedAt ?? now,
                UpdatedAt = now
            };

            await _profileRepository.PutAsync(key, updated, cancellationToken);

            return new ChatToolExecutionResult(
                $"Updated profile for **{(string.IsNullOrWhiteSpace(updated.Name) ? targetUserId : updated.Name)}**.",
                new ChatActionDocument { Type = UpdateProfileToolName, Status = "succeeded", Result = targetUserId });
        }

        private async Task<ChatToolExecutionResult> ExecuteManagePantryAsync(
            string argumentsJson,
            string familyId,
            CancellationToken cancellationToken)
        {
            using var argsDoc = ParseArguments(argumentsJson);
            var root = argsDoc.RootElement;

            var action = root.TryGetProperty("action", out var actionElement) && actionElement.ValueKind == JsonValueKind.String
                ? actionElement.GetString()
                : "list";

            if (string.Equals(action, "list", StringComparison.OrdinalIgnoreCase))
            {
                var pantry = await _groceryListService.GetPantryStaplesAsync(familyId, cancellationToken);
                if (pantry.Items.Count == 0)
                {
                    return new ChatToolExecutionResult(
                        "Your pantry staples list is empty.",
                        new ChatActionDocument { Type = ManagePantryToolName, Status = "succeeded", Result = "0 items" });
                }

                var lines = pantry.Items.Take(15).Select(item => $"- {item.Name}{(string.IsNullOrWhiteSpace(item.Section) ? string.Empty : $" ({item.Section})")}");
                return new ChatToolExecutionResult(
                    "Pantry staples:\n" + string.Join("\n", lines),
                    new ChatActionDocument { Type = ManagePantryToolName, Status = "succeeded", Result = $"{pantry.Items.Count} items" });
            }

            if (!string.Equals(action, "add_items", StringComparison.OrdinalIgnoreCase))
            {
                return new ChatToolExecutionResult(
                    "I currently support pantry actions: add_items and list.",
                    new ChatActionDocument { Type = ManagePantryToolName, Status = "ignored", Result = "Unsupported action" });
            }

            if (!root.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
            {
                return new ChatToolExecutionResult(
                    "Please provide pantry items to add.",
                    new ChatActionDocument { Type = ManagePantryToolName, Status = "failed", Result = "Missing items" });
            }

            var addedCount = 0;
            foreach (var itemElement in itemsElement.EnumerateArray())
            {
                if (itemElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = itemElement.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var section = itemElement.TryGetProperty("section", out var sectionElement) && sectionElement.ValueKind == JsonValueKind.String
                    ? sectionElement.GetString()
                    : null;

                await _groceryListService.AddPantryStapleAsync(
                    familyId,
                    new AddPantryStapleItemRequest { Name = name, Section = section },
                    cancellationToken);

                addedCount += 1;
            }

            return new ChatToolExecutionResult(
                $"Added {addedCount} item(s) to pantry staples.",
                new ChatActionDocument { Type = ManagePantryToolName, Status = "succeeded", Result = $"Added {addedCount}" });
        }

        private static int? TryReadInt(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.TryGetInt32(out var intValue) ? intValue : null;
        }

        private static string? ReadOptionalString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var value = property.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static List<string>? ReadOptionalStringList(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return property.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToList();
        }

        private static List<RecipeIngredientModel> ParseRecipeIngredients(JsonElement root)
        {
            if (!root.TryGetProperty("ingredients", out var ingredientsElement) || ingredientsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var ingredients = new List<RecipeIngredientModel>();
            foreach (var ingredientElement in ingredientsElement.EnumerateArray())
            {
                if (ingredientElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = ingredientElement.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                ingredients.Add(new RecipeIngredientModel
                {
                    Name = name,
                    Quantity = ingredientElement.TryGetProperty("quantity", out var quantityElement) ? quantityElement.ToString() : null,
                    Unit = ingredientElement.TryGetProperty("unit", out var unitElement) && unitElement.ValueKind == JsonValueKind.String
                        ? unitElement.GetString()
                        : null,
                    Section = ingredientElement.TryGetProperty("section", out var sectionElement) && sectionElement.ValueKind == JsonValueKind.String
                        ? sectionElement.GetString()
                        : null
                });
            }

            return ingredients;
        }

        private static JsonDocument ParseArguments(string argumentsJson)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return JsonDocument.Parse("{}");
            }

            try
            {
                return JsonDocument.Parse(argumentsJson);
            }
            catch
            {
                return JsonDocument.Parse("{}");
            }
        }

        private sealed record ChatToolCall(string Name, string ArgumentsJson);

        private sealed record ChatToolExecutionResult(string UserFacingMessage, ChatActionDocument Action);
}
