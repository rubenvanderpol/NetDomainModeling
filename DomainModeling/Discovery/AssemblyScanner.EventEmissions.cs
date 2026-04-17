using System.Reflection;
using DomainModeling.Graph;

namespace DomainModeling.Discovery;

internal sealed partial class AssemblyScanner
{
    private static List<EventEmissionInfo> DetectEventEmissions(
        Type type,
        List<Type> eventTypes,
        RoslynDocumentationIndexer? documentationIndexer)
    {
        var eventFullNames = new HashSet<string>(eventTypes.Select(e => e.FullName!));
        var emittedByMethod = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        ScanTypeMethods(type, eventFullNames, emittedByMethod);

        foreach (var nested in type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (nested.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false).Length > 0)
            {
                ScanTypeMethods(
                    nested,
                    eventFullNames,
                    emittedByMethod,
                    fallbackMethodName: TryExtractCompilerGeneratedMethodName(nested.Name));
            }
        }

        if (documentationIndexer is not null)
            MergeDocumentedMethodEmissions(type, eventFullNames, emittedByMethod, documentationIndexer);

        return emittedByMethod
            .SelectMany(kvp => kvp.Value.Select(method => new EventEmissionInfo
            {
                EventType = kvp.Key,
                MethodName = method
            }))
            .OrderBy(e => e.EventType)
            .ThenBy(e => e.MethodName)
            .ToList();
    }

    private static void MergeDocumentedMethodEmissions(
        Type declaringType,
        HashSet<string> eventFullNames,
        Dictionary<string, HashSet<string>> emittedByMethod,
        RoslynDocumentationIndexer documentationIndexer)
    {
        if (declaringType.FullName is null)
            return;

        var allMethods = declaringType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(declaringType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        foreach (var method in allMethods)
        {
            var methodName = NormalizeMethodName(method, fallbackMethodName: null);
            foreach (var documentedEvent in documentationIndexer.TryGetDocumentedEmissions(declaringType, methodName))
            {
                var key = ResolveCanonicalEventKey(documentedEvent, eventFullNames);
                if (key is not null)
                    AddEventEmission(emittedByMethod, key, methodName);
            }
        }
    }

    private static void ScanTypeMethods(
        Type type,
        HashSet<string> eventFullNames,
        Dictionary<string, HashSet<string>> emittedByMethod,
        string? fallbackMethodName = null)
    {
        var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        foreach (var method in allMethods)
        {
            var sourceMethodName = NormalizeMethodName(method, fallbackMethodName);
            ScanMethodBodyForEvents(method, type.Module, eventFullNames, emittedByMethod, sourceMethodName);
        }
    }

    private static void ScanMethodBodyForEvents(
        MethodBase method,
        Module module,
        HashSet<string> eventFullNames,
        Dictionary<string, HashSet<string>> emittedByMethod,
        string sourceMethodName)
    {
        System.Reflection.MethodBody? body;
        try { body = method.GetMethodBody(); }
        catch { return; }

        if (body is null)
            return;

        foreach (var local in body.LocalVariables)
            CheckTypeForEvents(local.LocalType, eventFullNames, emittedByMethod, sourceMethodName);

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
                    var key = ResolveCanonicalEventKey(fullName, eventFullNames);
                    if (key is not null)
                        AddEventEmission(emittedByMethod, key, sourceMethodName);
                }
            }
            catch
            {
            }

            i += 4;
        }
    }

    private static void CheckTypeForEvents(
        Type type,
        HashSet<string> eventFullNames,
        Dictionary<string, HashSet<string>> emittedByMethod,
        string sourceMethodName)
    {
        if (type.FullName is not null)
        {
            var key = ResolveCanonicalEventKey(type.FullName, eventFullNames);
            if (key is not null)
            {
                AddEventEmission(emittedByMethod, key, sourceMethodName);
                return;
            }
        }

        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
                CheckTypeForEvents(arg, eventFullNames, emittedByMethod, sourceMethodName);
        }
    }

    private static void AddEventEmission(
        Dictionary<string, HashSet<string>> emittedByMethod,
        string eventType,
        string methodName)
    {
        if (!emittedByMethod.TryGetValue(eventType, out var methods))
        {
            methods = new HashSet<string>(StringComparer.Ordinal);
            emittedByMethod[eventType] = methods;
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

    private static List<string> DetectPublishedEvents(Type type, List<Type> integrationEventTypes)
    {
        return DetectEventEmissions(type, integrationEventTypes, documentationIndexer: null)
            .Select(e => e.EventType)
            .Distinct()
            .ToList();
    }
}
