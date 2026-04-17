using System.Reflection;
using System.Runtime.CompilerServices;

namespace DomainModeling.Discovery;

internal sealed partial class AssemblyScanner
{
    private static List<(string TargetFullName, string MethodName)> DetectInvocationsOnAggregates(
        Type handlerType,
        HashSet<string> aggregateFullNames)
    {
        var results = new List<(string, string)>();
        var seen = new HashSet<(string Target, string Method)>();

        void OnCall(string targetFullName, string methodName)
        {
            if (seen.Add((targetFullName, methodName)))
                results.Add((targetFullName, methodName));
        }

        ScanTypeForInstanceCallsOnTypes(handlerType, aggregateFullNames, OnCall);

        foreach (var nested in handlerType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (nested.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length == 0)
                continue;

            ScanTypeForInstanceCallsOnTypes(nested, aggregateFullNames, OnCall);
        }

        return results;
    }

    private static List<(string TargetFullName, string MethodName)> DetectInvocationsOnDeclaredTypes(
        Type handlerType,
        HashSet<string> declaringTypeFullNames)
    {
        var results = new List<(string, string)>();
        var seen = new HashSet<(string Target, string Method)>();

        void OnCall(string targetFullName, string methodName)
        {
            if (seen.Add((targetFullName, methodName)))
                results.Add((targetFullName, methodName));
        }

        ScanTypeForInstanceCallsOnTypes(handlerType, declaringTypeFullNames, OnCall);

        foreach (var nested in handlerType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (nested.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length == 0)
                continue;

            ScanTypeForInstanceCallsOnTypes(nested, declaringTypeFullNames, OnCall);
        }

        return results;
    }

    private static List<string> DetectInstantiatedTypes(Type handlerType, HashSet<string> typeFullNames)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void OnCtor(string typeFullName)
        {
            if (seen.Add(typeFullName))
                results.Add(typeFullName);
        }

        ScanTypeForNewObjOfTypes(handlerType, typeFullNames, OnCtor);

        foreach (var nested in handlerType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (nested.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length == 0)
                continue;

            ScanTypeForNewObjOfTypes(nested, typeFullNames, OnCtor);
        }

        return results;
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
        System.Reflection.MethodBody? body;
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
                if (resolved is not System.Reflection.MethodInfo mi)
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
        System.Reflection.MethodBody? body;
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
            }

            i += 4;
        }
    }
}
