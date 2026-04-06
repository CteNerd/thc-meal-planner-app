using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThcMealPlanner.Api.Chat;
using ThcMealPlanner.Api.GroceryLists;
using ThcMealPlanner.Api.MealPlans;
using ThcMealPlanner.Api.Profiles;
using ThcMealPlanner.Api.Recipes;
using ThcMealPlanner.Core.Data;

namespace ThcMealPlanner.Tests;

public sealed class ChatServiceTests
{
    private const string FamilyId = "FAM#test-family";
    private const string UserId = "adult_1";

    [Fact]
    public async Task SendMessageAsync_WhenOutOfDomain_ReturnsDomainGuidance()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
        var service = CreateService(chatRepo, apiKey: null, httpResponse: null);

        var response = await service.SendMessageAsync(
            FamilyId,
            UserId,
            "Adult 1",
            new ChatMessageRequest { Message = "Tell me a joke" });

        response.AssistantMessage.Content.Should().Contain("I can help with meal planning");
        response.AssistantMessage.RequiresConfirmation.Should().BeFalse();

        var history = await chatRepo.QueryByPartitionKeyAsync($"USER#{UserId}");
        history.Should().HaveCount(2);
    }

    [Fact]
    public async Task SendMessageAsync_WhenDestructiveIntent_RequiresConfirmation()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
        var service = CreateService(chatRepo, apiKey: null, httpResponse: null);

        var response = await service.SendMessageAsync(
            FamilyId,
            UserId,
            "Adult 1",
            new ChatMessageRequest
            {
                ConversationId = "conv_destructive",
                Message = "Please delete this meal plan"
            });

        response.AssistantMessage.RequiresConfirmation.Should().BeTrue();
        response.AssistantMessage.PendingActionType.Should().Be("destructive_action");
        response.AssistantMessage.Content.Should().Contain("Reply with **Confirm**");
    }

    [Fact]
    public async Task SendMessageAsync_WhenConfirmWithoutPending_ReturnsNoPendingActionMessage()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
        var service = CreateService(chatRepo, apiKey: null, httpResponse: null);

        var response = await service.SendMessageAsync(
            FamilyId,
            UserId,
            "Adult 1",
            new ChatMessageRequest
            {
                ConversationId = "conv_none",
                Message = "Confirm"
            });

        response.AssistantMessage.Content.Should().Be("There is no pending action to confirm right now.");
    }

    [Fact]
    public async Task SendMessageAsync_WhenOpenAiReturnsMessage_UsesAssistantContent()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
        var openAiBody = """
            {
              "choices": [
                {
                  "message": {
                    "content": "Try a quick taco bowl with pantry beans tonight."
                  }
                }
              ]
            }
            """;

        var service = CreateService(chatRepo, "sk-test", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(openAiBody, Encoding.UTF8, "application/json")
        });

        var response = await service.SendMessageAsync(
            FamilyId,
            UserId,
            "Adult 1",
            new ChatMessageRequest { Message = "Can you suggest a dinner meal plan?" });

        response.AssistantMessage.Content.Should().Be("Try a quick taco bowl with pantry beans tonight.");
    }

    [Fact]
    public async Task SendMessageAsync_WhenOpenAiReturnsUnsupportedToolCall_ReturnsToolExecutionMessage()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
        var openAiBody = """
            {
              "choices": [
                {
                  "message": {
                    "tool_calls": [
                      {
                        "function": {
                          "name": "unsupported_function",
                          "arguments": "{}"
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var service = CreateService(chatRepo, "sk-test", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(openAiBody, Encoding.UTF8, "application/json")
        });

        var response = await service.SendMessageAsync(
            FamilyId,
            UserId,
            "Adult 1",
            new ChatMessageRequest { Message = "Please plan meals for this week" });

        response.AssistantMessage.Content.Should().Contain("I cannot execute the requested function");

        var history = await chatRepo.QueryByPartitionKeyAsync($"USER#{UserId}");
        var assistant = history.Single(x => x.Role == ChatConstants.AssistantRole);
        assistant.Actions.Should().ContainSingle();
        assistant.Actions[0].Type.Should().Be("unsupported_function");
        assistant.Actions[0].Status.Should().Be("ignored");
    }

    [Fact]
    public async Task SendMessageAsync_WhenOpenAiStatusNotSuccess_FallsBackToDeterministicMessage()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
        var service = CreateService(chatRepo, "sk-test", new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });

        var response = await service.SendMessageAsync(
            FamilyId,
            UserId,
            "Adult 1",
            new ChatMessageRequest { Message = "Give me a recipe for dinner" });

        response.AssistantMessage.Content.Should().Contain("Try asking for a weekly meal plan");
    }

    [Fact]
    public async Task GetHistoryAsync_FiltersConversationAndAppliesLimit()
    {
        var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();

        await chatRepo.PutAsync(
            new DynamoDbKey($"USER#{UserId}", "MSG#1"),
            new ChatHistoryMessageDocument
            {
                FamilyId = FamilyId,
                UserId = UserId,
                ConversationId = "conv_a",
                Role = ChatConstants.UserRole,
                Content = "one",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-3)
            });

        await chatRepo.PutAsync(
            new DynamoDbKey($"USER#{UserId}", "MSG#2"),
            new ChatHistoryMessageDocument
            {
                FamilyId = FamilyId,
                UserId = UserId,
                ConversationId = "conv_a",
                Role = ChatConstants.AssistantRole,
                Content = "two",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
            });

        await chatRepo.PutAsync(
            new DynamoDbKey($"USER#{UserId}", "MSG#3"),
            new ChatHistoryMessageDocument
            {
                FamilyId = FamilyId,
                UserId = UserId,
                ConversationId = "conv_b",
                Role = ChatConstants.AssistantRole,
                Content = "three",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            });

        var service = CreateService(chatRepo, apiKey: null, httpResponse: null);

        var history = await service.GetHistoryAsync(UserId, "conv_a", limit: 1);

        history.Should().HaveCount(1);
        history[0].Content.Should().Be("two");
    }

        [Fact]
        public async Task SendMessageAsync_WhenCancelingPendingAction_ReturnsCanceledMessage()
        {
                var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
                var service = CreateService(chatRepo, apiKey: null, httpResponse: null);

                await service.SendMessageAsync(
                        FamilyId,
                        UserId,
                        "Adult 1",
                        new ChatMessageRequest
                        {
                                ConversationId = "conv_cancel",
                                Message = "Delete this recipe"
                        });

                var response = await service.SendMessageAsync(
                        FamilyId,
                        UserId,
                        "Adult 1",
                        new ChatMessageRequest
                        {
                                ConversationId = "conv_cancel",
                                Message = "Cancel"
                        });

                response.AssistantMessage.Content.Should().Be("Canceled. I did not apply that action.");

                var history = await chatRepo.QueryByPartitionKeyAsync($"USER#{UserId}");
                history.Last(x => x.Role == ChatConstants.AssistantRole).Actions.Should().ContainSingle();
                history.Last(x => x.Role == ChatConstants.AssistantRole).Actions[0].Status.Should().Be("canceled");
        }

        [Fact]
        public async Task SendMessageAsync_WhenConfirmingClearCompletedRequest_RemovesCompletedItems()
        {
                var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
                var groceryService = new StatefulGroceryListService
                {
                        CurrentList = new GroceryListDocument
                        {
                                FamilyId = FamilyId,
                                ListId = "LIST#ACTIVE",
                                Version = 3,
                                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                                UpdatedAt = DateTimeOffset.UtcNow,
                                Items =
                                [
                                        new GroceryItemDocument
                                        {
                                                Id = "done_1",
                                                Name = "Milk",
                                                Section = "dairy",
                                                Quantity = 1,
                                                Unit = "carton",
                                                MealAssociations = [],
                                                CheckedOff = true,
                                                InStock = false
                                        },
                                        new GroceryItemDocument
                                        {
                                                Id = "todo_1",
                                                Name = "Bread",
                                                Section = "pantry",
                                                Quantity = 1,
                                                Unit = "loaf",
                                                MealAssociations = [],
                                                CheckedOff = false,
                                                InStock = false
                                        }
                                ],
                                Progress = new GroceryProgressDocument { Total = 2, Completed = 1, Percentage = 50 }
                        }
                };

                var service = CreateService(chatRepo, apiKey: null, httpResponse: null, groceryListService: groceryService);

                var prompt = await service.SendMessageAsync(
                        FamilyId,
                        UserId,
                        "Adult 1",
                        new ChatMessageRequest
                        {
                                ConversationId = "conv_clear_completed",
                                Message = "Please clear completed items from the grocery list"
                        });

                prompt.AssistantMessage.RequiresConfirmation.Should().BeTrue();
                prompt.AssistantMessage.PendingActionType.Should().Be("clear_completed_grocery");

                var response = await service.SendMessageAsync(
                        FamilyId,
                        UserId,
                        "Adult 1",
                        new ChatMessageRequest
                        {
                                ConversationId = "conv_clear_completed",
                                Message = "Confirm"
                        });

                response.AssistantMessage.Content.Should().Be("Cleared 1 completed grocery item(s).");
                groceryService.CurrentList!.Items.Should().ContainSingle(i => i.Id == "todo_1");
        }

        [Fact]
        public async Task SendMessageAsync_WhenToolCallAddsPantryItems_UsesPantryAction()
        {
                var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
                var groceryService = new StatefulGroceryListService();
                var openAiBody = """
                        {
                            "choices": [
                                {
                                    "message": {
                                        "tool_calls": [
                                            {
                                                "function": {
                                                    "name": "manage_pantry",
                                                    "arguments": "{\"action\":\"add_items\",\"items\":[{\"name\":\"Rice\",\"section\":\"pantry\"},{\"name\":\"Beans\"}]}"
                                                }
                                            }
                                        ]
                                    }
                                }
                            ]
                        }
                        """;

                var service = CreateService(
                        chatRepo,
                        "sk-test",
                        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(openAiBody, Encoding.UTF8, "application/json") },
                        groceryListService: groceryService);

                var response = await service.SendMessageAsync(
                        FamilyId,
                        UserId,
                        "Adult 1",
                        new ChatMessageRequest { Message = "Add pantry staples for dinner prep" });

                response.AssistantMessage.Content.Should().Contain("Added 2 item(s) to pantry staples.");
                groceryService.Pantry.Items.Should().Contain(i => i.Name == "Rice");
                groceryService.Pantry.Items.Should().Contain(i => i.Name == "Beans");
        }

        [Fact]
        public async Task SendMessageAsync_WhenToolCallUpdatesProfile_PersistsProfileChanges()
        {
                var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
                var profileRepository = new InMemoryRepository<UserProfileDocument>();
                var openAiBody = """
                        {
                            "choices": [
                                {
                                    "message": {
                                        "tool_calls": [
                                            {
                                                "function": {
                                                    "name": "update_profile",
                                                    "arguments": "{\"updates\":{\"name\":\"Alex\",\"dietaryPrefs\":[\"vegetarian\"],\"excludedIngredients\":[\"shrimp\"]}}"
                                                }
                                            }
                                        ]
                                    }
                                }
                            ]
                        }
                        """;

                var service = CreateService(
                        chatRepo,
                        "sk-test",
                        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(openAiBody, Encoding.UTF8, "application/json") },
                        profileRepository: profileRepository);

                var response = await service.SendMessageAsync(
                        FamilyId,
                        UserId,
                        "Adult 1",
                        new ChatMessageRequest { Message = "Update my meal profile for recipes" });

                response.AssistantMessage.Content.Should().Contain("Updated profile for **Alex**.");

                var stored = await profileRepository.GetAsync(new DynamoDbKey($"USER#{UserId}", "PROFILE"));
                stored.Should().NotBeNull();
                stored!.DietaryPrefs.Should().ContainSingle("vegetarian");
                stored.ExcludedIngredients.Should().ContainSingle("shrimp");
        }

        [Fact]
        public async Task SendMessageAsync_WhenToolCallGetsNutritionSummary_ReturnsTotals()
        {
                var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
                var recipeService = new StubRecipeService
                {
                        Recipes =
                        [
                                new RecipeDocument
                                {
                                        RecipeId = "rec_1",
                                        FamilyId = FamilyId,
                                        Name = "Salmon Bowl",
                                        Category = "dinner",
                                        Ingredients = [new RecipeIngredientModel { Name = "salmon" }],
                                        Instructions = ["Cook"],
                                        Nutrition = new RecipeNutritionModel { Calories = 500, Protein = 30, Carbohydrates = 20, Fat = 25 },
                                        CreatedByUserId = UserId,
                                        CreatedAt = DateTimeOffset.UtcNow,
                                        UpdatedAt = DateTimeOffset.UtcNow
                                },
                                new RecipeDocument
                                {
                                        RecipeId = "rec_2",
                                        FamilyId = FamilyId,
                                        Name = "Rice Bowl",
                                        Category = "dinner",
                                        Ingredients = [new RecipeIngredientModel { Name = "rice" }],
                                        Instructions = ["Cook"],
                                        Nutrition = new RecipeNutritionModel { Calories = 300, Protein = 5, Carbohydrates = 50, Fat = 3 },
                                        CreatedByUserId = UserId,
                                        CreatedAt = DateTimeOffset.UtcNow,
                                        UpdatedAt = DateTimeOffset.UtcNow
                                }
                        ]
                };

                var openAiBody = """
                        {
                            "choices": [
                                {
                                    "message": {
                                        "tool_calls": [
                                            {
                                                "function": {
                                                    "name": "get_nutritional_info",
                                                    "arguments": "{\"recipeIds\":[\"rec_1\",\"rec_2\"]}"
                                                }
                                            }
                                        ]
                                    }
                                }
                            ]
                        }
                        """;

                var service = CreateService(
                        chatRepo,
                        "sk-test",
                        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(openAiBody, Encoding.UTF8, "application/json") },
                        recipeService: recipeService);

                var response = await service.SendMessageAsync(
                        FamilyId,
                        UserId,
                        "Adult 1",
                        new ChatMessageRequest { Message = "Show nutrition for dinner recipes" });

                response.AssistantMessage.Content.Should().Contain("800 kcal, 35g protein, 70g carbs, 28g fat");
        }

        [Fact]
        public async Task SendMessageAsync_WhenToolCallModifiesMealPlan_UpdatesSlotAndRefreshesGroceryList()
        {
                var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
                var mealPlanService = new StatefulMealPlanService
                {
                        CurrentPlan = new MealPlanDocument
                        {
                                FamilyId = FamilyId,
                                WeekStartDate = "2026-03-30",
                                Status = "active",
                                Meals =
                                [
                                        new MealSlotDocument { Day = "Monday", MealType = "dinner", RecipeId = "rec_old", RecipeName = "Old Dinner", Servings = 4 }
                                ],
                                GeneratedBy = "ai",
                                ConstraintsUsed = "v1",
                                CreatedAt = DateTimeOffset.UtcNow,
                                UpdatedAt = DateTimeOffset.UtcNow
                        }
                };
                var groceryService = new StatefulGroceryListService();
                var recipeService = new StubRecipeService
                {
                        ById = new Dictionary<string, RecipeDocument>(StringComparer.OrdinalIgnoreCase)
                        {
                                ["rec_new"] = new RecipeDocument
                                {
                                        RecipeId = "rec_new",
                                        FamilyId = FamilyId,
                                        Name = "New Dinner",
                                        Category = "dinner",
                                        Ingredients = [new RecipeIngredientModel { Name = "chicken" }],
                                        Instructions = ["Cook"],
                                        CreatedByUserId = UserId,
                                        CreatedAt = DateTimeOffset.UtcNow,
                                        UpdatedAt = DateTimeOffset.UtcNow
                                }
                        }
                };
                var openAiBody = """
                        {
                            "choices": [
                                {
                                    "message": {
                                        "tool_calls": [
                                            {
                                                "function": {
                                                    "name": "modify_meal_plan",
                                                    "arguments": "{\"day\":\"Monday\",\"mealType\":\"dinner\",\"newRecipeId\":\"rec_new\"}"
                                                }
                                            }
                                        ]
                                    }
                                }
                            ]
                        }
                        """;

                var service = CreateService(
                        chatRepo,
                        "sk-test",
                        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(openAiBody, Encoding.UTF8, "application/json") },
                        mealPlanService: mealPlanService,
                        recipeService: recipeService,
                        groceryListService: groceryService);

                var response = await service.SendMessageAsync(
                        FamilyId,
                        UserId,
                        "Adult 1",
                        new ChatMessageRequest { Message = "Swap Monday dinner in my meal plan" });

                response.AssistantMessage.Content.Should().Contain("Updated Monday dinner to **New Dinner**");
                mealPlanService.LastUpdateRequest.Should().NotBeNull();
                groceryService.GenerateCalls.Should().Be(1);
        }

        [Fact]
        public async Task SendMessageAsync_WhenNoApiKeyAndMealPlanIntent_UsesDeterministicGeneration()
        {
                var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
                var mealPlanService = new StatefulMealPlanService
                {
                        CurrentPlan = new MealPlanDocument
                        {
                                FamilyId = FamilyId,
                                WeekStartDate = "2026-03-30",
                                Status = "active",
                                Meals =
                                [
                                        new MealSlotDocument { Day = "Monday", MealType = "breakfast", RecipeId = "rec_1", RecipeName = "Oats" },
                                        new MealSlotDocument { Day = "Monday", MealType = "dinner", RecipeId = "rec_2", RecipeName = "Pasta" }
                                ],
                                GeneratedBy = "ai",
                                ConstraintsUsed = "v1",
                                CreatedAt = DateTimeOffset.UtcNow,
                                UpdatedAt = DateTimeOffset.UtcNow
                        }
                };
                var groceryService = new StatefulGroceryListService();

                var service = CreateService(
                        chatRepo,
                        apiKey: null,
                        httpResponse: null,
                        mealPlanService: mealPlanService,
                        groceryListService: groceryService);

                var response = await service.SendMessageAsync(
                        FamilyId,
                        UserId,
                        "Adult 1",
                        new ChatMessageRequest { Message = "Generate a meal plan for 2026-03-30" });

                response.AssistantMessage.Content.Should().Contain("Generated a meal plan for **2026-03-30** with 2 meals");
                groceryService.GenerateCalls.Should().Be(1);
        }

        [Fact]
        public async Task SendMessageAsync_WhenToolCallCreatesRecipe_CreatesRecipeFromArguments()
        {
                var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
                var recipeService = new StubRecipeService();
                var openAiBody = """
                        {
                            "choices": [
                                {
                                    "message": {
                                        "tool_calls": [
                                            {
                                                "function": {
                                                    "name": "create_recipe",
                                                    "arguments": "{\"name\":\"Veggie Tacos\",\"category\":\"dinner\",\"ingredients\":[{\"name\":\"Tortillas\",\"quantity\":\"8\"}],\"instructions\":[\"Warm tortillas\",\"Serve\"],\"tags\":[\"quick\"]}"
                                                }
                                            }
                                        ]
                                    }
                                }
                            ]
                        }
                        """;

                var service = CreateService(
                        chatRepo,
                        "sk-test",
                        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(openAiBody, Encoding.UTF8, "application/json") },
                        recipeService: recipeService);

                var response = await service.SendMessageAsync(FamilyId, UserId, "Adult 1", new ChatMessageRequest { Message = "Create a taco recipe" });

                response.AssistantMessage.Content.Should().Contain("Created recipe **Veggie Tacos**");
                recipeService.LastCreateRequest.Should().NotBeNull();
                recipeService.LastCreateRequest!.Ingredients.Should().ContainSingle(i => i.Name == "Tortillas");
        }

        [Fact]
        public async Task SendMessageAsync_WhenToolCallListsGroceryItems_ReturnsCurrentList()
        {
                var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
                var groceryService = new StatefulGroceryListService
                {
                        CurrentList = new GroceryListDocument
                        {
                                FamilyId = FamilyId,
                                ListId = "LIST#ACTIVE",
                                Version = 1,
                                CreatedAt = DateTimeOffset.UtcNow,
                                UpdatedAt = DateTimeOffset.UtcNow,
                                Items =
                                [
                                        new GroceryItemDocument { Id = "item_1", Name = "Milk", Section = "dairy", MealAssociations = [] },
                                        new GroceryItemDocument { Id = "item_2", Name = "Apples", Section = "produce", MealAssociations = [] }
                                ],
                                Progress = new GroceryProgressDocument { Total = 2, Completed = 0, Percentage = 0 }
                        }
                };
                var openAiBody = """
                        {
                            "choices": [
                                {
                                    "message": {
                                        "tool_calls": [
                                            {
                                                "function": {
                                                    "name": "manage_grocery_list",
                                                    "arguments": "{\"action\":\"list\"}"
                                                }
                                            }
                                        ]
                                    }
                                }
                            ]
                        }
                        """;

                var service = CreateService(
                        chatRepo,
                        "sk-test",
                        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(openAiBody, Encoding.UTF8, "application/json") },
                        groceryListService: groceryService);

                var response = await service.SendMessageAsync(FamilyId, UserId, "Adult 1", new ChatMessageRequest { Message = "Show my grocery list" });

                response.AssistantMessage.Content.Should().Contain("Current grocery list:");
                response.AssistantMessage.Content.Should().Contain("Milk (dairy)");
                response.AssistantMessage.Content.Should().Contain("Apples (produce)");
        }

        [Fact]
        public async Task SendMessageAsync_WhenToolCallSearchesRecipes_ReturnsMatchingSafeRecipes()
        {
                var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
                var recipeService = new StubRecipeService
                {
                        Recipes =
                        [
                                new RecipeDocument
                                {
                                        RecipeId = "rec_1",
                                        FamilyId = FamilyId,
                                        Name = "Quick Pasta",
                                        Category = "dinner",
                                        Tags = ["quick", "family"],
                                        PrepTimeMinutes = 10,
                                        CookTimeMinutes = 10,
                                        Ingredients = [new RecipeIngredientModel { Name = "pasta" }],
                                        Instructions = ["Cook"]
                                },
                                new RecipeDocument
                                {
                                        RecipeId = "rec_2",
                                        FamilyId = FamilyId,
                                        Name = "Slow Chili",
                                        Category = "dinner",
                                        Tags = ["slow"],
                                        PrepTimeMinutes = 30,
                                        CookTimeMinutes = 45,
                                        Ingredients = [new RecipeIngredientModel { Name = "beans" }],
                                        Instructions = ["Cook"]
                                }
                        ]
                };
                var openAiBody = """
                        {
                            "choices": [
                                {
                                    "message": {
                                        "tool_calls": [
                                            {
                                                "function": {
                                                    "name": "search_recipes",
                                                    "arguments": "{\"query\":\"Pasta\",\"category\":\"dinner\",\"maxPrepTime\":25,\"tags\":[\"quick\"]}"
                                                }
                                            }
                                        ]
                                    }
                                }
                            ]
                        }
                        """;

                var service = CreateService(
                        chatRepo,
                        "sk-test",
                        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(openAiBody, Encoding.UTF8, "application/json") },
                        recipeService: recipeService);

                var response = await service.SendMessageAsync(FamilyId, UserId, "Adult 1", new ChatMessageRequest { Message = "Search dinner recipes" });

                response.AssistantMessage.Content.Should().Contain("Here are matching recipes:");
                response.AssistantMessage.Content.Should().Contain("Quick Pasta");
                response.AssistantMessage.Content.Should().NotContain("Slow Chili");
        }

        [Fact]
        public async Task SendMessageAsync_WhenToolCallModifiesMealPlanWithoutActivePlan_ReturnsFailureMessage()
        {
                var chatRepo = new InMemoryRepository<ChatHistoryMessageDocument>();
                var openAiBody = """
                        {
                            "choices": [
                                {
                                    "message": {
                                        "tool_calls": [
                                            {
                                                "function": {
                                                    "name": "modify_meal_plan",
                                                    "arguments": "{\"day\":\"Monday\",\"mealType\":\"dinner\"}"
                                                }
                                            }
                                        ]
                                    }
                                }
                            ]
                        }
                        """;

                var service = CreateService(
                        chatRepo,
                        "sk-test",
                        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(openAiBody, Encoding.UTF8, "application/json") });

                var response = await service.SendMessageAsync(FamilyId, UserId, "Adult 1", new ChatMessageRequest { Message = "Swap a dinner" });

                response.AssistantMessage.Content.Should().Be("There is no active meal plan to modify.");
        }

    private static ChatService CreateService(
        InMemoryRepository<ChatHistoryMessageDocument> chatRepository,
        string? apiKey,
                HttpResponseMessage? httpResponse,
                IMealPlanService? mealPlanService = null,
                IRecipeService? recipeService = null,
                IGroceryListService? groceryListService = null,
                InMemoryRepository<UserProfileDocument>? profileRepository = null,
                IDependentProfileService? dependentProfileService = null)
    {
                profileRepository ??= new InMemoryRepository<UserProfileDocument>();
        var apiKeyProvider = new StubApiKeyProvider(apiKey);
        var handler = new StubHttpMessageHandler(httpResponse);

        return new ChatService(
            chatRepository,
            apiKeyProvider,
            Options.Create(new OpenAiOptions()),
                        mealPlanService ?? new NoOpMealPlanService(),
                        recipeService ?? new NoOpRecipeService(),
                        groceryListService ?? new NoOpGroceryListService(),
            profileRepository,
                        dependentProfileService ?? new NoOpDependentProfileService(),
            new HttpClient(handler),
            NullLogger<ChatService>.Instance);
    }

    private sealed class StubApiKeyProvider : IOpenAiApiKeyProvider
    {
        private readonly string? _apiKey;

        public StubApiKeyProvider(string? apiKey)
        {
            _apiKey = apiKey;
        }

        public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apiKey);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;

        public StubHttpMessageHandler(HttpResponseMessage? response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_response is null)
            {
                throw new InvalidOperationException("HTTP response was not configured for this test.");
            }

            request.Headers.Authorization.Should().NotBeNull();
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");

            return Task.FromResult(_response);
        }
    }

    private sealed class InMemoryRepository<TDocument> : IDynamoDbRepository<TDocument>
        where TDocument : class
    {
        private readonly Dictionary<string, TDocument> _store = new(StringComparer.Ordinal);

        public Task<TDocument?> GetAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(ToCompositeKey(key), out var value);
            return Task.FromResult(value);
        }

        public Task PutAsync(DynamoDbKey key, TDocument document, CancellationToken cancellationToken = default)
        {
            _store[ToCompositeKey(key)] = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(DynamoDbKey key, CancellationToken cancellationToken = default)
        {
            _store.Remove(ToCompositeKey(key));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TDocument>> QueryByPartitionKeyAsync(
            string partitionKey,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            var results = _store
                .Where(x => x.Key.StartsWith($"{partitionKey}|", StringComparison.Ordinal))
                .Select(x => x.Value)
                .ToList();

            if (limit.HasValue)
            {
                results = results.Take(limit.Value).ToList();
            }

            return Task.FromResult<IReadOnlyList<TDocument>>(results);
        }

        public Task<IReadOnlyList<TDocument>> QueryByIndexPartitionKeyAsync(
            string indexName,
            string partitionKeyName,
            string partitionKeyValue,
            IReadOnlyDictionary<string, string>? equalsFilters = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TDocument>>([]);
        }

        private static string ToCompositeKey(DynamoDbKey key) => $"{key.PartitionKey}|{key.SortKey}";
    }

    private sealed class NoOpMealPlanService : IMealPlanService
    {
        public Task<MealPlanDocument?> GetCurrentAsync(string familyId, CancellationToken cancellationToken = default) => Task.FromResult<MealPlanDocument?>(null);

        public Task<MealPlanDocument?> GetByWeekAsync(string familyId, string weekStartDate, CancellationToken cancellationToken = default) => Task.FromResult<MealPlanDocument?>(null);

        public Task<IReadOnlyList<MealPlanDocument>> GetHistoryAsync(string familyId, int limit = 10, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MealPlanDocument>>([]);

        public Task<MealPlanDocument> CreateAsync(string familyId, string userId, CreateMealPlanRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new MealPlanDocument());

        public Task<MealPlanDocument> GenerateAsync(string familyId, string userId, GenerateMealPlanRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new MealPlanDocument());

        public Task<IReadOnlyList<MealSwapSuggestion>> SuggestSwapOptionsAsync(string familyId, string weekStartDate, string day, string mealType, int limit = 5, CancellationToken cancellationToken = default, string? profileContext = null) => Task.FromResult<IReadOnlyList<MealSwapSuggestion>>([]);

        public Task<MealPlanDocument?> UpdateAsync(string familyId, string weekStartDate, UpdateMealPlanRequest request, CancellationToken cancellationToken = default) => Task.FromResult<MealPlanDocument?>(null);

        public Task<bool> DeleteAsync(string familyId, string weekStartDate, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class StatefulMealPlanService : IMealPlanService
    {
        public MealPlanDocument? CurrentPlan { get; set; }

        public UpdateMealPlanRequest? LastUpdateRequest { get; private set; }

        public IReadOnlyList<MealSwapSuggestion> Suggestions { get; set; } = [];

        public Task<MealPlanDocument?> GetCurrentAsync(string familyId, CancellationToken cancellationToken = default) => Task.FromResult(CurrentPlan);

        public Task<MealPlanDocument?> GetByWeekAsync(string familyId, string weekStartDate, CancellationToken cancellationToken = default) => Task.FromResult(CurrentPlan);

        public Task<IReadOnlyList<MealPlanDocument>> GetHistoryAsync(string familyId, int limit = 10, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MealPlanDocument>>(CurrentPlan is null ? [] : [CurrentPlan]);

        public Task<MealPlanDocument> CreateAsync(string familyId, string userId, CreateMealPlanRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentPlan ?? new MealPlanDocument());

        public Task<MealPlanDocument> GenerateAsync(string familyId, string userId, GenerateMealPlanRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentPlan ?? new MealPlanDocument { WeekStartDate = request.WeekStartDate, FamilyId = familyId, Meals = [] });

        public Task<IReadOnlyList<MealSwapSuggestion>> SuggestSwapOptionsAsync(string familyId, string weekStartDate, string day, string mealType, int limit = 5, CancellationToken cancellationToken = default, string? profileContext = null)
            => Task.FromResult(Suggestions);

        public Task<MealPlanDocument?> UpdateAsync(string familyId, string weekStartDate, UpdateMealPlanRequest request, CancellationToken cancellationToken = default)
        {
            LastUpdateRequest = request;
            if (CurrentPlan is null)
            {
                return Task.FromResult<MealPlanDocument?>(null);
            }

            var updatedMeals = request.Meals?.Select(m => new MealSlotDocument
            {
                Day = m.Day,
                MealType = m.MealType,
                RecipeId = m.RecipeId,
                RecipeName = m.RecipeId,
                Servings = m.Servings
            }).ToList() ?? CurrentPlan.Meals;

            CurrentPlan = new MealPlanDocument
            {
                FamilyId = CurrentPlan.FamilyId,
                WeekStartDate = CurrentPlan.WeekStartDate,
                Status = CurrentPlan.Status,
                Meals = updatedMeals,
                NutritionalSummary = CurrentPlan.NutritionalSummary,
                ConstraintsUsed = CurrentPlan.ConstraintsUsed,
                GeneratedBy = CurrentPlan.GeneratedBy,
                QualityScore = CurrentPlan.QualityScore,
                CreatedAt = CurrentPlan.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow,
                TTL = CurrentPlan.TTL
            };
            return Task.FromResult<MealPlanDocument?>(CurrentPlan);
        }

        public Task<bool> DeleteAsync(string familyId, string weekStartDate, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private class StubRecipeService : IRecipeService
    {
        public IReadOnlyList<RecipeDocument> Recipes { get; set; } = [];

        public Dictionary<string, RecipeDocument> ById { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public CreateRecipeRequest? LastCreateRequest { get; private set; }

        public virtual Task<IReadOnlyList<RecipeDocument>> ListByFamilyAsync(string familyId, CancellationToken cancellationToken = default) => Task.FromResult(Recipes);

        public virtual Task<RecipeDocument?> GetByIdAsync(string familyId, string recipeId, CancellationToken cancellationToken = default)
            => Task.FromResult(ById.TryGetValue(recipeId, out var recipe) ? recipe : null);

        public virtual Task<RecipeDocument> CreateAsync(string familyId, string userId, CreateRecipeRequest request, CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;
            return Task.FromResult(new RecipeDocument
            {
                RecipeId = "rec_created",
                FamilyId = familyId,
                Name = request.Name,
                Category = request.Category,
                Ingredients = request.Ingredients ?? [],
                Instructions = request.Instructions ?? [],
                CreatedByUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        public Task<RecipeDocument?> UpdateAsync(string familyId, string recipeId, UpdateRecipeRequest request, CancellationToken cancellationToken = default) => Task.FromResult<RecipeDocument?>(null);

        public Task<bool> DeleteAsync(string familyId, string recipeId, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<FavoriteRecipeDocument?> AddFavoriteAsync(string familyId, string userId, string recipeId, FavoriteRecipeRequest request, CancellationToken cancellationToken = default) => Task.FromResult<FavoriteRecipeDocument?>(null);

        public Task RemoveFavoriteAsync(string userId, string recipeId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<FavoriteRecipeDocument>> ListFavoritesAsync(string userId, string? category, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FavoriteRecipeDocument>>([]);
    }

    private sealed class NoOpRecipeService : StubRecipeService
    {
    }

    private class StatefulGroceryListService : IGroceryListService
    {
        public GroceryListDocument? CurrentList { get; set; }

        public PantryStaplesDocument Pantry { get; private set; } = new();

        public int GenerateCalls { get; private set; }

        public virtual Task<GroceryListDocument?> GetCurrentAsync(string familyId, CancellationToken cancellationToken = default) => Task.FromResult(CurrentList);

        public virtual Task<GroceryListDocument> GenerateAsync(string familyId, string userId, string? userName, GenerateGroceryListRequest request, CancellationToken cancellationToken = default)
        {
            GenerateCalls += 1;
            CurrentList ??= new GroceryListDocument
            {
                FamilyId = familyId,
                ListId = "LIST#ACTIVE",
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Items = [],
                Progress = new GroceryProgressDocument()
            };

            return Task.FromResult(CurrentList);
        }

        public virtual Task<GroceryItemMutationResult> ToggleItemAsync(string familyId, string itemId, string userId, string? userName, ToggleGroceryItemRequest request, CancellationToken cancellationToken = default) => Task.FromResult(GroceryItemMutationResult.NotFoundItem);

        public virtual Task<GroceryItemMutationResult> AddItemAsync(string familyId, AddGroceryItemRequest request, CancellationToken cancellationToken = default)
        {
            if (CurrentList is null)
            {
                return Task.FromResult(GroceryItemMutationResult.NotFoundList);
            }

            var item = new GroceryItemDocument
            {
                Id = $"item_{CurrentList.Version + 1}",
                Name = request.Name,
                Section = request.Section,
                Quantity = request.Quantity ?? 1,
                Unit = request.Unit,
                MealAssociations = [],
                CheckedOff = false,
                InStock = false
            };

            CurrentList = CurrentList with
            {
                Items = [.. CurrentList.Items, item],
                Version = CurrentList.Version + 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                Progress = new GroceryProgressDocument { Total = CurrentList.Items.Count + 1, Completed = CurrentList.Items.Count(i => i.CheckedOff), Percentage = 0 }
            };

            return Task.FromResult(GroceryItemMutationResult.Success(item, CurrentList));
        }

        public virtual Task<GroceryItemMutationResult> SetInStockAsync(string familyId, string itemId, SetInStockRequest request, CancellationToken cancellationToken = default) => Task.FromResult(GroceryItemMutationResult.NotFoundItem);

        public virtual Task<GroceryItemMutationResult> RemoveItemAsync(string familyId, string itemId, RemoveGroceryItemRequest request, CancellationToken cancellationToken = default)
        {
            if (CurrentList is null)
            {
                return Task.FromResult(GroceryItemMutationResult.NotFoundList);
            }

            var item = CurrentList.Items.FirstOrDefault(i => i.Id == itemId);
            if (item is null)
            {
                return Task.FromResult(GroceryItemMutationResult.NotFoundItem);
            }

            var items = CurrentList.Items.Where(i => i.Id != itemId).ToList();
            CurrentList = CurrentList with
            {
                Items = items,
                Version = CurrentList.Version + 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                Progress = new GroceryProgressDocument { Total = items.Count, Completed = items.Count(i => i.CheckedOff), Percentage = items.Count == 0 ? 0 : 100 * items.Count(i => i.CheckedOff) / items.Count }
            };

            return Task.FromResult(GroceryItemMutationResult.Success(item, CurrentList));
        }

        public virtual Task<GroceryListPollResult> PollAsync(string familyId, DateTimeOffset? since, CancellationToken cancellationToken = default) => Task.FromResult(GroceryListPollResult.NotFound);

        public virtual Task<PantryStaplesDocument> GetPantryStaplesAsync(string familyId, CancellationToken cancellationToken = default)
        {
            Pantry = Pantry with { FamilyId = familyId };
            return Task.FromResult(Pantry);
        }

        public virtual Task<PantryStaplesDocument> ReplacePantryStaplesAsync(string familyId, ReplacePantryStaplesRequest request, CancellationToken cancellationToken = default)
        {
            Pantry = new PantryStaplesDocument { FamilyId = familyId, Items = request.Items, PreferredSectionOrder = request.PreferredSectionOrder ?? [], UpdatedAt = DateTimeOffset.UtcNow };
            return Task.FromResult(Pantry);
        }

        public virtual Task<PantryStaplesDocument> AddPantryStapleAsync(string familyId, AddPantryStapleItemRequest request, CancellationToken cancellationToken = default)
        {
            Pantry = Pantry with
            {
                FamilyId = familyId,
                Items = [.. Pantry.Items, new PantryStapleItemDocument { Name = request.Name, Section = request.Section }],
                UpdatedAt = DateTimeOffset.UtcNow
            };
            return Task.FromResult(Pantry);
        }

        public virtual Task<bool> DeletePantryStapleAsync(string familyId, string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class NoOpGroceryListService : StatefulGroceryListService
    {
        public override Task<GroceryListDocument?> GetCurrentAsync(string familyId, CancellationToken cancellationToken = default) => Task.FromResult<GroceryListDocument?>(null);

        public override Task<GroceryListDocument> GenerateAsync(string familyId, string userId, string? userName, GenerateGroceryListRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new GroceryListDocument());

        public override Task<GroceryItemMutationResult> AddItemAsync(string familyId, AddGroceryItemRequest request, CancellationToken cancellationToken = default) => Task.FromResult(GroceryItemMutationResult.NotFoundList);

        public override Task<GroceryItemMutationResult> RemoveItemAsync(string familyId, string itemId, RemoveGroceryItemRequest request, CancellationToken cancellationToken = default) => Task.FromResult(GroceryItemMutationResult.NotFoundItem);

        public override Task<PantryStaplesDocument> GetPantryStaplesAsync(string familyId, CancellationToken cancellationToken = default) => Task.FromResult(new PantryStaplesDocument { FamilyId = familyId });

        public override Task<PantryStaplesDocument> AddPantryStapleAsync(string familyId, AddPantryStapleItemRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new PantryStaplesDocument { FamilyId = familyId });
    }

    private sealed class NoOpDependentProfileService : IDependentProfileService
    {
        public Task<IReadOnlyList<DependentProfileDocument>> ListByFamilyAsync(string familyId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DependentProfileDocument>>([]);

        public Task<DependentProfileDocument> CreateAsync(string familyId, CreateDependentRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new DependentProfileDocument());

        public Task<DependentProfileDocument?> UpdateAsync(string familyId, string userId, UpdateDependentRequest request, CancellationToken cancellationToken = default) => Task.FromResult<DependentProfileDocument?>(null);

        public Task<bool> DeleteAsync(string familyId, string userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
