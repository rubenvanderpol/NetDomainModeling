using System.Collections.Concurrent;
using System.Xml.Linq;

namespace DomainModeling.Discovery;

/// <summary>
/// Loads type summaries from XML documentation files emitted next to assemblies
/// (member IDs prefixed with <c>T:</c> for types).
/// </summary>
internal static class DocumentationCommentReader
{
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns plain text for the type's <c>&lt;summary&gt;</c>, or <c>null</c> if none is available.
    /// </summary>
    public static string? TryGetTypeSummary(Type type)
    {
        if (type.FullName is null)
            return null;

        var assembly = type.Assembly;
        var location = assembly.Location;
        if (string.IsNullOrEmpty(location))
            return null;

        var xmlPath = Path.ChangeExtension(location, ".xml");
        if (!File.Exists(xmlPath))
            return null;

        var map = Cache.GetOrAdd(xmlPath, static path => LoadXml(path));
        var id = GetTypeDocumentationMemberName(type);
        return map.TryGetValue(id, out var text) ? text : null;
    }

    private static string GetTypeDocumentationMemberName(Type type)
    {
        var documented = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        return "T:" + documented.FullName!;
    }

    private static IReadOnlyDictionary<string, string> LoadXml(string xmlPath)
    {
        try
        {
            var doc = XDocument.Load(xmlPath, LoadOptions.PreserveWhitespace);
            var root = doc.Root;
            if (root is null)
                return new Dictionary<string, string>(StringComparer.Ordinal);

            XNamespace ns = root.GetDefaultNamespace() == XNamespace.None ? XNamespace.None : root.GetDefaultNamespace();
            var members = root.Element(ns + "members");
            if (members is null)
                return new Dictionary<string, string>(StringComparer.Ordinal);

            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var member in members.Elements(ns + "member"))
            {
                var nameAttr = member.Attribute("name");
                if (nameAttr is null || !nameAttr.Value.StartsWith("T:", StringComparison.Ordinal))
                    continue;

                var summary = member.Element(ns + "summary");
                if (summary is null)
                    continue;

                var text = NormalizeSummaryText(summary.Value);
                if (text.Length > 0)
                    dict[nameAttr.Value] = text;
            }

            return dict;
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static string NormalizeSummaryText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        return string.Join(' ', raw.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
