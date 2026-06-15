using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace TypeScanning.Discovery;

/// <summary>
/// IL body analysis utilities for discovering type references in method bodies.
/// </summary>
public static class IlBodyAnalyzer
{
    /// <summary>
    /// Scans a type's methods (including compiler-generated nested types) for <c>newobj</c> / call patterns
    /// that reference types in <paramref name="targetTypeFullNames"/>.
    /// </summary>
    public static Dictionary<string, HashSet<string>> DetectTypeCreations(
        Type type,
        HashSet<string> targetTypeFullNames)
    {
        var emittedByMethod = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        ScanTypeMethods(type, targetTypeFullNames, emittedByMethod);

        foreach (var nested in type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (nested.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length == 0)
                continue;

            ScanTypeMethods(
                nested,
                targetTypeFullNames,
                emittedByMethod,
                fallbackMethodName: TryExtractCompilerGeneratedMethodName(nested.Name));
        }

        return emittedByMethod;
    }

    /// <summary>
    /// Finds instance method calls whose declaring type is in <paramref name="declaringTypeFullNames"/>.
    /// </summary>
    public static List<(string TargetFullName, string MethodName)> DetectInstanceCallsOnTypes(
        Type type,
        HashSet<string> declaringTypeFullNames)
    {
        var results = new List<(string, string)>();
        var seen = new HashSet<(string Target, string Method)>();

        void OnCall(string targetFullName, string methodName)
        {
            if (seen.Add((targetFullName, methodName)))
                results.Add((targetFullName, methodName));
        }

        ScanTypeForInstanceCallsOnTypes(type, declaringTypeFullNames, OnCall);

        foreach (var nested in type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (nested.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length == 0)
                continue;

            ScanTypeForInstanceCallsOnTypes(nested, declaringTypeFullNames, OnCall);
        }

        return results;
    }

    /// <summary>
    /// Finds <c>newobj</c> instructions that construct types in <paramref name="typeFullNames"/>.
    /// </summary>
    public static List<string> DetectInstantiatedTypes(Type type, HashSet<string> typeFullNames)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void OnCtor(string typeFullName)
        {
            if (seen.Add(typeFullName))
                results.Add(typeFullName);
        }

        ScanTypeForNewObjOfTypes(type, typeFullNames, OnCtor);

        foreach (var nested in type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (nested.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length == 0)
                continue;

            ScanTypeForNewObjOfTypes(nested, typeFullNames, OnCtor);
        }

