using System.Text.Json.Serialization;
using DomainModeling.Graph;

namespace DomainModeling.AspNetCore;

/// <summary>
/// Serialization-friendly DTO for a property on a domain type.
/// Used by the feature editor API instead of <see cref="Graph.PropertyInfo"/>
/// which shadows <see cref="System.Reflection.PropertyInfo"/>.
/// </summary>
internal sealed class PropertyDto
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public bool IsCollection { get; set; }
    public string? ReferenceTypeName { get; set; }

    public Graph.PropertyInfo ToGraphProperty() => new()
    {
        Name = Name,
        TypeName = TypeName,
        IsCollection = IsCollection,
        ReferenceTypeName = ReferenceTypeName,
    };

    public static PropertyDto FromGraphProperty(Graph.PropertyInfo p) => new()
    {
        Name = p.Name,
        TypeName = p.TypeName,
        IsCollection = p.IsCollection,
        ReferenceTypeName = p.ReferenceTypeName,
    };
}

/// <summary>
/// Serialization-friendly DTO for a method parameter.
/// </summary>
internal sealed class MethodParameterDto
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";

    public Graph.MethodParameterInfo ToGraphParameter() => new()
    {
        Name = Name,
        TypeName = TypeName,
    };

    public static MethodParameterDto FromGraphParameter(Graph.MethodParameterInfo p) => new()
    {
        Name = p.Name,
        TypeName = p.TypeName,
    };
}

/// <summary>
/// Serialization-friendly DTO for a method on an aggregate.
/// </summary>
internal sealed class MethodDto
{
    public string Name { get; set; } = "";
    public string ReturnTypeName { get; set; } = "";
    public List<MethodParameterDto> Parameters { get; set; } = [];

    public Graph.MethodInfo ToGraphMethod() => new()
    {
        Name = Name,
        ReturnTypeName = ReturnTypeName,
        Parameters = Parameters.Select(p => p.ToGraphParameter()).ToList(),
    };

    public static MethodDto FromGraphMethod(Graph.MethodInfo m) => new()
    {
        Name = m.Name,
        ReturnTypeName = m.ReturnTypeName,
        Parameters = m.Parameters.Select(MethodParameterDto.FromGraphParameter).ToList(),
    };
}
