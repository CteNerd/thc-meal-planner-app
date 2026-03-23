using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Text.Json;

var arguments = MigrationArguments.Parse(args);

if (arguments.ShowHelp)
{
    MigrationArguments.PrintUsage();
    return;
}

if (!File.Exists(arguments.UsersFilePath))
{
    Console.Error.WriteLine($"Users seed file not found: {arguments.UsersFilePath}");
    Environment.ExitCode = 1;
    return;
}

var profiles = await LoadProfilesAsync(arguments.UsersFilePath);
var validationErrors = ValidateProfiles(profiles);

if (validationErrors.Count > 0)
{
    Console.Error.WriteLine("Profile seed validation failed:");
    foreach (var error in validationErrors)
    {
        Console.Error.WriteLine($"- {error}");
    }

    Environment.ExitCode = 1;
    return;
}

Console.WriteLine($"Loaded {profiles.Count} profile records from {arguments.UsersFilePath}.");
Console.WriteLine($"Target users table: {arguments.TableName}");
Console.WriteLine(arguments.DryRun ? "Mode: DRY RUN (no writes will be performed)." : "Mode: EXECUTE (writes enabled).");

if (arguments.DryRun)
{
    foreach (var profile in profiles)
    {
        Console.WriteLine($"DRY RUN put: PK={profile.Pk} SK={profile.Sk} role={profile.Role} name={profile.Name}");
    }

    return;
}

var config = new AmazonDynamoDBConfig
{
    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(arguments.Region)
};

using var dynamoDb = new AmazonDynamoDBClient(config);

foreach (var profile in profiles)
{
    var item = BuildItem(profile);

    await dynamoDb.PutItemAsync(new PutItemRequest
    {
        TableName = arguments.TableName,
        Item = item
    });

    Console.WriteLine($"Wrote profile: PK={profile.Pk} SK={profile.Sk} role={profile.Role} name={profile.Name}");
}

Console.WriteLine("Profile migration completed successfully.");

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

static Dictionary<string, AttributeValue> BuildItem(SeedProfileRecord profile)
{
    var item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
    {
        ["PK"] = new AttributeValue { S = profile.Pk },
        ["SK"] = new AttributeValue { S = profile.Sk }
    };

    foreach (var property in profile.Raw.EnumerateObject())
    {
        if (property.NameEquals("PK") || property.NameEquals("SK") ||
            string.Equals(property.Name, "pk", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(property.Name, "sk", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        item[property.Name] = ToAttributeValue(property.Value);
    }

    return item;
}

static AttributeValue ToAttributeValue(JsonElement element)
{
    return element.ValueKind switch
    {
        JsonValueKind.String => new AttributeValue { S = element.GetString() ?? string.Empty },
        JsonValueKind.Number => new AttributeValue { N = element.GetRawText() },
        JsonValueKind.True => new AttributeValue { BOOL = true },
        JsonValueKind.False => new AttributeValue { BOOL = false },
        JsonValueKind.Null => new AttributeValue { NULL = true },
        JsonValueKind.Array => new AttributeValue
        {
            L = element.EnumerateArray().Select(ToAttributeValue).ToList()
        },
        JsonValueKind.Object => new AttributeValue
        {
            M = element.EnumerateObject().ToDictionary(prop => prop.Name, prop => ToAttributeValue(prop.Value), StringComparer.Ordinal)
        },
        _ => throw new InvalidOperationException($"Unsupported JSON value kind: {element.ValueKind}")
    };
}

internal sealed record MigrationArguments
{
    public bool DryRun { get; init; } = true;

    public bool ShowHelp { get; init; }

    public string Region { get; init; } = "us-east-1";

    public string TableName { get; init; } = "thc-meal-planner-dev-users";

    public string UsersFilePath { get; init; } = Path.Combine(".local", "seed-data", "Users.json");

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
                case "--table-name":
                    parsed = parsed with { TableName = RequireValue(args, ++index, "--table-name") };
                    break;
                case "--users-file":
                    parsed = parsed with { UsersFilePath = RequireValue(args, ++index, "--users-file") };
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
        Console.WriteLine("  --users-file <path>  Path to Users.json seed data (default: .local/seed-data/Users.json)");
        Console.WriteLine("  --table-name <name>  DynamoDB users table name (default: thc-meal-planner-dev-users)");
        Console.WriteLine("  --region <region>    AWS region (default: us-east-1)");
        Console.WriteLine("  --dry-run            Validate and preview writes only (default)");
        Console.WriteLine("  --execute            Perform PutItem writes");
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
