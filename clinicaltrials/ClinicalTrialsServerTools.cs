using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace ClinicalTrialsServer.Tools;

[McpServerToolType]
public static class ClinicalTrialsTools
{
    private const string CompactFields = "NCTId,BriefTitle,OverallStatus,Phase,Condition,InterventionType,InterventionName,LeadSponsorName,StartDate,CompletionDate,LocationFacility,LocationCity,LocationState,LocationCountry";

    [McpServerTool, Description("Search ClinicalTrials.gov studies and return the complete study records defined by the ClinicalTrials.gov study data structure.")]
    public static async Task<string> GetStudiesFull(
        HttpClient client,
        [Description("Condition or disease search expression (query.cond).")] string? condition = null,
        [Description("Other terms search expression (query.term).")] string? terms = null,
        [Description("Location search expression (query.locn).")] string? location = null,
        [Description("Study title or acronym search expression (query.titles).")] string? titles = null,
        [Description("Intervention or treatment search expression (query.intr).")] string? intervention = null,
        [Description("Outcome measure search expression (query.outc).")] string? outcome = null,
        [Description("Sponsor or collaborator search expression (query.spons).")] string? sponsor = null,
        [Description("Lead sponsor name search expression (query.lead).")] string? leadSponsor = null,
        [Description("Study ID search expression (query.id).")] string? studyId = null,
        [Description("Patient search expression (query.patient).")] string? patient = null,
        [Description("Overall statuses separated by commas or pipes (filter.overallStatus).")] string? overallStatus = null,
        [Description("NCT IDs separated by commas or pipes (filter.ids).")] string? ids = null,
        [Description("Advanced Essie filter expression (filter.advanced).")] string? advancedFilter = null,
        [Description("Geographic distance filter expression (filter.geo).")] string? geoFilter = null,
        [Description("Synonym filters separated by commas or pipes (filter.synonyms).")] string? synonymsFilter = null,
        [Description("Overall statuses applied after aggregation (postFilter.overallStatus).")] string? postOverallStatus = null,
        [Description("Geographic distance filter applied after aggregation (postFilter.geo).")] string? postGeoFilter = null,
        [Description("NCT IDs applied after aggregation (postFilter.ids).")] string? postIds = null,
        [Description("Advanced expression applied after aggregation (postFilter.advanced).")] string? postAdvancedFilter = null,
        [Description("Synonym filters applied after aggregation (postFilter.synonyms).")] string? postSynonymsFilter = null,
        [Description("Aggregation filter expression (aggFilters).")] string? aggregationFilters = null,
        [Description("Geographic relevance decay expression (geoDecay).")] string? geoDecay = null,
        [Description("Sort expressions separated by commas or pipes.")] string? sort = null,
        [Description("Number of complete studies to return, from 0 through 1000. Keep this small because full records can be large.")] int pageSize = 1,
        [Description("Whether to calculate the total number of matching studies.")] bool countTotal = false,
        [Description("Continuation token returned by a previous call.")] string? pageToken = null)
    {
        var requestUri = BuildStudiesRequestUri(
            condition, terms, location, titles, intervention, outcome, sponsor, leadSponsor, studyId, patient,
            overallStatus, ids, advancedFilter, geoFilter, synonymsFilter, postOverallStatus, postGeoFilter,
            postIds, postAdvancedFilter, postSynonymsFilter, aggregationFilters, geoDecay, sort, pageSize,
            countTotal, pageToken);

        using var jsonDocument = await client.ReadJsonDocumentAsync(requestUri);
        return JsonSerializer.Serialize(jsonDocument.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Search ClinicalTrials.gov studies and return compact study summaries with pagination metadata.")]
    public static async Task<string> GetStudiesCompact(
        HttpClient client,
        [Description("Condition or disease search expression (query.cond).")] string? condition = null,
        [Description("Other terms search expression (query.term).")] string? terms = null,
        [Description("Location search expression (query.locn).")] string? location = null,
        [Description("Study title or acronym search expression (query.titles).")] string? titles = null,
        [Description("Intervention or treatment search expression (query.intr).")] string? intervention = null,
        [Description("Outcome measure search expression (query.outc).")] string? outcome = null,
        [Description("Sponsor or collaborator search expression (query.spons).")] string? sponsor = null,
        [Description("Lead sponsor name search expression (query.lead).")] string? leadSponsor = null,
        [Description("Study ID search expression (query.id).")] string? studyId = null,
        [Description("Patient search expression (query.patient).")] string? patient = null,
        [Description("Overall statuses separated by commas or pipes (filter.overallStatus).")] string? overallStatus = null,
        [Description("NCT IDs separated by commas or pipes (filter.ids).")] string? ids = null,
        [Description("Advanced Essie filter expression (filter.advanced).")] string? advancedFilter = null,
        [Description("Geographic distance filter expression (filter.geo).")] string? geoFilter = null,
        [Description("Synonym filters separated by commas or pipes (filter.synonyms).")] string? synonymsFilter = null,
        [Description("Overall statuses applied after aggregation (postFilter.overallStatus).")] string? postOverallStatus = null,
        [Description("Geographic distance filter applied after aggregation (postFilter.geo).")] string? postGeoFilter = null,
        [Description("NCT IDs applied after aggregation (postFilter.ids).")] string? postIds = null,
        [Description("Advanced expression applied after aggregation (postFilter.advanced).")] string? postAdvancedFilter = null,
        [Description("Synonym filters applied after aggregation (postFilter.synonyms).")] string? postSynonymsFilter = null,
        [Description("Aggregation filter expression (aggFilters).")] string? aggregationFilters = null,
        [Description("Geographic relevance decay expression (geoDecay).")] string? geoDecay = null,
        [Description("Sort expressions separated by commas or pipes.")] string? sort = null,
        [Description("Number of studies to return, from 0 through 1000.")] int pageSize = 10,
        [Description("Whether to calculate the total number of matching studies.")] bool countTotal = false,
        [Description("Continuation token returned by a previous call.")] string? pageToken = null)
    {
        var requestUri = BuildStudiesRequestUri(
            condition, terms, location, titles, intervention, outcome, sponsor, leadSponsor, studyId, patient,
            overallStatus, ids, advancedFilter, geoFilter, synonymsFilter, postOverallStatus, postGeoFilter,
            postIds, postAdvancedFilter, postSynonymsFilter, aggregationFilters, geoDecay, sort, pageSize,
            countTotal, pageToken, CompactFields);

        using var jsonDocument = await client.ReadJsonDocumentAsync(requestUri);
        var root = jsonDocument.RootElement;
        var studies = root.GetProperty("studies").EnumerateArray().ToArray();

        if (studies.Length == 0)
        {
            return "No studies matched the search criteria.";
        }

        var summaries = studies.Select(FormatStudy);
        var totalCount = root.TryGetProperty("totalCount", out var total) ? total.GetInt32().ToString(CultureInfo.InvariantCulture) : "Not requested";
        var nextPageToken = GetString(root, "nextPageToken") ?? "None";

        return $"Total count: {totalCount}\nNext page token: {nextPageToken}\n\n{string.Join("\n---\n", summaries)}";
    }

    private static string BuildStudiesRequestUri(
        string? condition,
        string? terms,
        string? location,
        string? titles,
        string? intervention,
        string? outcome,
        string? sponsor,
        string? leadSponsor,
        string? studyId,
        string? patient,
        string? overallStatus,
        string? ids,
        string? advancedFilter,
        string? geoFilter,
        string? synonymsFilter,
        string? postOverallStatus,
        string? postGeoFilter,
        string? postIds,
        string? postAdvancedFilter,
        string? postSynonymsFilter,
        string? aggregationFilters,
        string? geoDecay,
        string? sort,
        int pageSize,
        bool countTotal,
        string? pageToken,
        string? fields = null)
    {
        if (pageSize is < 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 0 and 1000.");
        }

        var query = new List<string>
        {
            "format=json",
            "markupFormat=markdown",
            $"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}",
            $"countTotal={countTotal.ToString().ToLowerInvariant()}"
        };

        AddQueryParameter(query, "fields", fields);
        AddQueryParameter(query, "query.cond", condition);
        AddQueryParameter(query, "query.term", terms);
        AddQueryParameter(query, "query.locn", location);
        AddQueryParameter(query, "query.titles", titles);
        AddQueryParameter(query, "query.intr", intervention);
        AddQueryParameter(query, "query.outc", outcome);
        AddQueryParameter(query, "query.spons", sponsor);
        AddQueryParameter(query, "query.lead", leadSponsor);
        AddQueryParameter(query, "query.id", studyId);
        AddQueryParameter(query, "query.patient", patient);
        AddQueryParameter(query, "filter.overallStatus", overallStatus);
        AddQueryParameter(query, "filter.ids", ids);
        AddQueryParameter(query, "filter.advanced", advancedFilter);
        AddQueryParameter(query, "filter.geo", geoFilter);
        AddQueryParameter(query, "filter.synonyms", synonymsFilter);
        AddQueryParameter(query, "postFilter.overallStatus", postOverallStatus);
        AddQueryParameter(query, "postFilter.geo", postGeoFilter);
        AddQueryParameter(query, "postFilter.ids", postIds);
        AddQueryParameter(query, "postFilter.advanced", postAdvancedFilter);
        AddQueryParameter(query, "postFilter.synonyms", postSynonymsFilter);
        AddQueryParameter(query, "aggFilters", aggregationFilters);
        AddQueryParameter(query, "geoDecay", geoDecay);
        AddQueryParameter(query, "sort", sort);
        AddQueryParameter(query, "pageToken", pageToken);

        return $"studies?{string.Join('&', query)}";
    }

    private static string FormatStudy(JsonElement study)
    {
        var protocol = study.GetProperty("protocolSection");
        var identification = protocol.GetProperty("identificationModule");
        var nctId = GetString(identification, "nctId") ?? "Not available";
        var title = GetString(identification, "briefTitle") ?? "Not available";
        var status = GetNestedString(protocol, "statusModule", "overallStatus") ?? "Not available";
        var phases = GetNestedStrings(protocol, "designModule", "phases");
        var conditions = GetNestedStrings(protocol, "conditionsModule", "conditions");
        var interventions = GetInterventions(protocol);
        var sponsor = GetNestedString(protocol, "sponsorCollaboratorsModule", "leadSponsor", "name") ?? "Not available";
        var startDate = GetNestedString(protocol, "statusModule", "startDateStruct", "date") ?? "Not available";
        var completionDate = GetNestedString(protocol, "statusModule", "completionDateStruct", "date") ?? "Not available";
        var locations = GetLocations(protocol);

        return $"""
                NCT ID: {nctId}
                Title: {title}
                Status: {status}
                Phase: {JoinOrDefault(phases)}
                Conditions: {JoinOrDefault(conditions)}
                Interventions: {JoinOrDefault(interventions)}
                Lead sponsor: {sponsor}
                Dates: {startDate} to {completionDate}
                Locations: {JoinOrDefault(locations)}
                URL: https://clinicaltrials.gov/study/{nctId}
                """;
    }

    private static IEnumerable<string> GetInterventions(JsonElement protocol)
    {
        if (!TryGetNestedProperty(protocol, out var interventions, "armsInterventionsModule", "interventions"))
        {
            return [];
        }

        return interventions.EnumerateArray().Select(item =>
        {
            var type = GetString(item, "type");
            var name = GetString(item, "name") ?? "Unnamed";
            return type is null ? name : $"{type}: {name}";
        });
    }

    private static IEnumerable<string> GetLocations(JsonElement protocol)
    {
        if (!TryGetNestedProperty(protocol, out var locations, "contactsLocationsModule", "locations"))
        {
            return [];
        }

        return locations.EnumerateArray().Take(5).Select(location =>
        {
            var parts = new[]
            {
                GetString(location, "facility"),
                GetString(location, "city"),
                GetString(location, "state"),
                GetString(location, "country")
            };
            return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        });
    }

    private static IEnumerable<string> GetNestedStrings(JsonElement element, params string[] path)
    {
        return TryGetNestedProperty(element, out var value, path)
            ? value.EnumerateArray().Select(item => item.GetString()).OfType<string>()
            : [];
    }

    private static string? GetNestedString(JsonElement element, params string[] path)
    {
        return TryGetNestedProperty(element, out var value, path) ? value.GetString() : null;
    }

    private static bool TryGetNestedProperty(JsonElement element, out JsonElement value, params string[] path)
    {
        value = element;
        foreach (var propertyName in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(propertyName, out value))
            {
                return false;
            }
        }

        return value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
    }

    private static string JoinOrDefault(IEnumerable<string> values)
    {
        var items = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return items.Length == 0 ? "Not available" : string.Join("; ", items);
    }

    private static void AddQueryParameter(ICollection<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }
}

internal static class HttpClientExt
{
    public static async Task<JsonDocument> ReadJsonDocumentAsync(this HttpClient client, string requestUri)
    {
        using var response = await client.GetAsync(requestUri);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"ClinicalTrials.gov returned {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}");
        }

        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}