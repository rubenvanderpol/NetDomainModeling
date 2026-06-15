namespace DomainModeling.Builder;

/// <summary>
/// Consumer-defined relationship type names used when configuring the generic scanner for DDD.
/// </summary>
internal static class DddRelationshipNames
{
    public const string Contains = "Contains";
    public const string References = "References";
    public const string ReferencesById = "ReferencesById";
    public const string Emits = "Emits";
    public const string Handles = "Handles";
    public const string Manages = "Manages";
    public const string Publishes = "Publishes";
    public const string Has = "Has";
    public const string HasMany = "HasMany";
}
