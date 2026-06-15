using System.Reflection;

namespace TypeScanning.Convention;

/// <summary>
/// Describes how to derive types from other types via method parameters
/// (e.g. first parameter of <c>Handle</c> on types matching a nested convention).
/// </summary>
internal sealed class StructuralTypeRule(TypeConventionBuilder rootMatcher, string methodName, int parameterIndex)
{
    public TypeConventionBuilder RootMatcher { get; } = rootMatcher;
    public string MethodName { get; } = methodName;
    public int ParameterIndex { get; } = parameterIndex;

    /// <summary>
    /// Yields non-null parameter types from matching roots that have a suitable method.
    /// </summary>
    public IEnumerable<Type> EnumerateTypes(IEnumerable<Type> candidateRoots)
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
