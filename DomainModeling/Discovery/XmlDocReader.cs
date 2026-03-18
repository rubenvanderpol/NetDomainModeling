using System.Xml.Linq;

namespace DomainModeling.Discovery;

/// <summary>
/// Reads .NET XML documentation files (produced by &lt;GenerateDocumentationFile&gt;)
/// and extracts &lt;summary&gt; text for types and members.
/// </summary>
internal sealed class XmlDocReader
{
    private readonly Dictionary<string, string> _summaries = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates an empty reader (no documentation loaded).
    /// </summary>
    public static XmlDocReader Empty => new();

    private XmlDocReader() { }

    /// <summary>
    /// Creates a reader from one or more XML documentation file paths.
    /// </summary>
    public static XmlDocReader Load(IEnumerable<string> xmlDocPaths)
    {
        var reader = new XmlDocReader();
        foreach (var path in xmlDocPaths)
        {
            reader.LoadFile(path);
        }
        return reader;
    }

    /// <summary>
    /// Creates a reader by auto-discovering XML doc files next to the given assemblies.
    /// Looks for a .xml file with the same name as the assembly DLL in the same directory.
    /// </summary>
    public static XmlDocReader AutoDiscover(IEnumerable<System.Reflection.Assembly> assemblies)
    {
        var paths = new List<string>();
        foreach (var asm in assemblies)
        {
            if (string.IsNullOrEmpty(asm.Location))
                continue;

            var xmlPath = Path.ChangeExtension(asm.Location, ".xml");
            if (File.Exists(xmlPath))
                paths.Add(xmlPath);
        }

        return paths.Count > 0 ? Load(paths) : Empty;
    }

    /// <summary>
    /// Gets the summary description for a type, or null if not found.
    /// </summary>
    public string? GetTypeSummary(Type type)
    {
        var key = $"T:{type.FullName}";
        return _summaries.GetValueOrDefault(key);
    }

    /// <summary>
    /// Gets the summary for a type by full name, or null if not found.
    /// </summary>
    public string? GetTypeSummary(string fullName)
    {
        var key = $"T:{fullName}";
        return _summaries.GetValueOrDefault(key);
    }

    /// <summary>
    /// Returns true if any documentation was loaded.
    /// </summary>
    public bool HasDocumentation => _summaries.Count > 0;

    private void LoadFile(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            var doc = XDocument.Load(path);
            var members = doc.Descendants("member");

            foreach (var member in members)
            {
                var name = member.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(name))
                    continue;

                var summary = member.Element("summary");
                if (summary is null)
                    continue;

                var text = CleanSummary(summary);
                if (!string.IsNullOrWhiteSpace(text))
                    _summaries.TryAdd(name, text);
            }
        }
        catch
        {
            // Silently skip unreadable XML files
        }
    }

    /// <summary>
    /// Cleans a summary element: strips leading/trailing whitespace, collapses
    /// internal whitespace, and extracts text from inline elements like &lt;see cref="..."/&gt;.
    /// </summary>
    private static string CleanSummary(XElement summary)
    {
        var parts = new List<string>();
        ExtractText(summary, parts);
        var joined = string.Join(" ", parts);
        // Collapse multiple spaces
        return System.Text.RegularExpressions.Regex.Replace(joined, @"\s+", " ").Trim();
    }

    private static void ExtractText(XElement element, List<string> parts)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    var t = text.Value.Trim();
                    if (!string.IsNullOrEmpty(t))
                        parts.Add(t);
                    break;

                case XElement child:
                    if (child.Name.LocalName is "see" or "seealso")
                    {
                        var cref = child.Attribute("cref")?.Value;
                        if (cref is not null)
                        {
                            // Strip the member-type prefix (T:, M:, P:, etc.)
                            var lastDot = cref.LastIndexOf('.');
                            var display = lastDot >= 0 ? cref[(lastDot + 1)..] : cref;
                            if (display.Length > 2 && display[1] == ':')
                                display = display[2..];
                            parts.Add(display);
                        }
                    }
                    else
                    {
                        ExtractText(child, parts);
                    }
                    break;
            }
        }
    }
}
