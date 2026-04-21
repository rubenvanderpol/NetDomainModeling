using System.Reflection;

namespace DomainModeling.Builder;

/// <summary>
/// Describes how to derive domain event types from handler types (e.g. first parameter of <c>Handle</c>).
/// </summary>
internal sealed class StructuralDomainEventRule(TypeConventionBuilder rootMatcher, string methodName, int parameterIndex)
{
    public TypeConventionBuilder RootMatcher { get; } = rootMatcher;
    public string MethodName { get; } = methodName;
    public int ParameterIndex { get; } = parameterIndex;

    /// <summary>
    /// Yields non-null parameter types from matching roots that have a suitable method.
    /// </summary>
    public IEnumerable<Type> EnumerateEventTypes(IEnumerable<Type> candidateRoots)
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var root in candidateRoots)
        {
            if (!RootMatcher.Matches(root))
                continue;

            var methods = root.GetMethods(flags).Where(m => string.Equals(m.Name, MethodName, StringComparison.Ordinal));
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (ParameterIndex >= parameters.Length)
                    continue;

                var paramType = parameters[ParameterIndex].ParameterType;
                if (paramType is { IsAbstract: false, IsInterface: false, FullName: not null })
                    yield return paramType;
            }
        }
    }
}
