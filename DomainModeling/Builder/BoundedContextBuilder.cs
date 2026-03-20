using System.Reflection;

namespace DomainModeling.Builder;

/// <summary>
/// Configures a bounded context: its assemblies and the conventions used to
/// identify DDD building blocks (entities, aggregates, events, handlers, etc.).
/// </summary>
public sealed class BoundedContextBuilder
{
    internal string Name { get; }
    internal Assembly? DomainAssembly { get; private set; }
    internal Assembly? ApplicationAssembly { get; private set; }
    internal Assembly? InfrastructureAssembly { get; private set; }
    internal List<Assembly> AdditionalAssemblies { get; } = [];
    internal List<Assembly> SharedAssemblies { get; } = [];

    internal TypeConventionBuilder EntityConvention { get; } = new();
    internal TypeConventionBuilder AggregateConvention { get; } = new();
    internal TypeConventionBuilder ValueObjectConvention { get; } = new();
    internal TypeConventionBuilder DomainEventConvention { get; } = new();
    internal TypeConventionBuilder IntegrationEventConvention { get; } = new();
    internal TypeConventionBuilder EventHandlerConvention { get; } = new();
    internal TypeConventionBuilder CommandHandlerConvention { get; } = new();
    internal TypeConventionBuilder CommandConvention { get; } = new();
    internal TypeConventionBuilder QueryHandlerConvention { get; } = new();
    internal TypeConventionBuilder RepositoryConvention { get; } = new();
    internal TypeConventionBuilder DomainServiceConvention { get; } = new();
    internal DocumentationBuilder Documentation { get; } = new();

    internal BoundedContextBuilder(string name) => Name = name;

    /// <summary>
    /// Sets the assembly containing the domain layer (entities, aggregates, events).
    /// </summary>
    public BoundedContextBuilder WithDomainAssembly(Assembly assembly)
    {
        DomainAssembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        return this;
    }

    /// <summary>
    /// Sets the assembly containing the application layer (handlers, services).
    /// </summary>
    public BoundedContextBuilder WithApplicationAssembly(Assembly assembly)
    {
        ApplicationAssembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        return this;
    }

    /// <summary>
    /// Sets the assembly containing the infrastructure layer (repositories, adapters).
    /// </summary>
    public BoundedContextBuilder WithInfrastructureAssembly(Assembly assembly)
    {
        InfrastructureAssembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        return this;
    }

    /// <summary>
    /// Adds an additional assembly to scan.
    /// </summary>
    public BoundedContextBuilder WithAssembly(Assembly assembly)
    {
        AdditionalAssemblies.Add(assembly ?? throw new ArgumentNullException(nameof(assembly)));
        return this;
    }

    /// <summary>
    /// Define how entities are identified (e.g. <c>Entities(e => e.InheritsFrom&lt;BaseEntity&gt;())</c>).
    /// </summary>
    public BoundedContextBuilder Entities(Action<TypeConventionBuilder> configure)
    {
        configure(EntityConvention);
        return this;
    }

    /// <summary>
    /// Define how aggregate roots are identified.
    /// </summary>
    public BoundedContextBuilder Aggregates(Action<TypeConventionBuilder> configure)
    {
        configure(AggregateConvention);
        return this;
    }

    /// <summary>
    /// Define how value objects are identified.
    /// </summary>
    public BoundedContextBuilder ValueObjects(Action<TypeConventionBuilder> configure)
    {
        configure(ValueObjectConvention);
        return this;
    }

    /// <summary>
    /// Define how domain events are identified.
    /// </summary>
    public BoundedContextBuilder DomainEvents(Action<TypeConventionBuilder> configure)
    {
        configure(DomainEventConvention);
        return this;
    }

    /// <summary>
    /// Define how integration events are identified.
    /// Integration events cross bounded-context boundaries and are typically
    /// published by event handlers that react to domain events.
    /// </summary>
    public BoundedContextBuilder IntegrationEvents(Action<TypeConventionBuilder> configure)
    {
        configure(IntegrationEventConvention);
        return this;
    }

