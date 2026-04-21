namespace DomainModeling.Discovery;

/// <summary>
/// Types grouped by DDD convention after scanning configured assemblies.
/// </summary>
internal readonly record struct DomainTypeCategories(
    List<Type> EntityTypes,
    List<Type> AggregateTypes,
    List<Type> ValueObjectTypes,
    List<Type> DomainEventTypes,
    List<Type> IntegrationEventTypesAll,
    List<Type> IntegrationEventTypes,
    List<Type> EventHandlerTypes,
    List<Type> CommandHandlerTypes,
    List<Type> QueryHandlerTypes,
    List<Type> RepositoryTypes,
    List<Type> DomainServiceTypes);
