using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using System.Text.Json;

var arguments = MigrationArguments.Parse(args);

if (arguments.ShowHelp)
{
    MigrationArguments.PrintUsage();
    return;
}

try
{
    if (arguments.Entity == MigrationEntity.Users)
    {
        await RunUsersMigrationAsync(arguments);
    }
    else
    {
        await RunRecipesMigrationAsync(arguments);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static async Task RunUsersMigrationAsync(MigrationArguments arguments)
{
    if (!File.Exists(arguments.UsersFilePath))
    {
        throw new InvalidOperationException($"Users seed file not found: {arguments.UsersFilePath}");
    }

    var profiles = await LoadProfilesAsync(arguments.UsersFilePath);
    var validationErrors = ValidateProfiles(profiles);

    if (validationErrors.Count > 0)
    {
        throw new InvalidOperationException($"Profile seed validation failed:{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", validationErrors)}");
    }

    Console.WriteLine($"Loaded {profiles.Count} profile records from {arguments.UsersFilePath}.");
    Console.WriteLine($"Target users table: {arguments.UsersTableName}");
    Console.WriteLine(arguments.DryRun ? "Mode: DRY RUN (no writes will be performed)." : "Mode: EXECUTE (writes enabled).");

    if (arguments.DryRun)
    {
        foreach (var profile in profiles)
        {
            Console.WriteLine($"DRY RUN put: PK={profile.Pk} SK={profile.Sk} role={profile.Role} name={profile.Name}");
        }

        return;
    }

    using var dynamoDb = CreateDynamoDbClient(arguments.Region);

    foreach (var profile in profiles)
    {
        var item = BuildItem(profile.Raw, profile.Pk, profile.Sk);

        await dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = arguments.UsersTableName,
            Item = item
        });

        Console.WriteLine($"Wrote profile: PK={profile.Pk} SK={profile.Sk} role={profile.Role} name={profile.Name}");
    }

    Console.WriteLine("Profile migration completed successfully.");
}

static async Task RunRecipesMigrationAsync(MigrationArguments arguments)
{
    if (!File.Exists(arguments.RecipesFilePath))
    {
        throw new InvalidOperationException($"Recipes seed file not found: {arguments.RecipesFilePath}");
    }

    var recipes = await LoadRecipesAsync(arguments.RecipesFilePath);
    var validationErrors = ValidateRecipes(recipes, arguments.ExpectedRecipes);

    if (validationErrors.Count > 0)
    {
        throw new InvalidOperationException($"Recipe seed validation failed:{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", validationErrors)}");
    }

    Console.WriteLine($"Loaded {recipes.Count} recipe records from {arguments.RecipesFilePath}.");
    Console.WriteLine($"Target recipes table: {arguments.RecipesTableName}");
    Console.WriteLine(arguments.DryRun ? "Mode: DRY RUN (no writes will be performed)." : "Mode: EXECUTE (writes enabled).");

    if (arguments.DryRun)
    {
        foreach (var recipe in recipes)
        {
            Console.WriteLine($"DRY RUN put: PK={recipe.Pk} SK={recipe.Sk} recipeId={recipe.RecipeId} name={recipe.Name}");
        }

        return;
    }

    using var dynamoDb = CreateDynamoDbClient(arguments.Region);

    foreach (var recipe in recipes)
    {
        var item = BuildItem(recipe.Raw, recipe.Pk, recipe.Sk);

        await dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = arguments.RecipesTableName,
            Item = item
        });

        Console.WriteLine($"Wrote recipe: PK={recipe.Pk} SK={recipe.Sk} recipeId={recipe.RecipeId} name={recipe.Name}");
    }

    Console.WriteLine("Recipe migration completed successfully.");
}

static AmazonDynamoDBClient CreateDynamoDbClient(string region)
{
    var config = new AmazonDynamoDBConfig
    {
        RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
    };

    return new AmazonDynamoDBClient(config);
}

static async Task<List<SeedProfileRecord>> LoadProfilesAsync(string filePath)
{
    await using var stream = File.OpenRead(filePath);
    using var document = await JsonDocument.ParseAsync(stream);

    if (document.RootElement.ValueKind is not JsonValueKind.Array)
    {
        throw new InvalidOperationException("Users seed file must contain a top-level JSON array.");
    }

    var profiles = new List<SeedProfileRecord>();

    foreach (var element in document.RootElement.EnumerateArray())
    {
        profiles.Add(SeedProfileRecord.FromJson(element));
    }

    return profiles;
}

