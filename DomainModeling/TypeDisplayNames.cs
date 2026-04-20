namespace DomainModeling;

/// <summary>
/// C#-style display names for <see cref="System.Type"/> (nested types, generics, arity markers).
/// Used for graph node <c>Name</c> and type references in property/method metadata.
/// </summary>
internal static class TypeDisplayNames
{
    /// <summary>
    /// Short type name for UI: nested types as <c>Outer.Inner</c>, closed generics as <c>Event&lt;T&gt;</c> (no <c>`1</c>).
    /// </summary>
    public static string ShortName(Type type)
    {
        if (type.IsGenericParameter || type.IsGenericTypeParameter)
            return type.Name;

        if (type.IsArray)
            return ShortName(type.GetElementType()!) + "[]";

        if (type.IsGenericType)
        {
            // Open generic definition: show "EntityDeletedEvent", not "EntityDeletedEvent`1" / not "EntityDeletedEvent<TEntity>"
            if (type.IsGenericTypeDefinition)
                return StripArity(type.Name);

            var defName = StripArity(type.IsNested ? type.Name : type.Name);
            var args = string.Join(", ", type.GetGenericArguments().Select(ShortName));
            if (type.IsNested)
                return $"{ShortName(type.DeclaringType!)}.{defName}<{args}>";
            return $"{defName}<{args}>";
        }

        if (type.IsNested)
            return $"{ShortName(type.DeclaringType!)}.{type.Name}";

        return type.Name;
    }

    /// <summary>
    /// Type as it would appear in C# source (keywords for primitives, nested + generics).
    /// </summary>
    public static string FormatTypeReference(Type type)
    {
        if (type.IsGenericParameter || type.IsGenericTypeParameter)
            return type.Name;

        if (type == typeof(void)) return "void";
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(double)) return "double";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(float)) return "float";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(short)) return "short";
        if (type == typeof(uint)) return "uint";
        if (type == typeof(ulong)) return "ulong";
        if (type == typeof(char)) return "char";
        if (type == typeof(object)) return "object";
        if (type == typeof(nint)) return "nint";
        if (type == typeof(nuint)) return "nuint";
        if (type == typeof(Guid)) return "Guid";

        if (type.IsGenericType)
        {
            if (type.IsGenericTypeDefinition)
                return $"{StripArity(type.Name)}<>";

            var defName = StripArity(type.IsNested ? type.Name : type.Name);
            var args = string.Join(", ", type.GetGenericArguments().Select(FormatTypeReference));
            if (type.IsNested)
                return $"{FormatTypeReference(type.DeclaringType!)}.{defName}<{args}>";
            return $"{defName}<{args}>";
        }

        if (type.IsArray)
            return $"{FormatTypeReference(type.GetElementType()!)}[]";

        if (type.IsNested)
            return $"{FormatTypeReference(type.DeclaringType!)}.{type.Name}";

        return type.Name;
    }

    private static string StripArity(string name)
    {
        var idx = name.IndexOf('`');
        return idx >= 0 ? name[..idx] : name;
    }
}
