using System.Reflection;

namespace TypeScanning.Discovery;

/// <summary>
/// Assembly and type loading utilities.
/// </summary>
internal static class AssemblyTypeLoader
{
    public static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    /// <summary>
    /// When the same CLR full name appears in more than one scanned assembly, keep a single
    /// <see cref="Type"/> — prefer the assembly that appears earlier in <paramref name="assemblyOrder"/>.
    /// </summary>
    public static List<Type> DeduplicateTypesByFullName(IEnumerable<Type> types, Dictionary<Assembly, int> assemblyOrder)
    {
        var list = types as IList<Type> ?? types.ToList();
        var withFullName = list.Where(t => t.FullName is not null).ToList();
        var withoutFullName = list.Where(t => t.FullName is null).Distinct().ToList();

        static int Order(Dictionary<Assembly, int> order, Assembly a) =>
            order.TryGetValue(a, out var i) ? i : int.MaxValue;

        var deduped = withFullName
            .GroupBy(t => t.FullName!, StringComparer.Ordinal)
            .Select(g => g.OrderBy(t => Order(assemblyOrder, t.Assembly)).First())
            .ToList();

        return deduped.Concat(withoutFullName).ToList();
    }

    public static Dictionary<Assembly, int> BuildAssemblyOrder(IReadOnlyList<Assembly> assemblies)
    {
        var assemblyOrder = new Dictionary<Assembly, int>();
        for (var i = 0; i < assemblies.Count; i++)
        {
            if (!assemblyOrder.ContainsKey(assemblies[i]))
                assemblyOrder[assemblies[i]] = i;
        }

        return assemblyOrder;
    }
}
