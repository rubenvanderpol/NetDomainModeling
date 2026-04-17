using DomainModeling.Builder;

namespace DomainModeling.Discovery;

/// <summary>
/// Scans assemblies configured in a <see cref="BoundedContextBuilder"/>
/// and produces a <see cref="Graph.BoundedContextNode"/> with all discovered types
/// and their relationships.
/// </summary>
internal sealed partial class AssemblyScanner
{
    private readonly BoundedContextBuilder _config;
    private readonly RoslynDocumentationIndexer? _documentationIndexer;

    public AssemblyScanner(BoundedContextBuilder config)
    {
        _config = config;
        _documentationIndexer = RoslynDocumentationIndexer.TryCreate(_config.DocumentationSourceRoots);
    }
}