        return results;
    }

    /// <summary>
    /// Resolves a type full name against a set of registered type names, including generic forms.
    /// </summary>
    public static string? ResolveCanonicalTypeKey(string typeFullName, HashSet<string> registeredTypeFullNames)
    {
        if (registeredTypeFullNames.Contains(typeFullName))
            return typeFullName;

        var bracket = typeFullName.IndexOf("[[", StringComparison.Ordinal);
        if (bracket >= 0)
        {
            var canonical = ToCanonicalClosedGenericFullName(typeFullName);
            if (canonical is not null && registeredTypeFullNames.Contains(canonical))
                return canonical;

            var def = typeFullName[..bracket];
            if (registeredTypeFullNames.Contains(def))
                return def;
        }

        return null;
    }

    private static void ScanTypeMethods(
        Type type,
        HashSet<string> targetTypeFullNames,
        Dictionary<string, HashSet<string>> emittedByMethod,
        string? fallbackMethodName = null)
    {
        var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        foreach (var method in allMethods)
        {
            var sourceMethodName = NormalizeMethodName(method, fallbackMethodName);
            ScanMethodBodyForTypes(method, type.Module, targetTypeFullNames, emittedByMethod, sourceMethodName);
        }
    }

    private static void ScanMethodBodyForTypes(
        MethodBase method,
        Module module,
        HashSet<string> targetTypeFullNames,
        Dictionary<string, HashSet<string>> emittedByMethod,
        string sourceMethodName)
    {
        MethodBody? body;
        try { body = method.GetMethodBody(); }
        catch { return; }

        if (body is null)
            return;

        foreach (var local in body.LocalVariables)
            CheckTypeForTargets(local.LocalType, targetTypeFullNames, emittedByMethod, sourceMethodName);

        var il = body.GetILAsByteArray();
        if (il is null)
            return;

        const byte newobj = 0x73;
        const byte call = 0x28;
        const byte callvirt = 0x6F;

        for (var i = 0; i < il.Length; i++)
        {
            if (il[i] is not (newobj or call or callvirt))
                continue;

            if (i + 4 >= il.Length)
                continue;

            var token = il[i + 1]
                      | (il[i + 2] << 8)
                      | (il[i + 3] << 16)
                      | (il[i + 4] << 24);

            try
            {
                var resolved = module.ResolveMethod(token);
                if (resolved?.DeclaringType?.FullName is { } fullName)
                {
                    var key = ResolveCanonicalTypeKey(fullName, targetTypeFullNames);
                    if (key is not null)
                        AddTypeReference(emittedByMethod, key, sourceMethodName);
                }
            }
            catch
            {
                // Token might not be a method token — skip
            }

            i += 4;
        }
    }

    private static void CheckTypeForTargets(
        Type type,
        HashSet<string> targetTypeFullNames,
        Dictionary<string, HashSet<string>> emittedByMethod,
        string sourceMethodName)
    {
        if (type.FullName is not null)
        {
            var key = ResolveCanonicalTypeKey(type.FullName, targetTypeFullNames);
            if (key is not null)
            {
                AddTypeReference(emittedByMethod, key, sourceMethodName);
                return;
            }
        }

        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
                CheckTypeForTargets(arg, targetTypeFullNames, emittedByMethod, sourceMethodName);
        }
    }

    private static void AddTypeReference(
        Dictionary<string, HashSet<string>> emittedByMethod,
        string typeFullName,
        string methodName)
    {
        if (!emittedByMethod.TryGetValue(typeFullName, out var methods))
        {
            methods = new HashSet<string>(StringComparer.Ordinal);
            emittedByMethod[typeFullName] = methods;
        }

        methods.Add(methodName);
    }

    private static string NormalizeMethodName(MethodBase method, string? fallbackMethodName)
    {
        var methodName = method.Name;
        if (methodName == ".ctor")
            return "ctor";
        if (methodName == ".cctor")
            return "cctor";
        if (methodName == "MoveNext" && !string.IsNullOrWhiteSpace(fallbackMethodName))
            return fallbackMethodName!;
        return methodName;
    }

    private static string? TryExtractCompilerGeneratedMethodName(string generatedTypeName)
    {
        var open = generatedTypeName.IndexOf('<');
        var close = generatedTypeName.IndexOf('>');
        if (open < 0 || close <= open + 1)
            return null;

        var methodName = generatedTypeName[(open + 1)..close];
        return string.IsNullOrWhiteSpace(methodName) ? null : methodName;
    }

    private static void ScanTypeForInstanceCallsOnTypes(
        Type type,
        HashSet<string> declaringTypeFullNames,
        Action<string, string> onCall)
    {
        var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        foreach (var method in allMethods)
            ScanMethodBodyForInstanceCallsOnTypes(method, type.Module, declaringTypeFullNames, onCall);
    }

    private static void ScanMethodBodyForInstanceCallsOnTypes(
        MethodBase method,
        Module module,
        HashSet<string> declaringTypeFullNames,
        Action<string, string> onCall)
    {
        MethodBody? body;
        try { body = method.GetMethodBody(); }
        catch { return; }

        if (body is null)
            return;

        var il = body.GetILAsByteArray();
        if (il is null)
            return;

        const byte call = 0x28;
        const byte callvirt = 0x6F;

        for (var i = 0; i < il.Length; i++)
        {
            if (il[i] is not (call or callvirt))
                continue;

            if (i + 4 >= il.Length)
                continue;

            var token = il[i + 1]
                      | (il[i + 2] << 8)
                      | (il[i + 3] << 16)
                      | (il[i + 4] << 24);

            try
            {
                var resolved = module.ResolveMethod(token);
                if (resolved is not MethodInfo mi)
                    continue;
                if (mi.IsStatic)
                    continue;
                if (string.Equals(mi.Name, ".ctor", StringComparison.Ordinal))
                    continue;
                if (mi.IsSpecialName)
                    continue;

                var decl = mi.DeclaringType;
                if (decl?.FullName is not { } declFullName)
                    continue;
                if (!declaringTypeFullNames.Contains(declFullName))
                    continue;

                onCall(declFullName, mi.Name);
            }
            catch
            {
                // Token might not be a method token
            }

            i += 4;
        }
    }

    private static void ScanTypeForNewObjOfTypes(Type type, HashSet<string> typeFullNames, Action<string> onCtor)
    {
        var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        foreach (var method in allMethods)
            ScanMethodBodyForNewObjOfTypes(method, type.Module, typeFullNames, onCtor);
    }

    private static void ScanMethodBodyForNewObjOfTypes(
        MethodBase method,
        Module module,
        HashSet<string> typeFullNames,
        Action<string> onCtor)
    {
        MethodBody? body;
        try { body = method.GetMethodBody(); }
        catch { return; }

        if (body is null)
            return;

        var il = body.GetILAsByteArray();
        if (il is null)
            return;

        const byte newobj = 0x73;

        for (var i = 0; i < il.Length; i++)
        {
            if (il[i] != newobj)
                continue;

            if (i + 4 >= il.Length)
                continue;

            var token = il[i + 1]
                      | (il[i + 2] << 8)
                      | (il[i + 3] << 16)
                      | (il[i + 4] << 24);

            try
            {
                var resolved = module.ResolveMethod(token);
                if (resolved is not ConstructorInfo ctor)
                    continue;
                var decl = ctor.DeclaringType;
                if (decl?.FullName is not { } declFullName)
                    continue;
                if (!typeFullNames.Contains(declFullName))
                    continue;

                onCtor(declFullName);
            }
            catch
            {
                // Token might not be a method token
            }

            i += 4;
        }
    }

    private static string? ToCanonicalClosedGenericFullName(string clrFullName)
    {
        var outerStart = clrFullName.IndexOf("[[", StringComparison.Ordinal);
        if (outerStart < 0)
            return null;

        var prefix = clrFullName[..outerStart];
        var sb = new StringBuilder(prefix);
        sb.Append('[');

        var i = outerStart + 1;
        var first = true;
        while (i < clrFullName.Length)
        {
            if (clrFullName[i] == '[')
            {
                if (!first) sb.Append(',');
                first = false;
                i++;
                var commaPos = clrFullName.IndexOf(',', i);
                var closeBracket = clrFullName.IndexOf(']', i);
                string argFullName;
                if (commaPos >= 0 && commaPos < closeBracket)
                    argFullName = clrFullName[i..commaPos].Trim();
                else if (closeBracket >= 0)
                    argFullName = clrFullName[i..closeBracket].Trim();
                else
                    return null;

                sb.Append('[');
                sb.Append(argFullName);
                sb.Append(']');

                var depth = 1;
                while (i < clrFullName.Length && depth > 0)
                {
                    if (clrFullName[i] == '[') depth++;
                    else if (clrFullName[i] == ']') depth--;
                    i++;
                }
            }
            else
            {
                i++;
            }
        }

        sb.Append(']');
        return sb.ToString();
    }
}