    /// <summary>
    /// Define how event handlers are identified (domain event handlers, integration event handlers, etc.).
    /// </summary>
    public BoundedContextBuilder EventHandlers(Action<TypeConventionBuilder> configure)
    {
        configure(EventHandlerConvention);
        return this;
    }

    /// <summary>
    /// Define how command handlers are identified.
    /// </summary>
    public BoundedContextBuilder CommandHandlers(Action<TypeConventionBuilder> configure)
    {
        configure(CommandHandlerConvention);
        return this;
    }

    /// <summary>
    /// Define how command message types (DTOs) are identified, e.g.
    /// <c>Commands(c => c.NameEndsWith("Command"))</c>.
    /// Matched types are surfaced as command-handler targets in the graph even when no handler references them yet.
    /// </summary>
    public BoundedContextBuilder Commands(Action<TypeConventionBuilder> configure)
    {
        configure(CommandConvention);
        return this;
    }

    /// <summary>
    /// Define how query handlers are identified.
    /// </summary>
    public BoundedContextBuilder QueryHandlers(Action<TypeConventionBuilder> configure)
    {
        configure(QueryHandlerConvention);
        return this;
    }

    /// <summary>
    /// Define how repositories are identified.
    /// </summary>
    public BoundedContextBuilder Repositories(Action<TypeConventionBuilder> configure)
    {
        configure(RepositoryConvention);
        return this;
    }

    /// <summary>
    /// Define how domain services are identified.
    /// </summary>
    public BoundedContextBuilder DomainServices(Action<TypeConventionBuilder> configure)
    {
        configure(DomainServiceConvention);
        return this;
    }

    /// <summary>
    /// Configure how XML documentation descriptions are loaded for discovered types.
    /// Descriptions serve as friendly names in the output graph.
    /// <para>
    /// Example:
    /// <code>
    /// .WithDocumentation(d => d.AutoDiscover())
    /// .WithDocumentation(d => d.FromFile("path/to/MyProject.xml"))
    /// </code>
    /// </para>
    /// </summary>
    public BoundedContextBuilder WithDocumentation(Action<DocumentationBuilder> configure)
    {
        configure(Documentation);
        return this;
    }

    /// <summary>
    /// Collects all configured assemblies into a flat list for scanning.
    /// </summary>
    internal IReadOnlyList<Assembly> GetAllAssemblies()
    {
        var assemblies = new List<Assembly>();
        if (DomainAssembly is not null) assemblies.Add(DomainAssembly);
        if (ApplicationAssembly is not null) assemblies.Add(ApplicationAssembly);
        if (InfrastructureAssembly is not null) assemblies.Add(InfrastructureAssembly);
        assemblies.AddRange(AdditionalAssemblies);
        assemblies.AddRange(SharedAssemblies);
        return assemblies.Distinct().ToList();
    }

    /// <summary>
    /// Resolves the architectural layer for a given type based on which assembly it belongs to.
    /// Returns "Domain", "Application", "Infrastructure", or <c>null</c> if unknown.
    /// </summary>
    internal string? GetLayer(Type type)
    {
        var assembly = type.Assembly;
        if (DomainAssembly is not null && assembly == DomainAssembly) return "Domain";
        if (ApplicationAssembly is not null && assembly == ApplicationAssembly) return "Application";
        if (InfrastructureAssembly is not null && assembly == InfrastructureAssembly) return "Infrastructure";
        return null;
    }

    /// <summary>
    /// Runs reflection-based discovery and produces a <see cref="Graph.BoundedContextNode"/>.
    /// </summary>
    internal Graph.BoundedContextNode BuildContext()
    {
        var scanner = new Discovery.AssemblyScanner(this);
        return scanner.Scan();
    }
}
