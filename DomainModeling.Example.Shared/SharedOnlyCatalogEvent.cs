namespace DomainModeling.Example.Domain;

/// <summary>
/// Domain event type defined only in the shared kernel assembly (regression: must still be detected when emitted
/// from a bounded context that references shared types via <c>WithSharedAssembly</c>).
/// </summary>
public sealed class SharedOnlyCatalogEvent : DomainEvent;
