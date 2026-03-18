using System.Reflection;

namespace DomainModeling.Builder;

/// <summary>
/// Fluent builder to configure how XML documentation descriptions are loaded
/// for discovered domain types. Descriptions appear as friendly names in the graph output.
/// </summary>
public sealed class DocumentationBuilder
{
    internal List<string> XmlDocPaths { get; } = [];
    internal bool AutoDiscoverEnabled { get; private set; }

    /// <summary>
    /// Automatically discover XML documentation files alongside the configured assemblies.
    /// Looks for <c>AssemblyName.xml</c> in the same directory as the DLL.
    /// <para>
    /// This requires <c>&lt;GenerateDocumentationFile&gt;true&lt;/GenerateDocumentationFile&gt;</c>
    /// in the target project's <c>.csproj</c>.
    /// </para>
    /// </summary>
    public DocumentationBuilder AutoDiscover()
    {
        AutoDiscoverEnabled = true;
        return this;
    }

    /// <summary>
    /// Provide an explicit path to an XML documentation file.
    /// </summary>
    public DocumentationBuilder FromFile(string xmlDocFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlDocFilePath);
        XmlDocPaths.Add(xmlDocFilePath);
        return this;
    }

    /// <summary>
    /// Provide an XML documentation file path relative to a given assembly's location.
    /// </summary>
    public DocumentationBuilder FromAssemblyXml(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (!string.IsNullOrEmpty(assembly.Location))
        {
            var xmlPath = Path.ChangeExtension(assembly.Location, ".xml");
            XmlDocPaths.Add(xmlPath);
        }
        return this;
    }
}
