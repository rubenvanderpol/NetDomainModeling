namespace DomainModeling.Builder;

/// <summary>
/// Begins a structural rule: match types (e.g. event handlers), then walk to a parameter type.
/// </summary>
public sealed class TypeStructuralSelector
{
    private readonly TypeConventionBuilder _owner;
    private readonly TypeConventionBuilder _rootMatcher;

    internal TypeStructuralSelector(TypeConventionBuilder owner, TypeConventionBuilder rootMatcher)
    {
        _owner = owner;
        _rootMatcher = rootMatcher;
    }

    /// <summary>
    /// Restrict to types that declare a public instance or static method with this name (first overload with enough parameters is used).
    /// </summary>
    public MethodParameterSelector HasMethod(string methodName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        return new MethodParameterSelector(_owner, _rootMatcher, methodName);
    }
}

/// <summary>
/// Selects which method parameter supplies the domain event type.
/// </summary>
public sealed class MethodParameterSelector
{
    private readonly TypeConventionBuilder _owner;
    private readonly TypeConventionBuilder _rootMatcher;
    private readonly string _methodName;

    internal MethodParameterSelector(TypeConventionBuilder owner, TypeConventionBuilder rootMatcher, string methodName)
    {
        _owner = owner;
        _rootMatcher = rootMatcher;
        _methodName = methodName;
    }

    /// <summary>
    /// Select from the method's formal parameters.
    /// </summary>
    public ParameterPositionSelector Parameters() => new(_owner, _rootMatcher, _methodName);
}

/// <summary>
/// Chooses a parameter index, then completes the rule with <see cref="TypeExtractor.Type"/>.
/// </summary>
public sealed class ParameterPositionSelector
{
    private readonly TypeConventionBuilder _owner;
    private readonly TypeConventionBuilder _rootMatcher;
    private readonly string _methodName;

    internal ParameterPositionSelector(TypeConventionBuilder owner, TypeConventionBuilder rootMatcher, string methodName)
    {
        _owner = owner;
        _rootMatcher = rootMatcher;
        _methodName = methodName;
    }

    /// <summary>Uses the first parameter (index 0).</summary>
    public TypeExtractor First() => new(_owner, _rootMatcher, _methodName, 0);

    /// <summary>Uses the parameter at the given zero-based index.</summary>
    public TypeExtractor At(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        return new TypeExtractor(_owner, _rootMatcher, _methodName, index);
    }
}

/// <summary>
/// Completes a structural extraction by registering the parameter's type as a candidate domain event type.
/// </summary>
public sealed class TypeExtractor
{
    private readonly TypeConventionBuilder _owner;
    private readonly TypeConventionBuilder _rootMatcher;
    private readonly string _methodName;
    private readonly int _parameterIndex;

    internal TypeExtractor(
        TypeConventionBuilder owner,
        TypeConventionBuilder rootMatcher,
        string methodName,
        int parameterIndex)
    {
        _owner = owner;
        _rootMatcher = rootMatcher;
        _methodName = methodName;
        _parameterIndex = parameterIndex;
    }

    /// <summary>
    /// Registers this structural rule: discovered event types are taken from the selected parameter's type.
    /// </summary>
    public TypeConventionBuilder Type()
    {
        _owner.AddStructuralRule(new StructuralDomainEventRule(_rootMatcher, _methodName, _parameterIndex));
        return _owner;
    }
}
