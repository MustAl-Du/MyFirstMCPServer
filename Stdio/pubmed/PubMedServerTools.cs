using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace PubMedServer.Tools.Stdio;

[McpServerToolType]
public static class PubMedTools
{
    private static readonly HashSet<string> SupportedSortValues = new(StringComparer.Ordinal)
    {
        "pub_date",
        "Author",
        "JournalName",
        "relevance"
    };

    [McpServerTool, Description("Search PubMed with NCBI ESearch and return matching PubMed IDs, result counts, query translation, and optional History server metadata.")]
    public static async Task<string> SearchPubMed(
        HttpClient client,
        [Description("PubMed query using Entrez search syntax, including optional field tags and Boolean operators.")] string term,
        [Description("Zero-based index of the first PMID to return. PubMed exposes only the first 10,000 matches through ESearch.")] int returnStart = 0,
        [Description("Number of PMIDs to return, from 0 through 10,000.")] int returnMaximum = 20,
        [Description("Sort order: relevance, pub_date, Author, or JournalName.")] string? sort = null,
        [Description("PubMed field to apply to the entire query, such as title or author.")] string? field = null,
        [Description("Post the complete result set to the NCBI History server and return WebEnv and query_key values.")] bool useHistory = false,
        [Description("WebEnv value from an earlier E-utility call. Requires useHistory=true.")] string? webEnvironment = null,
        [Description("Query key to intersect with this search. Requires webEnvironment and useHistory=true.")] int? queryKey = null,
        [Description("Date type for date filters: pdat (publication), edat (Entrez), or mdat (modification).")]
        string? dateType = null,
        [Description("Limit results to records within this many days according to dateType.")] int? relativeDays = null,
        [Description("Start of a date range in YYYY, YYYY/MM, or YYYY/MM/DD format. Must be supplied with maximumDate.")] string? minimumDate = null,
        [Description("End of a date range in YYYY, YYYY/MM, or YYYY/MM/DD format. Must be supplied with minimumDate.")] string? maximumDate = null)
    {
        ValidateParameters(term, returnStart, returnMaximum, sort, useHistory, webEnvironment, queryKey,
            dateType, relativeDays, minimumDate, maximumDate);

        var query = new List<string>
        {
            "db=pubmed",
            $"term={Uri.EscapeDataString(term)}",
            "retmode=json",
            $"retstart={returnStart.ToString(CultureInfo.InvariantCulture)}",
            $"retmax={returnMaximum.ToString(CultureInfo.InvariantCulture)}",
            "tool=pubmed-mcp-server"
        };

        AddQueryParameter(query, "sort", sort);
        AddQueryParameter(query, "field", field);
        AddQueryParameter(query, "usehistory", useHistory ? "y" : null);
        AddQueryParameter(query, "WebEnv", webEnvironment);
        AddQueryParameter(query, "query_key", queryKey?.ToString(CultureInfo.InvariantCulture));
        AddQueryParameter(query, "datetype", dateType);
        AddQueryParameter(query, "reldate", relativeDays?.ToString(CultureInfo.InvariantCulture));
        AddQueryParameter(query, "mindate", minimumDate);
        AddQueryParameter(query, "maxdate", maximumDate);
        AddQueryParameter(query, "email", Environment.GetEnvironmentVariable("NCBI_EMAIL"));
        AddQueryParameter(query, "api_key", Environment.GetEnvironmentVariable("NCBI_API_KEY"));

        using var response = await client.GetAsync($"esearch.fcgi?{string.Join('&', query)}");
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"NCBI ESearch returned {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}");
        }

        using var jsonDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return JsonSerializer.Serialize(jsonDocument.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }

    private static void ValidateParameters(
        string term,
        int returnStart,
        int returnMaximum,
        string? sort,
        bool useHistory,
        string? webEnvironment,
        int? queryKey,
        string? dateType,
        int? relativeDays,
        string? minimumDate,
        string? maximumDate)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            throw new ArgumentException("A PubMed search term is required.", nameof(term));
        }

        if (returnStart is < 0 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(returnStart), "Return start must be between 0 and 9999.");
        }

        if (returnMaximum is < 0 or > 10000 || returnStart + returnMaximum > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(returnMaximum), "The requested PMID window must remain within the first 10,000 results.");
        }

        if (sort is not null && !SupportedSortValues.Contains(sort))
        {
            throw new ArgumentException("Sort must be relevance, pub_date, Author, or JournalName.", nameof(sort));
        }

        if ((!string.IsNullOrWhiteSpace(webEnvironment) || queryKey.HasValue) && !useHistory)
        {
            throw new ArgumentException("History server parameters require useHistory=true.", nameof(useHistory));
        }

        if (queryKey.HasValue && string.IsNullOrWhiteSpace(webEnvironment))
        {
            throw new ArgumentException("queryKey requires a webEnvironment value.", nameof(queryKey));
        }

        if (queryKey < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queryKey), "Query key cannot be negative.");
        }

        if (relativeDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeDays), "Relative days cannot be negative.");
        }

        if ((relativeDays.HasValue || minimumDate is not null || maximumDate is not null) && string.IsNullOrWhiteSpace(dateType))
        {
            throw new ArgumentException("A dateType is required when applying date filters.", nameof(dateType));
        }

        if ((minimumDate is null) != (maximumDate is null))
        {
            throw new ArgumentException("minimumDate and maximumDate must be supplied together.", nameof(minimumDate));
        }
    }

    private static void AddQueryParameter(ICollection<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }
}