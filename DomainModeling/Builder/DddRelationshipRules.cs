using TypeScanning.Configuration;
using TypeScanning.Relationships;

namespace DomainModeling.Builder;

/// <summary>
/// Configures DDD relationship detection rules on a <see cref="ScanContextBuilder"/>.
/// </summary>
internal static class DddRelationshipRules
{
    public static void Apply(ScanContextBuilder builder)
    {
        builder
            .WithRelationship(DddRelationshipNames.Has, r => r
                .FromCategories(
                    DddCategoryNames.Entity,
                    DddCategoryNames.Aggregate,
                    DddCategoryNames.ValueObject)
                .PropertyReference(o => o.ExcludeCollections = true))

            .WithRelationship(DddRelationshipNames.HasMany, r => r
                .FromCategories(
                    DddCategoryNames.Entity,
                    DddCategoryNames.Aggregate,
                    DddCategoryNames.ValueObject)
                .PropertyReference(o => o.CollectionsOnly = true))

            .WithRelationship(DddRelationshipNames.Contains, r => r
                .FromCategories(DddCategoryNames.Aggregate)
                .PropertyReferencesCategory(DddCategoryNames.Entity))

            .WithRelationship(DddRelationshipNames.ReferencesById, r => r
                .FromCategories(DddCategoryNames.Entity, DddCategoryNames.Aggregate)
                .IdPropertyReference(o => o.PreferTargetCategories =
                [
                    DddCategoryNames.Aggregate,
                    DddCategoryNames.Entity
                ]))

            .WithRelationship(DddRelationshipNames.Emits, r => r
                .FromCategories(DddCategoryNames.Entity, DddCategoryNames.Aggregate)
                .IlInstantiatesTypesInCategories([DddCategoryNames.DomainEvent], "emits via {Method}()"))

            .WithRelationship(DddRelationshipNames.Publishes, r => r
                .FromCategories(DddCategoryNames.EventHandler)
                .IlInstantiatesTypesInCategories([DddCategoryNames.IntegrationEvent]))

            .WithRelationship(DddRelationshipNames.Handles, r => r
                .FromCategories(
                    DddCategoryNames.EventHandler,
                    DddCategoryNames.CommandHandler,
                    DddCategoryNames.QueryHandler)
                .InterfaceGenericArguments())

            .WithRelationship(DddRelationshipNames.References, r => r
                .FromCategories(DddCategoryNames.CommandHandler)
                .IlCallsTypesInCategories([DddCategoryNames.Aggregate], "invokes {Method}()"))

            .WithRelationship(DddRelationshipNames.Handles, r => r
                .FromCategories(DddCategoryNames.EventHandler)
                .IlInstantiatesTypesInCategories([DddCategoryNames.Command], "creates command"))

            .WithRelationship(DddRelationshipNames.References, r => r
                .FromCategories(DddCategoryNames.EventHandler)
                .IlCallsTypesInCategories([DddCategoryNames.CommandHandler], "invokes {Method}()"))

            .WithRelationship(DddRelationshipNames.Manages, r => r
                .FromCategories(DddCategoryNames.Repository)
                .InterfaceGenericArguments(o =>
                {
                    o.TargetCategories = [DddCategoryNames.Aggregate];
                    o.TargetMustBeKnown = true;
                }));
    }
}