static async Task<List<SeedRecipeRecord>> LoadRecipesAsync(string filePath)
{
    await using var stream = File.OpenRead(filePath);
    using var document = await JsonDocument.ParseAsync(stream);

    if (document.RootElement.ValueKind is not JsonValueKind.Array)
    {
        throw new InvalidOperationException("Recipes seed file must contain a top-level JSON array.");
    }

    var recipes = new List<SeedRecipeRecord>();

    foreach (var element in document.RootElement.EnumerateArray())
    {
        recipes.Add(SeedRecipeRecord.FromJson(element));
    }

    return recipes;
}

static List<string> ValidateProfiles(IReadOnlyList<SeedProfileRecord> profiles)
{
    var errors = new List<string>();

    if (profiles.Count != 4)
    {
        errors.Add($"Expected exactly 4 profile records (2 adults + 2 dependents) but found {profiles.Count}.");
    }

    var adults = profiles.Where(profile => !string.Equals(profile.Role, "dependent", StringComparison.OrdinalIgnoreCase)).ToList();
    var dependents = profiles.Where(profile => string.Equals(profile.Role, "dependent", StringComparison.OrdinalIgnoreCase)).ToList();

    if (adults.Count != 2)
    {
        errors.Add($"Expected 2 adult profiles but found {adults.Count}.");
    }

    if (dependents.Count != 2)
    {
        errors.Add($"Expected 2 dependent profiles but found {dependents.Count}.");
    }

    foreach (var profile in profiles)
    {
        if (string.IsNullOrWhiteSpace(profile.Pk))
        {
            errors.Add($"Profile '{profile.Name}' is missing PK.");
        }

        if (string.IsNullOrWhiteSpace(profile.Sk))
        {
            errors.Add($"Profile '{profile.Name}' is missing SK.");
        }

        if (string.IsNullOrWhiteSpace(profile.FamilyId))
        {
            errors.Add($"Profile '{profile.Name}' is missing familyId.");
        }

        if (!string.Equals(profile.Sk, "PROFILE", StringComparison.Ordinal))
        {
            errors.Add($"Profile '{profile.Name}' must use SK='PROFILE'.");
        }

        if (!profile.Pk.StartsWith("USER#", StringComparison.Ordinal))
        {
            errors.Add($"Profile '{profile.Name}' must use PK starting with 'USER#'.");
        }

        var userId = profile.GetOptionalString("userId");
        if (string.IsNullOrWhiteSpace(userId))
        {
            errors.Add($"Profile '{profile.Name}' is missing userId.");
        }
        else
        {
            var expectedPk = $"USER#{userId}";
            if (!string.Equals(profile.Pk, expectedPk, StringComparison.Ordinal))
            {
                errors.Add($"Profile '{profile.Name}' PK '{profile.Pk}' does not match userId '{userId}' (expected '{expectedPk}').");
            }
        }
    }

    var distinctFamilyIds = profiles.Select(profile => profile.FamilyId).Distinct(StringComparer.Ordinal).Count();
    if (distinctFamilyIds != 1)
    {
        errors.Add("All profile records must use the same familyId.");
    }

    var duplicateNames = profiles
        .GroupBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToList();

    if (duplicateNames.Count > 0)
    {
        errors.Add($"Duplicate profile names are not allowed: {string.Join(", ", duplicateNames)}.");
    }

    return errors;
}

