namespace DomainModeling.Discovery;

internal sealed partial class AssemblyScanner
{
    /// <summary>
    /// Adds domain event types derived from <see cref="BoundedContextBuilder.DomainEventConvention"/> structural rules
    /// (e.g. first parameter of <c>Handle</c> on types matching a nested convention).
    /// </summary>
    private void MergeStructuralDomainEvents(
        List<Type> allTypes,
        Func<Type, bool> ownedElsewhere,
        List<Type> domainEventTypes)
    {
        var rules = _config.DomainEventConvention.StructuralRules;
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
}
