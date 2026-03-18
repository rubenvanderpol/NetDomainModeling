using System.Text.RegularExpressions;

namespace DomainModeling.Builder;

/// <summary>
/// Fluent builder to define how a particular DDD building block is identified
/// in a target project — by base type, implemented interface, naming convention, or attribute.
/// Multiple rules can be combined; a type matches if it satisfies <b>any</b> of them.
/// </summary>
public sealed class TypeConventionBuilder
{
    internal List<Func<Type, bool>> Predicates { get; } = [];

    /// <summary>
    /// Match types that inherit (directly or indirectly) from <typeparamref name="T"/>.
    /// </summary>
    public TypeConventionBuilder InheritsFrom<T>()
    {
        var baseType = typeof(T);
        Predicates.Add(t => IsAssignableToGenericOrConcrete(t, baseType) && t != baseType);
        return this;
    }

    /// <summary>
    /// Match types that inherit from <paramref name="baseType"/>.
    /// </summary>
    public TypeConventionBuilder InheritsFrom(Type baseType)
    {
        ArgumentNullException.ThrowIfNull(baseType);
        Predicates.Add(t => IsAssignableToGenericOrConcrete(t, baseType) && t != baseType);
        return this;
    }

    /// <summary>
    /// Match types that implement <typeparamref name="T"/>.
    /// </summary>
    public TypeConventionBuilder Implements<T>()
    {
        var interfaceType = typeof(T);
        Predicates.Add(t => IsAssignableToGenericOrConcrete(t, interfaceType) && t != interfaceType);
        return this;
    }

    /// <summary>
    /// Match types that implement <paramref name="interfaceType"/>.
    /// </summary>
    public TypeConventionBuilder Implements(Type interfaceType)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        Predicates.Add(t => IsAssignableToGenericOrConcrete(t, interfaceType) && t != interfaceType);
        return this;
    }

    /// <summary>
    /// Match types whose name ends with <paramref name="suffix"/> (e.g. "Entity", "Event").
    /// </summary>
    public TypeConventionBuilder NameEndsWith(string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);
        Predicates.Add(t => t.Name.EndsWith(suffix, StringComparison.Ordinal));
        return this;
    }

    /// <summary>
    /// Match types whose name matches a regex pattern.
    /// </summary>
    public TypeConventionBuilder NameMatches(string regexPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regexPattern);
        var regex = new Regex(regexPattern, RegexOptions.Compiled);
        Predicates.Add(t => regex.IsMatch(t.Name));
        return this;
    }

    /// <summary>
    /// Match types decorated with the attribute <typeparamref name="TAttribute"/>.
    /// </summary>
    public TypeConventionBuilder HasAttribute<TAttribute>() where TAttribute : Attribute
    {
        Predicates.Add(t => t.GetCustomAttributes(typeof(TAttribute), true).Length > 0);
        return this;
    }

    /// <summary>
    /// Match types decorated with the given attribute type.
    /// </summary>
    public TypeConventionBuilder HasAttribute(Type attributeType)
    {
        ArgumentNullException.ThrowIfNull(attributeType);
        Predicates.Add(t => t.GetCustomAttributes(attributeType, true).Length > 0);
        return this;
    }

    /// <summary>
    /// Match types satisfying a custom predicate.
    /// </summary>
    public TypeConventionBuilder Where(Func<Type, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        Predicates.Add(predicate);
        return this;
    }

    /// <summary>
    /// Returns true if the given type matches <b>any</b> of the configured predicates.
    /// </summary>
    internal bool Matches(Type type) => Predicates.Count > 0 && Predicates.Any(p => p(type));

    /// <summary>
    /// Handles both concrete and open-generic base type / interface matching.
    /// </summary>
    private static bool IsAssignableToGenericOrConcrete(Type candidate, Type target)
    {
        if (target.IsAssignableFrom(candidate))
            return true;

        if (!target.IsGenericTypeDefinition)
            return false;

        // Check open-generic base classes
        var current = candidate.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == target)
                return true;
            current = current.BaseType;
        }

        // Check open-generic interfaces
        return candidate.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == target);
    }
}