static List<string> ValidateRecipes(IReadOnlyList<SeedRecipeRecord> recipes, int? expectedRecipes)
{
    var errors = new List<string>();

    if (expectedRecipes.HasValue && recipes.Count != expectedRecipes.Value)
    {
        errors.Add($"Expected exactly {expectedRecipes.Value} recipe records but found {recipes.Count}.");
    }

    foreach (var recipe in recipes)
    {
        if (string.IsNullOrWhiteSpace(recipe.Pk) || !recipe.Pk.StartsWith("FAMILY#", StringComparison.Ordinal))
        {
            errors.Add($"Recipe '{recipe.Name}' must use PK starting with 'FAMILY#'.");
        }

        if (string.IsNullOrWhiteSpace(recipe.Sk) || !recipe.Sk.StartsWith("RECIPE#", StringComparison.Ordinal))
        {
            errors.Add($"Recipe '{recipe.Name}' must use SK starting with 'RECIPE#'.");
        }

        if (string.IsNullOrWhiteSpace(recipe.RecipeId))
        {
            errors.Add($"Recipe '{recipe.Name}' is missing recipeId.");
        }

        if (string.IsNullOrWhiteSpace(recipe.Name))
        {
            errors.Add("Recipe record is missing name.");
        }

        if (string.IsNullOrWhiteSpace(recipe.FamilyId))
        {
            errors.Add($"Recipe '{recipe.Name}' is missing familyId.");
        }

        if (!RecipeCategories.Contains(recipe.Category))
        {
            errors.Add($"Recipe '{recipe.Name}' must use category one of: breakfast, lunch, dinner, snack.");
        }

        if (!RecipeSourceTypes.Contains(recipe.SourceType))
        {
            errors.Add($"Recipe '{recipe.Name}' must use sourceType one of: manual, url, image_upload.");
        }

        if (recipe.IngredientCount <= 0)
        {
            errors.Add($"Recipe '{recipe.Name}' must include at least one ingredient.");
        }

        if (recipe.InstructionCount <= 0)
        {
            errors.Add($"Recipe '{recipe.Name}' must include at least one instruction.");
        }

        if (recipe.ProteinSourceCount == 0)
        {
            errors.Add($"Recipe '{recipe.Name}' must include at least one proteinSource value.");
        }

        if (recipe.CookingMethodCount == 0)
        {
            errors.Add($"Recipe '{recipe.Name}' must include at least one cookingMethod value.");
        }
    }

    var distinctFamilyIds = recipes.Select(recipe => recipe.FamilyId).Distinct(StringComparer.Ordinal).Count();
    if (distinctFamilyIds > 1)
    {
        errors.Add("All recipe records in a migration run must use the same familyId.");
    }

    var duplicateRecipeIds = recipes
        .GroupBy(recipe => recipe.RecipeId, StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToList();

    if (duplicateRecipeIds.Count > 0)
    {
        errors.Add($"Duplicate recipeId values are not allowed: {string.Join(", ", duplicateRecipeIds)}.");
    }

    return errors;
}

static Dictionary<string, AttributeValue> BuildItem(JsonElement raw, string pk, string sk)
{
    var document = Document.FromJson(raw.GetRawText());
    var item = document.ToAttributeMap();

    item["PK"] = new AttributeValue { S = pk };
    item["SK"] = new AttributeValue { S = sk };

    item.Remove("pk");
    item.Remove("sk");

    return item;
}

internal enum MigrationEntity
{
    Users,
    Recipes
}

internal sealed record MigrationArguments
{
    public bool DryRun { get; init; } = true;

    public bool ShowHelp { get; init; }

    public string Region { get; init; } = "us-east-1";

    public MigrationEntity Entity { get; init; } = MigrationEntity.Users;

    public string UsersTableName { get; init; } = "thc-meal-planner-dev-users";

    public string UsersFilePath { get; init; } = Path.Combine(".local", "seed-data", "Users.json");

    public string RecipesTableName { get; init; } = "thc-meal-planner-dev-recipes";

    public string RecipesFilePath { get; init; } = Path.Combine(".local", "seed-data", "Recipes.json");

    public int? ExpectedRecipes { get; init; }

    public static MigrationArguments Parse(string[] args)
    {
        var parsed = new MigrationArguments();

        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];

            switch (current)
            {
                case "--help":
                case "-h":
                    parsed = parsed with { ShowHelp = true };
                    break;
                case "--execute":
                    parsed = parsed with { DryRun = false };
                    break;
                case "--dry-run":
                    parsed = parsed with { DryRun = true };
                    break;
                case "--region":
                    parsed = parsed with { Region = RequireValue(args, ++index, "--region") };
                    break;
                case "--entity":
                    parsed = parsed with { Entity = ParseEntity(RequireValue(args, ++index, "--entity")) };
                    break;
                case "--users-table-name":
                    parsed = parsed with { UsersTableName = RequireValue(args, ++index, "--users-table-name") };
                    break;
                case "--users-file":
                    parsed = parsed with { UsersFilePath = RequireValue(args, ++index, "--users-file") };
                    break;
                case "--recipes-table-name":
                    parsed = parsed with { RecipesTableName = RequireValue(args, ++index, "--recipes-table-name") };
                    break;
                case "--recipes-file":
                    parsed = parsed with { RecipesFilePath = RequireValue(args, ++index, "--recipes-file") };
                    break;
                case "--expected-recipes":
                    parsed = parsed with { ExpectedRecipes = int.Parse(RequireValue(args, ++index, "--expected-recipes")) };
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {current}");
            }
        }

        return parsed;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --project backend/ThcMealPlanner.Migration -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --entity <users|recipes>     Migration entity (default: users)");
        Console.WriteLine("  --region <region>            AWS region (default: us-east-1)");
        Console.WriteLine("  --dry-run                    Validate and preview writes only (default)");
        Console.WriteLine("  --execute                    Perform PutItem writes");
        Console.WriteLine();
        Console.WriteLine("Users migration options:");
        Console.WriteLine("  --users-file <path>          Path to Users.json seed data (default: .local/seed-data/Users.json)");
        Console.WriteLine("  --users-table-name <name>    DynamoDB users table name (default: thc-meal-planner-dev-users)");
        Console.WriteLine();
        Console.WriteLine("Recipes migration options:");
        Console.WriteLine("  --recipes-file <path>        Path to Recipes.json seed data (default: .local/seed-data/Recipes.json)");
        Console.WriteLine("  --recipes-table-name <name>  DynamoDB recipes table name (default: thc-meal-planner-dev-recipes)");
        Console.WriteLine("  --expected-recipes <count>   Optional guardrail expected recipe count");
    }

    private static MigrationEntity ParseEntity(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "users" => MigrationEntity.Users,
            "recipes" => MigrationEntity.Recipes,
            _ => throw new ArgumentException("--entity must be either 'users' or 'recipes'.")
        };
    }

    private static string RequireValue(string[] args, int index, string option)
    {
        if (index >= args.Length)
        {
            throw new ArgumentException($"Missing value for {option}");
        }

        return args[index];
    }
}

