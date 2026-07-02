namespace NexusScholar.ResearchWorkspace;

public static class SearchImportAliases
{
    public static string NormalizeSource(string source)
    {
        var normalized = source.Trim().ToLowerInvariant();
        return normalized switch
        {
            "scopus" => "scopus",
            "web-of-science" or "webofscience" or "wos" => "web-of-science",
            "google-scholar" or "googlescholar" or "scholar" => "google-scholar",
            "openalex" => "openalex",
            "semantic-scholar" or "semanticscholar" or "s2" => "semantic-scholar",
            "other" => "other",
            _ => throw new ArgumentException($"Unsupported search source alias: {source}")
        };
    }

    public static string NormalizeFormat(string format)
    {
        var normalized = format.Trim().ToLowerInvariant();
        return normalized switch
        {
            "csv" or "scopus-csv" => "csv",
            "ris" or "wos-ris" or "openalex-ris" => "ris",
            "bibtex" or "bib" or "semantic-scholar-bibtex" or "google-scholar-bibtex" => "bibtex",
            _ => throw new ArgumentException($"Unsupported search format alias: {format}")
        };
    }

    public static string ParserFormatFor(string normalizedFormat)
    {
        return normalizedFormat switch
        {
            "csv" => "scopus-csv",
            "ris" => "ris",
            "bibtex" => "bibtex",
            _ => throw new ArgumentException($"Unsupported search format alias: {normalizedFormat}")
        };
    }

    public static string ExtensionFor(string normalizedFormat)
    {
        return normalizedFormat switch
        {
            "csv" => "csv",
            "ris" => "ris",
            "bibtex" => "bib",
            _ => throw new ArgumentException($"Unsupported search format alias: {normalizedFormat}")
        };
    }
}
