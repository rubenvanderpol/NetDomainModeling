using System.Reflection;
using DomainModeling.Builder;

namespace DomainModeling.Discovery;

/// <summary>
/// Assembly ordering, safe type enumeration, deduplication, and convention-based categorization.
/// </summary>
internal static class ScannedTypeCatalog
{
    public static Dictionary<Assembly, int> BuildAssemblyOrderIndex(IReadOnlyList<Assembly> assemblies)
    {
        var assemblyOrder = new Dictionary<Assembly, int>();
        for (var i = 0; i < assemblies.Count; i++)
        {
            if (!assemblyOrder.ContainsKey(assemblies[i]))
                assemblyOrder[assemblies[i]] = i;
        }

        return assemblyOrder;
    }

    public static DomainTypeCategories CategorizeDomainTypes(BoundedContextBuilder config, List<Type> allTypes)
    {
        bool OwnedElsewhere(Type t) => config.ExternallyOwnedSharedAssemblies.Contains(t.Assembly);

        var entityTypes = allTypes.Where(t => !OwnedElsewhere(t) && config.EntityConvention.Matches(t)).ToList();
        var aggregateTypes = allTypes.Where(t => !OwnedElsewhere(t) && config.AggregateConvention.Matches(t)).ToList();
        var valueObjectTypes = allTypes.Where(t => !OwnedElsewhere(t) && config.ValueObjectConvention.Matches(t)).ToList();
        var domainEventTypes = allTypes.Where(t => !OwnedElsewhere(t) && config.DomainEventConvention.Matches(t)).ToList();
        var integrationEventTypesAll = allTypes.Where(t => config.IntegrationEventConvention.Matches(t)).ToList();
        var integrationEventTypes = integrationEventTypesAll.Where(t => !OwnedElsewhere(t)).ToList();
        var eventHandlerTypes = allTypes.Where(t => !OwnedElsewhere(t) && config.EventHandlerConvention.Matches(t)).ToList();
        var commandHandlerTypes = allTypes.Where(t => !OwnedElsewhere(t) && config.CommandHandlerConvention.Matches(t)).ToList();
        var queryHandlerTypes = allTypes.Where(t => !OwnedElsewhere(t) && config.QueryHandlerConvention.Matches(t)).ToList();
        var repositoryTypes = allTypes.Where(t => !OwnedElsewhere(t) && config.RepositoryConvention.Matches(t)).ToList();
        var domainServiceTypes = allTypes.Where(t => !OwnedElsewhere(t) && config.DomainServiceConvention.Matches(t)).ToList();

        MergeStructuralDomainEvents(config, allTypes, OwnedElsewhere, domainEventTypes);

        return new DomainTypeCategories(
            entityTypes,
            aggregateTypes,
            valueObjectTypes,
            domainEventTypes,
            integrationEventTypesAll,
            integrationEventTypes,
            eventHandlerTypes,
            commandHandlerTypes,
            queryHandlerTypes,
            repositoryTypes,
            domainServiceTypes);
    }

    /// <summary>
    /// Adds domain event types derived from <see cref="BoundedContextBuilder.DomainEventConvention"/> structural rules.
    /// </summary>
    private static void MergeStructuralDomainEvents(
        BoundedContextBuilder config,
        List<Type> allTypes,
        Func<Type, bool> ownedElsewhere,
        List<Type> domainEventTypes)
    {
        var rules = config.DomainEventConvention.StructuralRules;
        if (rules.Count == 0)
            return;

        var existing = new HashSet<string>(domainEventTypes.Select(t => t.FullName!).Where(n => n is not null), StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            foreach (var eventType in rule.EnumerateEventTypes(allTypes))
            {
                if (ownedElsewhere(eventType))
                    continue;
                var fullName = eventType.FullName;
                if (fullName is null || !existing.Add(fullName))
                    continue;
                domainEventTypes.Add(eventType);
            }
        }
    }

    public static List<Type> DeduplicateTypesByFullName(IEnumerable<Type> types, Dictionary<Assembly, int> assemblyOrder)
    {
        var list = types as IList<Type> ?? types.ToList();
        var withFullName = list.Where(t => t.FullName is not null).ToList();
        var withoutFullName = list.Where(t => t.FullName is null).Distinct().ToList();

        static int Order(Dictionary<Assembly, int> order, Assembly a) =>
            order.TryGetValue(a, out var i) ? i : int.MaxValue;

        var deduped = withFullName
            .GroupBy(t => t.FullName!, StringComparer.Ordinal)
            .Select(g => g.OrderBy(t => Order(assemblyOrder, t.Assembly)).First())
            .ToList();

        return deduped.Concat(withoutFullName).ToList();
    }

    public static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    public static HashSet<string> BuildKnownDomainTypeSet(DomainTypeCategories categories)
    {
        return new HashSet<string>(
            categories.EntityTypes
                .Concat(categories.AggregateTypes)
                .Concat(categories.ValueObjectTypes)
                .Concat(categories.DomainEventTypes)
                .Concat(categories.IntegrationEventTypesAll)
                .Select(t => t.FullName!)
                .Where(n => n is not null));
    }
}
