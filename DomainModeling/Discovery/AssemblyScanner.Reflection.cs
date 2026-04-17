using System.Reflection;
using PropertyInfo = DomainModeling.Graph.PropertyInfo;
using MethodInfo = DomainModeling.Graph.MethodInfo;
using MethodParameterInfo = DomainModeling.Graph.MethodParameterInfo;

namespace DomainModeling.Discovery;

internal sealed partial class AssemblyScanner
{
    private static List<PropertyInfo> GetProperties(Type type, HashSet<string> knownDomainTypes)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.DeclaringType == type || p.DeclaringType?.Assembly == type.Assembly)
            .Select(p =>
            {
                var (propertyTypeName, isCollection, elementType) = AnalyzePropertyType(p.PropertyType);
                var referenceType = elementType ?? p.PropertyType;
                var refFullName = referenceType.FullName;
                var isKnownDomain = refFullName is not null && knownDomainTypes.Contains(refFullName);
                var isCustomType = !isKnownDomain && refFullName is not null && IsCustomType(referenceType);

                return new PropertyInfo
                {
                    Name = p.Name,
                    TypeName = propertyTypeName,
                    IsCollection = isCollection,
                    ReferenceTypeName = isKnownDomain || isCustomType ? refFullName : null
                };
            })
            .ToList();
    }

    private static bool IsCustomType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying.IsPrimitive) return false;
        if (underlying.IsEnum) return false;
        if (underlying == typeof(string)) return false;
        if (underlying == typeof(decimal)) return false;
        if (underlying == typeof(Guid)) return false;
        if (underlying == typeof(DateTime)) return false;
        if (underlying == typeof(DateTimeOffset)) return false;
        if (underlying == typeof(DateOnly)) return false;
        if (underlying == typeof(TimeOnly)) return false;
        if (underlying == typeof(TimeSpan)) return false;
        if (underlying == typeof(Uri)) return false;
        if (underlying == typeof(byte[])) return false;
        if (underlying == typeof(object)) return false;

        if (underlying.FullName is null) return false;
        if (underlying.Namespace?.StartsWith("System") == true) return false;
        if (underlying.Namespace?.StartsWith("Microsoft") == true) return false;

        return true;
    }

    private static List<MethodInfo> GetMethods(Type type)
    {
        var objectMethods = new HashSet<string>(
            typeof(object).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name));

        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !objectMethods.Contains(m.Name))
            .Select(m => new MethodInfo
            {
                Name = m.Name,
                ReturnTypeName = FormatTypeName(m.ReturnType),
                Parameters = m.GetParameters()
                    .Select(p => new MethodParameterInfo
                    {
                        Name = p.Name ?? "arg",
                        TypeName = FormatTypeName(p.ParameterType),
                    })
                    .ToList()
            })
            .ToList();
    }

    private static string FormatTypeName(Type type)
    {
        if (type == typeof(void)) return "void";
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(double)) return "double";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(float)) return "float";
        if (type == typeof(Guid)) return "Guid";

        if (type.IsGenericType)
        {
            var baseName = StripGenericArity(type.Name);
            var args = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
            return $"{baseName}<{args}>";
        }

        if (type.IsArray)
        {
            return FormatTypeName(type.GetElementType()!) + "[]";
        }

        return type.Name;
    }

    private static (string TypeName, bool IsCollection, Type? ElementType) AnalyzePropertyType(Type type)
    {
        if (type.IsArray)
        {
            var elem = type.GetElementType()!;
            return ($"{elem.Name}[]", true, elem);
        }

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();

            if (args.Length == 1 && IsCollectionType(genericDef))
            {
                return ($"ICollection<{args[0].Name}>", true, args[0]);
            }

            var argNames = string.Join(", ", args.Select(a => a.Name));
            return ($"{StripGenericArity(type.Name)}<{argNames}>", false, null);
        }

        return (type.Name, false, null);
    }

    private static bool IsCollectionType(Type genericDef)
    {
        return genericDef == typeof(IEnumerable<>)
            || genericDef == typeof(ICollection<>)
            || genericDef == typeof(IList<>)
            || genericDef == typeof(List<>)
            || genericDef == typeof(IReadOnlyCollection<>)
            || genericDef == typeof(IReadOnlyList<>)
            || genericDef == typeof(HashSet<>)
            || genericDef == typeof(ISet<>);
    }

    private static string StripGenericArity(string name)
    {
        var idx = name.IndexOf('`');
        return idx >= 0 ? name[..idx] : name;
    }
}
