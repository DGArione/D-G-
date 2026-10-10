using System.Text.RegularExpressions;

namespace DIG.Core.Intelligence;

/// <summary>Shared helpers for turning imported rows into VMware build records.</summary>
internal static class BuildRowHeuristics
{
    private static readonly Regex BuildRx = new(@"^\d{6,9}$", RegexOptions.Compiled);
    private static readonly Regex DateRx = new(
        @"^(?:\d{4}-\d{2}-\d{2}|\d{1,2}/\d{1,2}/\d{2,4}|[A-Za-z]{3,9}\s+\d{1,2},?\s+\d{4})$",
        RegexOptions.Compiled);

    public static bool IsBuild(string s) => BuildRx.IsMatch(s.Replace(",", string.Empty).Trim());
    public static bool IsDate(string s) => DateRx.IsMatch(s.Trim());

    public static string NormalizeBuild(string s) => s.Replace(",", string.Empty).Trim();

    public static string ProductFrom(string? versionText, string? hint)
    {
        var v = versionText ?? string.Empty;
        if (v.Contains("ESXi", StringComparison.OrdinalIgnoreCase) || v.Contains("ESX ", StringComparison.OrdinalIgnoreCase))
            return "VMware ESXi";
        if (v.Contains("vCenter", StringComparison.OrdinalIgnoreCase) || v.Contains("VCSA", StringComparison.OrdinalIgnoreCase))
            return "VMware vCenter Server";
        return string.IsNullOrWhiteSpace(hint) ? "VMware" : hint!;
    }
}

/// <summary>Parses user-supplied CSV into build records. Accepts a header row that
/// names columns (product/version/build/releaseDate/releaseName, any order) or,
/// failing that, assumes positional order: product,version,build,releaseDate,releaseName.</summary>
public static class BuildCsvImporter
{
    public static IReadOnlyList<VmwareReleaseRecord> Parse(string? csv, string? productHint = null)
    {
        if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<VmwareReleaseRecord>();

        var lines = csv.Replace("\r\n", "\n").Replace("\r", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return Array.Empty<VmwareReleaseRecord>();

        int? iProduct = null, iVersion = null, iBuild = null, iDate = null, iName = null;
        var start = 0;

        var header = SplitLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
        var looksLikeHeader = header.Any(h => h is "build" or "build number" or "buildnumber" or "version");
        if (looksLikeHeader)
        {
            for (var i = 0; i < header.Count; i++)
            {
                var h = header[i];
                if (h is "product") iProduct = i;
                else if (h is "version") iVersion = i;
                else if (h is "build" or "build number" or "buildnumber") iBuild = i;
                else if (h is "releasedate" or "release date" or "date") iDate = i;
                else if (h is "releasename" or "release name" or "name") iName = i;
            }
            start = 1;
        }
        else
        {
            iProduct = 0; iVersion = 1; iBuild = 2; iDate = 3; iName = 4;
        }

        var results = new List<VmwareReleaseRecord>();
        for (var r = start; r < lines.Length; r++)
        {
            var cells = SplitLine(lines[r]);
            string? At(int? idx) => idx is { } i && i < cells.Count ? cells[i].Trim() : null;

            var build = At(iBuild);
            var version = At(iVersion);
            if (string.IsNullOrWhiteSpace(build)) continue;
            build = BuildRowHeuristics.NormalizeBuild(build);
            if (!BuildRowHeuristics.IsBuild(build)) continue;

            var product = At(iProduct);
            if (string.IsNullOrWhiteSpace(product))
                product = BuildRowHeuristics.ProductFrom(version, productHint);

            results.Add(new VmwareReleaseRecord(
                product!,
                string.IsNullOrWhiteSpace(version) ? build : version!,
                build,
                string.IsNullOrWhiteSpace(At(iDate)) ? null : At(iDate),
                null,
                string.IsNullOrWhiteSpace(At(iName)) ? null : At(iName)));
        }
        return results;
    }

    private static List<string> SplitLine(string line)
    {
        var cells = new List<string>();
        var cur = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if ((ch == ',' || ch == '\t') && !inQuotes)
            {
                cells.Add(cur.ToString());
                cur.Clear();
            }
            else cur.Append(ch);
        }
        cells.Add(cur.ToString());
        return cells;
    }
}

/// <summary>Turns rows scraped from an HTML table (each row = array of cell texts,
/// typically obtained from the browser) into build records, heuristically picking
/// out the build number, version and date columns.</summary>
public static class BroadcomTableParser
{
    public static IReadOnlyList<VmwareReleaseRecord> ParseRows(IEnumerable<string[]> rows, string? productHint = null)
    {
        var results = new List<VmwareReleaseRecord>();
        foreach (var row in rows)
        {
            if (row is null || row.Length == 0) continue;

            string? build = null, version = null, date = null;
            foreach (var raw in row)
            {
                var cell = (raw ?? string.Empty).Trim();
                if (cell.Length == 0) continue;

                if (build is null && BuildRowHeuristics.IsBuild(cell))
                {
                    build = BuildRowHeuristics.NormalizeBuild(cell);
                    continue;
                }
                if (date is null && BuildRowHeuristics.IsDate(cell))
                {
                    date = cell;
                    continue;
                }
                // Prefer a cell that names the product/version.
                if (version is null &&
                    (cell.Contains("ESXi", StringComparison.OrdinalIgnoreCase) ||
                     cell.Contains("vCenter", StringComparison.OrdinalIgnoreCase) ||
                     cell.Contains("VCSA", StringComparison.OrdinalIgnoreCase) ||
                     Regex.IsMatch(cell, @"^\d+\.\d+")))
                {
                    version = cell;
                }
            }

            if (build is null) continue;
            var product = BuildRowHeuristics.ProductFrom(version, productHint);
            results.Add(new VmwareReleaseRecord(
                product,
                string.IsNullOrWhiteSpace(version) ? build : version!,
                build,
                date));
        }
        return results;
    }
}