internal sealed record SeedProfileRecord(
    string Pk,
    string Sk,
    string Name,
    string Role,
    string FamilyId,
    JsonElement Raw)
{
    public static SeedProfileRecord FromJson(JsonElement element)
    {
        var raw = element.Clone();

        return new SeedProfileRecord(
            GetOptionalString(raw, "PK"),
            GetOptionalString(raw, "SK"),
            GetOptionalString(raw, "name"),
            GetOptionalString(raw, "role"),
            GetOptionalString(raw, "familyId"),
            raw);
    }

    public string GetOptionalString(string propertyName)
    {
        return GetOptionalString(Raw, propertyName);
    }

    private static string GetOptionalString(JsonElement element, string propertyName)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString() ?? string.Empty;
            }

            return property.Value.GetRawText();
        }

        return string.Empty;
    }
}

internal sealed record SeedRecipeRecord(
    string Pk,
    string Sk,
    string RecipeId,
    string Name,
    string FamilyId,
    string Category,
    string SourceType,
    int IngredientCount,
    int InstructionCount,
    int ProteinSourceCount,
    int CookingMethodCount,
    JsonElement Raw)
{
    public static SeedRecipeRecord FromJson(JsonElement element)
    {
        var raw = element.Clone();

        return new SeedRecipeRecord(
            GetOptionalString(raw, "PK"),
            GetOptionalString(raw, "SK"),
            GetOptionalString(raw, "recipeId"),
            GetOptionalString(raw, "name"),
            GetOptionalString(raw, "familyId"),
            GetOptionalString(raw, "category").ToLowerInvariant(),
            GetOptionalString(raw, "sourceType").ToLowerInvariant(),
            GetArrayCount(raw, "ingredients"),
            GetArrayCount(raw, "instructions"),
            GetArrayCount(raw, "proteinSource"),
            GetArrayCount(raw, "cookingMethod"),
            raw);
    }

    private static string GetOptionalString(JsonElement element, string propertyName)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        return string.Empty;
    }

    private static int GetArrayCount(JsonElement element, string propertyName)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return property.Value.ValueKind == JsonValueKind.Array ? property.Value.GetArrayLength() : 0;
        }

        return 0;
    }
}

internal static class RecipeCategories
{
    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        "breakfast",
        "lunch",
        "dinner",
        "snack"
    };

    public static bool Contains(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && All.Contains(value);
    }
}

internal static class RecipeSourceTypes
{
    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        "manual",
        "url",
        "image_upload"
    };

    public static bool Contains(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && All.Contains(value);
    }
}
