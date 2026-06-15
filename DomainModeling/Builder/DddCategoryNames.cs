namespace DomainModeling.Builder;

/// <summary>
/// Category names used when mapping DDD building blocks to the generic type scanner.
/// </summary>
internal static class DddCategoryNames
{
    public const string Entity = "Entity";
    public const string Aggregate = "Aggregate";
    public const string ValueObject = "ValueObject";
    public const string DomainEvent = "DomainEvent";
    public const string IntegrationEvent = "IntegrationEvent";
    public const string EventHandler = "EventHandler";
    public const string CommandHandler = "CommandHandler";
    public const string Command = "Command";
    public const string QueryHandler = "QueryHandler";
    public const string Repository = "Repository";
    public const string DomainService = "DomainService";
}
