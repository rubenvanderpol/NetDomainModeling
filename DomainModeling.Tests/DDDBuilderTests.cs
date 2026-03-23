using System.Text.Json;
using DomainModeling.Builder;
using DomainModeling.Graph;
using DomainModeling.Tests.SampleDomain;
using FluentAssertions;
using Xunit;

namespace DomainModeling.Tests;

public class DDDBuilderTests
{
    private static DomainGraph BuildSampleGraph()
    {
        var assembly = typeof(Order).Assembly;

        return DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .WithApplicationAssembly(assembly)
                .WithInfrastructureAssembly(assembly)
                .Entities(e => e.InheritsFrom<BaseEntity>())
                .Aggregates(a => a.InheritsFrom<BaseAggregateRoot>())
                .ValueObjects(v => v.InheritsFrom<BaseValueObject>())
                .DomainEvents(e => e.InheritsFrom<BaseDomainEvent>())
                .IntegrationEvents(e => e.InheritsFrom<BaseIntegrationEvent>())
                .EventHandlers(h => h
                    .Implements(typeof(IEventHandler<>))
                    .Implements(typeof(IIntegrationEventHandler<>)))
                .CommandHandlers(h => h.Implements(typeof(ICommandHandler<>)))
                .Commands(c => c.NameEndsWith("Command"))
                .QueryHandlers(h => h.Implements(typeof(IQueryHandler<,>)))
                .Repositories(r => r.Implements(typeof(IRepository<>)))
            )
            .Build();
    }

    [Fact]
    public void Build_DiscoversAggregates()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        ctx.Aggregates.Should().Contain(a => a.Name == "Order");
        ctx.Aggregates.Should().Contain(a => a.Name == "Customer");
    }

    [Fact]
    public void Build_DiscoversEntities()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        // OrderLine is an entity (inherits BaseEntity) but not an aggregate
        ctx.Entities.Should().Contain(e => e.Name == "OrderLine");
    }

    [Fact]
    public void Build_DiscoversValueObjects()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        ctx.ValueObjects.Should().Contain(v => v.Name == "Address");
        ctx.ValueObjects.Should().Contain(v => v.Name == "Money");
    }

    [Fact]
    public void Build_DiscoversDomainEvents()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        ctx.DomainEvents.Should().HaveCount(5);
        ctx.DomainEvents.Select(e => e.Name).Should()
            .Contain(["OrderPlacedEvent", "OrderShippedEvent", "CustomerCreatedEvent", "InvoiceCreatedEvent"]);
        ctx.DomainEvents.Should().Contain(e => e.Name.StartsWith("EntityDeletedEvent", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_DiscoversHandlers()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        ctx.EventHandlers.Should().HaveCount(4);
        ctx.CommandHandlers.Should().ContainSingle(h => h.Name == "PlaceOrderCommandHandler");
        ctx.QueryHandlers.Should().ContainSingle(h => h.Name == "GetOrderQueryHandler");
    }

    [Fact]
    public void Build_SurfacesCommandHandlerTargets_ForHandlesRelationships()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        ctx.CommandHandlerTargets.Should().Contain(t => t.Name == "PlaceOrderCommand");
        var target = ctx.CommandHandlerTargets.Single(t => t.Name == "PlaceOrderCommand");
        target.HandledBy.Should().Contain(h => h.Contains("PlaceOrderCommandHandler"));

        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Handles &&
            r.SourceType.Contains("PlaceOrderCommandHandler") &&
            r.TargetType.Contains("PlaceOrderCommand"));
    }

    [Fact]
    public void Build_CommandsConvention_SurfacesCommandTypesWithoutHandlers()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        ctx.CommandHandlerTargets.Should().Contain(t => t.Name == "UnassignedCommand");
        ctx.CommandHandlerTargets.Single(t => t.Name == "UnassignedCommand")
            .HandledBy.Should().BeEmpty();
    }

    [Fact]
    public void Build_DiscoversRepositories()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        ctx.Repositories.Should().HaveCount(2);
        ctx.Repositories.Should().Contain(r => r.Name == "OrderRepository");
        ctx.Repositories.Should().Contain(r => r.Name == "CustomerRepository");
    }

    [Fact]
    public void Build_DetectsAggregateChildEntities()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        var order = ctx.Aggregates.Single(a => a.Name == "Order");
        order.ChildEntities.Should().Contain(c => c.Contains("OrderLine"));
    }

    [Fact]
    public void Build_DetectsEmittedEvents()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        var order = ctx.Aggregates.Single(a => a.Name == "Order");
        order.EmittedEvents.Should().Contain(e => e.Contains("OrderPlacedEvent"));
        order.EmittedEvents.Should().Contain(e => e.Contains("OrderShippedEvent"));

        var customer = ctx.Aggregates.Single(a => a.Name == "Customer");
        customer.EmittedEvents.Should().Contain(e => e.Contains("CustomerCreatedEvent"));
    }

    [Fact]
    public void Build_DetectsEventEmissionMethods()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        var order = ctx.Aggregates.Single(a => a.Name == "Order");
        order.EventEmissions.Should().Contain(e =>
            e.EventType.Contains("OrderPlacedEvent") &&
            e.MethodName == "Place");
        order.EventEmissions.Should().Contain(e =>
            e.EventType.Contains("OrderShippedEvent") &&
            e.MethodName == "Ship");

        var invoice = ctx.Aggregates.Single(a => a.Name == "Invoice");
        invoice.EventEmissions.Should().Contain(e =>
            e.EventType.Contains("InvoiceCreatedEvent") &&
            e.MethodName == "Create");
    }

    [Fact]
    public void Build_EmitsRelationships_IncludeMethodNameInLabel()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Emits &&
            r.SourceType.EndsWith(".Order") &&
            r.TargetType.EndsWith(".OrderPlacedEvent") &&
            r.Label == "emits via Place()");

        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Emits &&
            r.SourceType.EndsWith(".Order") &&
            r.TargetType.EndsWith(".OrderShippedEvent") &&
            r.Label == "emits via Ship()");
    }

    [Fact]
    public void Build_CreatesRelationships()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        // Should have Emits relationships
        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Emits &&
            r.SourceType.Contains("Order") &&
            r.TargetType.Contains("OrderPlacedEvent"));

        // Should have Handles relationships
        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Handles &&
            r.TargetType.Contains("OrderPlacedEvent"));

        // Should have Contains relationships (Order → OrderLine)
        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Contains &&
            r.SourceType.Contains("Order") &&
            r.TargetType.Contains("OrderLine"));
    }

    [Fact]
    public void Build_CreatesHasRelationships_ForSinglePropertyReferences()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        // Order.ShippingAddress → Address (single value object)
        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Has &&
            r.SourceType.Contains("Order") &&
            r.TargetType.Contains("Address") &&
            r.Label == "ShippingAddress");

        // Customer.BillingAddress → Address (single value object)
        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Has &&
            r.SourceType.Contains("Customer") &&
            r.TargetType.Contains("Address") &&
            r.Label == "BillingAddress");

        // OrderLine.Price → Money (single value object)
        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Has &&
            r.SourceType.Contains("OrderLine") &&
            r.TargetType.Contains("Money") &&
            r.Label == "Price");
    }

    [Fact]
    public void Build_CreatesHasManyRelationships_ForCollectionPropertyReferences()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        // Order.Lines → OrderLine (collection of entities)
        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.HasMany &&
            r.SourceType.Contains("Order") &&
            r.TargetType.Contains("OrderLine") &&
            r.Label == "Lines");
    }

    [Fact]
    public void Build_DiscoversSubTypes_ForCustomPropertyTypes()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        // ContactInfo is not a registered domain type but is used on Customer
        ctx.SubTypes.Should().Contain(s => s.Name == "ContactInfo");

        var contactInfo = ctx.SubTypes.Single(s => s.Name == "ContactInfo");
        contactInfo.Properties.Should().Contain(p => p.Name == "Phone");
        contactInfo.Properties.Should().Contain(p => p.Name == "Fax");
    }

    [Fact]
    public void Build_CreatesHasRelationship_ForSubTypeProperties()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        // Customer.Contact → ContactInfo (sub-type, single)
        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Has &&
            r.SourceType.Contains("Customer") &&
            r.TargetType.Contains("ContactInfo") &&
            r.Label == "Contact");
    }

    [Fact]
    public void Build_EventNodesHaveCrossReferences()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        var placedEvent = ctx.DomainEvents.Single(e => e.Name == "OrderPlacedEvent");
        placedEvent.EmittedBy.Should().Contain(e => e.Contains("Order"));
        placedEvent.HandledBy.Should().Contain(h => h.Contains("OrderPlacedHandler"));
    }

    [Fact]
    public void Build_GenericDomainEvent_HandlerLinksToOpenGenericEventNode()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        var genericEvent = ctx.DomainEvents.Single(e => e.Name.StartsWith("EntityDeletedEvent", StringComparison.Ordinal));
        genericEvent.HandledBy.Should().Contain(h => h.Contains("OrderDeletedEventHandler"));

        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Handles &&
            r.SourceType.Contains("OrderDeletedEventHandler", StringComparison.Ordinal) &&
            r.TargetType == genericEvent.FullName);
    }

    [Fact]
    public void Build_GenericDomainEvent_EmissionUsesOpenGenericEventKey()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        var genericEvent = ctx.DomainEvents.Single(e => e.Name.StartsWith("EntityDeletedEvent", StringComparison.Ordinal));
        genericEvent.EmittedBy.Should().Contain(e => e.Contains("Order"));

        var order = ctx.Aggregates.Single(a => a.Name == "Order");
        order.EmittedEvents.Should().Contain(genericEvent.FullName);
    }

    [Fact]
    public void Build_RepositoryManagesAggregate()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        var orderRepo = ctx.Repositories.Single(r => r.Name == "OrderRepository");
        orderRepo.ManagesAggregate.Should().Contain("Order");

        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Manages &&
            r.SourceType.Contains("OrderRepository") &&
            r.TargetType.Contains("Order"));
    }

    [Fact]
    public void ToJson_ProducesValidJson()
    {
        var graph = BuildSampleGraph();
        var json = graph.ToJson();

        json.Should().NotBeNullOrWhiteSpace();

        // Should round-trip through the JSON serializer
        var deserialized = JsonSerializer.Deserialize<JsonDocument>(json);
        deserialized.Should().NotBeNull();

        // Should contain key structural elements
        json.Should().Contain("\"boundedContexts\"");
        json.Should().Contain("\"aggregates\"");
        json.Should().Contain("\"relationships\"");
        json.Should().Contain("\"kind\": \"Emits\"");
    }

    [Fact]
    public void Build_DetectsEmittedEvents_FromStaticFactoryMethods()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        var invoice = ctx.Aggregates.Single(a => a.Name == "Invoice");
        invoice.EmittedEvents.Should().Contain(e => e.Contains("InvoiceCreatedEvent"));

        var invoiceCreated = ctx.DomainEvents.Single(e => e.Name == "InvoiceCreatedEvent");
        invoiceCreated.EmittedBy.Should().Contain(e => e.Contains("Invoice"));
    }

    [Fact]
    public void Build_NameBasedHandler_DetectsHandledDomainEventAndPublishedIntegrationEvent()
    {
        var assembly = typeof(Order).Assembly;

        // Use name-based conventions (same as DataHub)
        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .WithApplicationAssembly(assembly)
                .Entities(e => e.InheritsFrom<BaseEntity>())
                .Aggregates(a => a.InheritsFrom<BaseAggregateRoot>())
                .DomainEvents(e => e.InheritsFrom<BaseDomainEvent>())
                .IntegrationEvents(e => e.InheritsFrom<BaseIntegrationEvent>())
                .EventHandlers(h => h.NameEndsWith("EventHandler"))
            )
            .Build();

        var ctx = graph.BoundedContexts.Single();

        // Handler should be discovered
        ctx.EventHandlers.Should().Contain(h => h.Name == "PublishCustomerRegisteredWhenCreatedEventHandler");

        // Handler should handle the domain event (via method parameter scan)
        var handler = ctx.EventHandlers.Single(h => h.Name == "PublishCustomerRegisteredWhenCreatedEventHandler");
        handler.Handles.Should().Contain(h => h.Contains("CustomerCreatedEvent"));

        // Handler should publish the integration event (via IL scanning)
        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Publishes &&
            r.SourceType.Contains("PublishCustomerRegisteredWhenCreatedEventHandler") &&
            r.TargetType.Contains("CustomerRegisteredIntegrationEvent"));
    }

    [Fact]
    public void Build_AsyncHandler_DetectsPublishedIntegrationEvent()
    {
        var assembly = typeof(Order).Assembly;

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .WithApplicationAssembly(assembly)
                .Entities(e => e.InheritsFrom<BaseEntity>())
                .Aggregates(a => a.InheritsFrom<BaseAggregateRoot>())
                .DomainEvents(e => e.InheritsFrom<BaseDomainEvent>())
                .IntegrationEvents(e => e.InheritsFrom<BaseIntegrationEvent>())
                .EventHandlers(h => h.NameEndsWith("EventHandler"))
            )
            .Build();

        var ctx = graph.BoundedContexts.Single();

        // Async handler should also be discovered and should publish integration event
        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Publishes &&
            r.SourceType.Contains("PublishCustomerRegisteredWhenCreatedAsyncEventHandler") &&
            r.TargetType.Contains("CustomerRegisteredIntegrationEvent"));
    }

    [Fact]
    public void Build_SupportsNameEndsWith_Convention()
    {
        var assembly = typeof(Order).Assembly;

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .DomainEvents(e => e.NameEndsWith("Event"))
            )
            .Build();

        var ctx = graph.BoundedContexts.Single();
        ctx.DomainEvents.Should().Contain(e => e.Name == "OrderPlacedEvent");
    }

    [Fact]
    public void Build_SupportsMultipleConventions()
    {
        var assembly = typeof(Order).Assembly;

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .DomainEvents(e => e
                    .InheritsFrom<BaseDomainEvent>()
                    .NameEndsWith("Event") // separate OR branches; any branch qualifies
                )
            )
            .Build();

        var ctx = graph.BoundedContexts.Single();
        ctx.DomainEvents.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Build_EventHandlers_AndConvention_RequiresAllPredicatesInBranch()
    {
        var assembly = typeof(Order).Assembly;

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .EventHandlers(h => h
                    .Implements(typeof(IEventHandler<>))
                    .And()
                    .NameEndsWith("Handler")))
            .Build();

        var ctx = graph.BoundedContexts.Single();
        ctx.EventHandlers.Should().HaveCount(3);
        ctx.EventHandlers.Should().Contain(h => h.Name == "OrderPlacedHandler");
        ctx.EventHandlers.Should().Contain(h => h.Name == "SendShipmentNotificationHandler");
        ctx.EventHandlers.Should().Contain(h => h.Name == "OrderDeletedEventHandler");
        ctx.EventHandlers.Should().NotContain(h => h.Name == "OrderPlacedIntegrationHandler");
        ctx.EventHandlers.Should().NotContain(h => h.Name.Contains("PublishCustomerRegistered"));
    }

    [Fact]
    public void Build_EventHandlers_OrBranches_WithAndGroup_MatchesUnion()
    {
        var assembly = typeof(Order).Assembly;

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .EventHandlers(h => h
                    .Implements(typeof(IEventHandler<>))
                    .And()
                    .NameEndsWith("Handler")
                    .Implements(typeof(IIntegrationEventHandler<>))))
            .Build();

        var ctx = graph.BoundedContexts.Single();
        ctx.EventHandlers.Should().HaveCount(4);
    }

    [Fact]
    public void TypeConventionBuilder_And_WithoutPriorRule_Throws()
    {
        var assembly = typeof(Order).Assembly;

        var act = () => DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .EventHandlers(h => h.And()))
            .Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*follow a convention rule*");
    }

    [Fact]
    public void TypeConventionBuilder_DanglingAnd_ThrowsOnMatch()
    {
        var assembly = typeof(Order).Assembly;

        var act = () => DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .EventHandlers(h => h
                    .Implements(typeof(IEventHandler<>))
                    .And()))
            .Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*follow the last And()*");
    }

    [Fact]
    public void TypeConventionBuilder_DoubleAnd_Throws()
    {
        var assembly = typeof(Order).Assembly;

        var act = () => DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .EventHandlers(h => h
                    .Implements(typeof(IEventHandler<>))
                    .And()
                    .And()
                    .NameEndsWith("Handler")))
            .Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*before And()*");
    }

    [Fact]
    public void Build_ThrowsWhenNoBoundedContexts()
    {
        var act = () => DDDBuilder.Create().Build();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void WithDomainAssembly_DefinesContextWithoutWithBoundedContext()
    {
        var assembly = typeof(Order).Assembly;

        var graph = DDDBuilder.Create()
            .WithDomainAssembly("Sales", assembly, ctx => ctx
                .Aggregates(a => a.InheritsFrom<BaseAggregateRoot>()))
            .Build();

        graph.BoundedContexts.Should().ContainSingle(c => c.Name == "Sales");
        graph.BoundedContexts.Single().Aggregates.Should().Contain(a => a.Name == "Order");
    }

    [Fact]
    public void WithDomainAssemblies_DefinesMultipleContextsAtOnce()
    {
        var assembly = typeof(Order).Assembly;

        var graph = DDDBuilder.Create()
            .WithDomainAssemblies(
                ctx => ctx.Aggregates(a => a.InheritsFrom<BaseAggregateRoot>()),
                ("Sales", assembly),
                ("Catalog", assembly))
            .Build();

        graph.BoundedContexts.Should().HaveCount(2);
        graph.BoundedContexts.Select(c => c.Name).Should().Contain(["Sales", "Catalog"]);
        graph.BoundedContexts.Should().OnlyContain(c => c.Aggregates.Any(a => a.Name == "Order"));
    }

    [Fact]
    public void ConfigureBoundedContexts_AppliesSharedConfigurationToAllContexts()
    {
        var assembly = typeof(Order).Assembly;

        var graph = DDDBuilder.Create()
            .WithDomainAssembly("Sales", assembly)
            .WithDomainAssembly("Catalog", assembly)
            .ConfigureBoundedContexts(ctx => ctx
                .Aggregates(a => a.InheritsFrom<BaseAggregateRoot>()))
            .Build();

        graph.BoundedContexts.Should().HaveCount(2);
        graph.BoundedContexts.Select(c => c.Name).Should().Contain(["Sales", "Catalog"]);
        graph.BoundedContexts.Should().OnlyContain(c => c.Aggregates.Any(a => a.Name == "Order"));
    }

    // ─── XML Documentation ──────────────────────────────────────────

    private static string GetSampleXmlDocPath()
    {
        // The XML doc file lives next to the test assembly in the SampleDomain folder at compile time,
        // but we copied it into the project. Resolve relative to test assembly location.
        var testDir = Path.GetDirectoryName(typeof(DDDBuilderTests).Assembly.Location)!;
        // During build it gets copied to output if marked as content/copy. Fallback to project source path.
        var candidate = Path.Combine(testDir, "SampleDomain", "SampleTypes.xml");
        if (File.Exists(candidate))
            return candidate;

        // Fallback: walk up to find it in the source tree
        var dir = new DirectoryInfo(testDir);
        while (dir is not null)
        {
            var check = Path.Combine(dir.FullName, "DomainModeling.Tests", "SampleDomain", "SampleTypes.xml");
            if (File.Exists(check))
                return check;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SampleTypes.xml");
    }

    [Fact]
    public void WithDocumentation_FromFile_PopulatesAggregateDescriptions()
    {
        var assembly = typeof(Order).Assembly;
        var xmlPath = GetSampleXmlDocPath();

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .Aggregates(a => a.InheritsFrom<BaseAggregateRoot>())
                .WithDocumentation(d => d.FromFile(xmlPath))
            )
            .Build();

        var ctx = graph.BoundedContexts.Single();
        var order = ctx.Aggregates.Single(a => a.Name == "Order");
        var customer = ctx.Aggregates.Single(a => a.Name == "Customer");

        order.Description.Should().Be("Represents a customer order in the sales domain.");
        customer.Description.Should().Be("A customer who can place orders.");
    }

    [Fact]
    public void WithDocumentation_FromFile_PopulatesEntityDescriptions()
    {
        var assembly = typeof(Order).Assembly;
        var xmlPath = GetSampleXmlDocPath();

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .Entities(e => e.InheritsFrom<BaseEntity>())
                .WithDocumentation(d => d.FromFile(xmlPath))
            )
            .Build();

        var ctx = graph.BoundedContexts.Single();
        var orderLine = ctx.Entities.Single(e => e.Name == "OrderLine");

        orderLine.Description.Should().Be("A single line item within an order.");
    }

    [Fact]
    public void WithDocumentation_FromFile_PopulatesValueObjectDescriptions()
    {
        var assembly = typeof(Order).Assembly;
        var xmlPath = GetSampleXmlDocPath();

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .ValueObjects(v => v.InheritsFrom<BaseValueObject>())
                .WithDocumentation(d => d.FromFile(xmlPath))
            )
            .Build();

        var ctx = graph.BoundedContexts.Single();
        var address = ctx.ValueObjects.Single(v => v.Name == "Address");
        var money = ctx.ValueObjects.Single(v => v.Name == "Money");

        address.Description.Should().Be("A physical mailing address.");
        money.Description.Should().Be("Represents a monetary amount with currency.");
    }

    [Fact]
    public void WithDocumentation_FromFile_PopulatesDomainEventDescriptions()
    {
        var assembly = typeof(Order).Assembly;
        var xmlPath = GetSampleXmlDocPath();

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .DomainEvents(e => e.InheritsFrom<BaseDomainEvent>())
                .WithDocumentation(d => d.FromFile(xmlPath))
            )
            .Build();

        var ctx = graph.BoundedContexts.Single();
        var orderPlaced = ctx.DomainEvents.Single(e => e.Name == "OrderPlacedEvent");
        var orderShipped = ctx.DomainEvents.Single(e => e.Name == "OrderShippedEvent");
        var customerCreated = ctx.DomainEvents.Single(e => e.Name == "CustomerCreatedEvent");

        orderPlaced.Description.Should().Be("Raised when an order is placed by a customer.");
        orderShipped.Description.Should().Be("Raised when an order has shipped.");
        customerCreated.Description.Should().Be("Raised when a new customer registers.");
    }

    [Fact]
    public void WithDocumentation_FromFile_PopulatesHandlerDescriptions()
    {
        var assembly = typeof(Order).Assembly;
        var xmlPath = GetSampleXmlDocPath();

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .WithApplicationAssembly(assembly)
                .EventHandlers(h => h.Implements(typeof(IEventHandler<>)))
                .WithDocumentation(d => d.FromFile(xmlPath))
            )
            .Build();

        var ctx = graph.BoundedContexts.Single();
        var handler = ctx.EventHandlers.Single(h => h.Name == "OrderPlacedHandler");

        handler.Description.Should().Be("Processes order-placed events for downstream fulfillment.");
    }

    [Fact]
    public void WithDocumentation_FromFile_PopulatesRepositoryDescriptions()
    {
        var assembly = typeof(Order).Assembly;
        var xmlPath = GetSampleXmlDocPath();

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .WithInfrastructureAssembly(assembly)
                .Aggregates(a => a.InheritsFrom<BaseAggregateRoot>())
                .Repositories(r => r.Implements(typeof(IRepository<>)))
                .WithDocumentation(d => d.FromFile(xmlPath))
            )
            .Build();

        var ctx = graph.BoundedContexts.Single();
        var repo = ctx.Repositories.Single(r => r.Name == "OrderRepository");

        repo.Description.Should().Be("Persists and retrieves orders.");
    }

    [Fact]
    public void WithDocumentation_NotConfigured_DescriptionsAreNull()
    {
        var assembly = typeof(Order).Assembly;

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .Aggregates(a => a.InheritsFrom<BaseAggregateRoot>())
            )
            .Build();

        var ctx = graph.BoundedContexts.Single();
        ctx.Aggregates.Should().OnlyContain(a => a.Description == null);
    }

    [Fact]
    public void WithDocumentation_DescriptionAppearsInJsonOutput()
    {
        var assembly = typeof(Order).Assembly;
        var xmlPath = GetSampleXmlDocPath();

        var graph = DDDBuilder.Create()
            .WithBoundedContext("Sales", ctx => ctx
                .WithDomainAssembly(assembly)
                .Aggregates(a => a.InheritsFrom<BaseAggregateRoot>())
                .WithDocumentation(d => d.FromFile(xmlPath))
            )
            .Build();

        var json = graph.ToJson();

        json.Should().Contain("\"description\"");
        json.Should().Contain("Represents a customer order in the sales domain.");
    }

    // ─── Integration Events ─────────────────────────────────────────

    [Fact]
    public void Build_DiscoversIntegrationEvents()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        ctx.IntegrationEvents.Should().ContainSingle(e => e.Name == "OrderPlacedIntegrationEvent");
    }

    [Fact]
    public void Build_DetectsPublishedIntegrationEvents()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        // OrderPlacedHandler creates an OrderPlacedIntegrationEvent
        ctx.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Publishes &&
            r.SourceType.Contains("OrderPlacedHandler") &&
            r.TargetType.Contains("OrderPlacedIntegrationEvent"));
    }

    [Fact]
    public void Build_IntegrationEventHasEmittedByFromHandler()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        var integrationEvent = ctx.IntegrationEvents.Single(e => e.Name == "OrderPlacedIntegrationEvent");
        integrationEvent.EmittedBy.Should().Contain(e => e.Contains("OrderPlacedHandler"));
    }

    [Fact]
    public void Build_IntegrationEventHandledByIsDiscovered()
    {
        var graph = BuildSampleGraph();
        var ctx = graph.BoundedContexts.Single();

        var integrationEvent = ctx.IntegrationEvents.Single(e => e.Name == "OrderPlacedIntegrationEvent");
        integrationEvent.HandledBy.Should().Contain(h => h.Contains("OrderPlacedIntegrationHandler"));
    }

    [Fact]
    public void Build_CrossContext_IntegrationEventReferencesAreMerged()
    {
        var assembly = typeof(Order).Assembly;

        // Context A discovers the publisher (OrderPlacedHandler → publishes integration event)
        // Context B also scans the same assembly and discovers the consumer (OrderPlacedIntegrationHandler)
        // Cross-referencing should merge EmittedBy/HandledBy across both contexts
        var graph = DDDBuilder.Create()
            .WithBoundedContext("ContextA", ctx => ctx
                .WithDomainAssembly(assembly)
                .Aggregates(a => a.InheritsFrom<BaseAggregateRoot>())
                .DomainEvents(e => e.InheritsFrom<BaseDomainEvent>())
                .IntegrationEvents(e => e.InheritsFrom<BaseIntegrationEvent>())
                .EventHandlers(h => h.Implements(typeof(IEventHandler<>)))
            )
            .WithBoundedContext("ContextB", ctx => ctx
                .WithDomainAssembly(assembly)
                .IntegrationEvents(e => e.InheritsFrom<BaseIntegrationEvent>())
                .EventHandlers(h => h.Implements(typeof(IIntegrationEventHandler<>)))
            )
            .Build();

        var ctxA = graph.BoundedContexts.Single(c => c.Name == "ContextA");
        var ctxB = graph.BoundedContexts.Single(c => c.Name == "ContextB");

        // Both contexts should have the integration event
        var evtA = ctxA.IntegrationEvents.Single(e => e.Name == "OrderPlacedIntegrationEvent");
        var evtB = ctxB.IntegrationEvents.Single(e => e.Name == "OrderPlacedIntegrationEvent");

        // Both should know who publishes AND who handles (merged from both contexts)
        evtA.EmittedBy.Should().Contain(e => e.Contains("OrderPlacedHandler"));
        evtA.HandledBy.Should().Contain(h => h.Contains("OrderPlacedIntegrationHandler"));

        evtB.EmittedBy.Should().Contain(e => e.Contains("OrderPlacedHandler"));
        evtB.HandledBy.Should().Contain(h => h.Contains("OrderPlacedIntegrationHandler"));
    }
}
