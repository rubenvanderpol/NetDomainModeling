using System.Text.RegularExpressions;

namespace DomainModeling.Builder;

/// <summary>
/// Fluent builder to define how a particular DDD building block is identified
/// in a target project — by base type, implemented interface, naming convention, or attribute.
/// </summary>
/// <remarks>
/// <para>
/// Each convention call (e.g. <see cref="Implements{T}"/>, <see cref="NameEndsWith"/>)
/// starts a new <b>OR</b> branch: a type matches if it satisfies <b>any</b> branch.
/// </para>
/// <para>
/// Use <see cref="And"/> before the next rule to <b>AND</b> it with the previous branch instead
/// (e.g. <c>NameEndsWith("Command").And().Implements(typeof(IMyCommand))</c>).
/// </para>
/// <para>
/// Optional <see cref="Or"/> clears a pending <see cref="And"/> and documents intent when
/// chaining several alternatives.
/// </para>
/// </remarks>
public sealed class TypeConventionBuilder
{
    private readonly List<List<Func<Type, bool>>> _orBranches = [];
    private bool _mergeNextIntoCurrentBranch;

    /// <summary>
    /// True when at least one convention rule has been configured.
    /// </summary>
    internal bool HasPredicates => _orBranches.Count > 0;

    /// <summary>
    /// AND the next rule with the current branch (the one formed by the immediately preceding rule).
    /// </summary>
    /// <exception cref="InvalidOperationException">No prior rule exists, or <see cref="And"/> was already called without a following rule.</exception>
    public TypeConventionBuilder And()
    {
        if (_mergeNextIntoCurrentBranch)
            throw new InvalidOperationException("A convention rule must follow And() before And() can be used again.");

        if (_orBranches.Count == 0)
            throw new InvalidOperationException("And() must follow a convention rule such as NameEndsWith or Implements.");

        _mergeNextIntoCurrentBranch = true;
        return this;
    }

    /// <summary>
    /// Starts the next rule as a new OR branch. Usually unnecessary because each rule already begins
    /// a new branch; use this to cancel a pending <see cref="And"/> or to clarify intent.
    /// </summary>
    public TypeConventionBuilder Or()
    {
        _mergeNextIntoCurrentBranch = false;
        return this;
    }

    /// <summary>
    /// Match types that inherit (directly or indirectly) from <typeparamref name="T"/>.
    /// </summary>
    public TypeConventionBuilder InheritsFrom<T>()
    {
        var baseType = typeof(T);
        AddPredicate(t => IsAssignableToGenericOrConcrete(t, baseType) && t != baseType);
        return this;
    }

    /// <summary>
    /// Match types that inherit from <paramref name="baseType"/>.
    /// </summary>
    public TypeConventionBuilder InheritsFrom(Type baseType)
    {
        ArgumentNullException.ThrowIfNull(baseType);
        AddPredicate(t => IsAssignableToGenericOrConcrete(t, baseType) && t != baseType);
        return this;
    }

    /// <summary>
    /// Match types that implement <typeparamref name="T"/>.
    /// </summary>
    public TypeConventionBuilder Implements<T>()
    {
        var interfaceType = typeof(T);
        AddPredicate(t => IsAssignableToGenericOrConcrete(t, interfaceType) && t != interfaceType);
        return this;
    }

    /// <summary>
    /// Match types that implement <paramref name="interfaceType"/>.
    /// </summary>
    public TypeConventionBuilder Implements(Type interfaceType)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        AddPredicate(t => IsAssignableToGenericOrConcrete(t, interfaceType) && t != interfaceType);
        return this;
    }

    /// <summary>
    /// Match types whose name ends with <paramref name="suffix"/> (e.g. "Entity", "Event").
    /// </summary>
    public TypeConventionBuilder NameEndsWith(string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);
        AddPredicate(t => t.Name.EndsWith(suffix, StringComparison.Ordinal));
        return this;
    }

    /// <summary>
    /// Match types whose name matches a regex pattern.
    /// </summary>
    public TypeConventionBuilder NameMatches(string regexPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regexPattern);
        var regex = new Regex(regexPattern, RegexOptions.Compiled);
        AddPredicate(t => regex.IsMatch(t.Name));
        return this;
    }

    /// <summary>
    /// Match types decorated with the attribute <typeparamref name="TAttribute"/>.
    /// </summary>
    public TypeConventionBuilder HasAttribute<TAttribute>() where TAttribute : Attribute
    {
        AddPredicate(t => t.GetCustomAttributes(typeof(TAttribute), true).Length > 0);
        return this;
    }

    /// <summary>
    /// Match types decorated with the given attribute type.
    /// </summary>
    public TypeConventionBuilder HasAttribute(Type attributeType)
    {
        ArgumentNullException.ThrowIfNull(attributeType);
        AddPredicate(t => t.GetCustomAttributes(attributeType, true).Length > 0);
        return this;
    }

    /// <summary>
    /// Match types satisfying a custom predicate.
    /// </summary>
    public TypeConventionBuilder Where(Func<Type, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        AddPredicate(predicate);
        return this;
    }

    /// <summary>
    /// Returns true if the type matches any OR branch, where each branch requires all of its predicates (AND).
    /// </summary>
    internal bool Matches(Type type)
    {
        if (_mergeNextIntoCurrentBranch)
            throw new InvalidOperationException("A convention rule must follow the last And().");

        return _orBranches.Count > 0 && _orBranches.Any(branch => branch.All(p => p(type)));
    }

    private void AddPredicate(Func<Type, bool> predicate)
    {
        if (_mergeNextIntoCurrentBranch)
        {
            _orBranches[^1].Add(predicate);
            _mergeNextIntoCurrentBranch = false;
        }
        else
            _orBranches.Add([predicate]);
    }

    /// <summary>
    /// Handles both concrete and open-generic base type / interface matching.
    /// </summary>
    private static bool IsAssignableToGenericOrConcrete(Type candidate, Type target)
    {
        if (target.IsAssignableFrom(candidate))
            return true;

        if (!target.IsGenericTypeDefinition)
            return false;

        // Closed generic interface (e.g. IRepository<Order>) satisfies Implements(typeof(IRepository<>))
        if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == target)
            return true;

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
