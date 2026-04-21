using DomainModeling.Builder;
using DomainModeling.Graph;

namespace DomainModeling.Discovery;

/// <summary>
/// Scans assemblies configured in a <see cref="BoundedContextBuilder"/>
/// and produces a <see cref="BoundedContextNode"/> with all discovered types
/// and their relationships.
/// </summary>
internal sealed class AssemblyScanner
{
    private readonly DomainDiscoveryPipeline _pipeline;

    public AssemblyScanner(BoundedContextBuilder config)
    {
        _pipeline = new DomainDiscoveryPipeline(config);
    }

    public BoundedContextNode Scan() => _pipeline.Run();
}
