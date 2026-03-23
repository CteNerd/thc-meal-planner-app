using System.Net;
using System.Text.RegularExpressions;

namespace ThcMealPlanner.Api.Recipes;

public interface IRecipeImportService
{
    Task<ImportedRecipeDraft> ImportFromUrlAsync(string url, CancellationToken cancellationToken = default);
}

public sealed partial class RecipeImportService : IRecipeImportService
{
    private const int MaxResponseBytes = 1024 * 1024;
    private readonly HttpClient _httpClient;

    public RecipeImportService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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

        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + bytesRead > MaxResponseBytes)
            {
                throw new InvalidOperationException("Recipe source exceeded the 1 MB response limit.");
            }

            await memory.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        var html = System.Text.Encoding.UTF8.GetString(memory.ToArray());
        return ParseDraft(uri, html);
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

    private static ImportedRecipeDraft ParseDraft(Uri sourceUrl, string html)
    {
        var normalizedText = NormalizeWhitespace(StripHtml(html));
        var lines = normalizedText
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        var title = ExtractTitle(html) ?? lines.FirstOrDefault() ?? "Imported recipe";
        var description = ExtractDescription(html) ?? lines.Skip(1).FirstOrDefault();
        var ingredients = ExtractSection(lines, "ingredients", new[] { "instructions", "method", "directions", "notes" })
            .Select(line => new RecipeIngredientModel { Name = line })
            .Take(20)
            .ToList();
        var instructions = ExtractSection(lines, "instructions", new[] { "notes", "nutrition" })
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

    private static List<string> ExtractSection(IReadOnlyList<string> lines, string startHeading, IReadOnlyList<string> stopHeadings)
    {
        var startIndex = lines.ToList().FindIndex(line => string.Equals(line, startHeading, StringComparison.OrdinalIgnoreCase));
        if (startIndex < 0)
        {
            return [];
        }

        var sectionLines = new List<string>();
        for (var index = startIndex + 1; index < lines.Count; index++)
        {
            var line = lines[index];
            if (stopHeadings.Any(stop => string.Equals(line, stop, StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            if (line.Length == 0)
            {
                continue;
            }

            sectionLines.Add(line.TrimStart('-', '*', ' ', '\t'));
        }

        return sectionLines;
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
}