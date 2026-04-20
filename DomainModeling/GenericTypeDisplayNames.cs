using System.Collections.Generic;
using System.Linq;

namespace DomainModeling;

/// <summary>
/// Shared helpers for generic domain type keys and C#-style display names.
/// Closed constructed types use bracket notation: <c>Ns.MyEvent[Ns.MyEntity]</c> (not CLR <c>MyEvent`1[[Ns.MyEntity]]</c>).
/// </summary>
public static class GenericTypeDisplayNames
{
    /// <summary>
    /// Strips the <c>`n</c> arity suffix from a metadata type name fragment.
    /// </summary>
    public static string StripGenericArity(string name)
    {
        var idx = name.IndexOf('`', StringComparison.Ordinal);
        return idx >= 0 ? name[..idx] : name;
    }

    /// <summary>
    /// Converts a CLR reflection constructed generic name (with <c>[[...]]</c>) or an existing bracket form
    /// to the canonical key: <c>Ns.Event[Ns.Arg1, Ns.Arg2]</c> (no <c>`n</c>, no double brackets).
    /// Returns <c>null</c> if the name is not a constructed generic.
    /// </summary>
    public static string? ToCanonicalClosedGenericFullName(string clrFullName)
    {
        if (string.IsNullOrEmpty(clrFullName))
            return null;

        if (clrFullName.Contains("[[", StringComparison.Ordinal))
            return FromClrDoubleBracketForm(clrFullName);

        // Bracket form: Ns.Event[Ns.Arg] (no CLR double brackets)
        if (clrFullName.Length >= 3 && clrFullName[^1] == ']')
        {
            var open = clrFullName.LastIndexOf('[');
            if (open > 0)
                return NormalizeBracketForm(clrFullName, open);
        }

        return null;
    }

    private static string? FromClrDoubleBracketForm(string clrFullName)
    {
        var outerStart = clrFullName.IndexOf("[[", StringComparison.Ordinal);
        if (outerStart < 0)
            return null;

        var defWithArity = clrFullName[..outerStart];
        var defStripped = StripGenericArity(defWithArity);
        var args = new List<string>();

        var i = outerStart + 1;
        while (i < clrFullName.Length)
        {
            if (clrFullName[i] != '[')
            {
                i++;
                continue;
            }

            i++;
            var commaPos = clrFullName.IndexOf(',', i);
            var closeBracket = clrFullName.IndexOf(']', i);
            string argFullName;
            if (commaPos >= 0 && closeBracket >= 0 && commaPos < closeBracket)
                argFullName = clrFullName[i..commaPos].Trim();
            else if (closeBracket >= 0)
                argFullName = clrFullName[i..closeBracket].Trim();
            else
                return null;

            args.Add(argFullName);

            var depth = 1;
            while (i < clrFullName.Length && depth > 0)
            {
                if (clrFullName[i] == '[') depth++;
                else if (clrFullName[i] == ']') depth--;
                i++;
            }
        }

        if (args.Count == 0)
            return null;

        return defStripped + "[" + string.Join(", ", args.Select(StripAssemblyQualifier)) + "]";
    }

    private static string NormalizeBracketForm(string fullName, int argsOpenBracket)
    {
        var def = fullName[..argsOpenBracket];
        var inner = fullName[(argsOpenBracket + 1)..^1];
        var args = SplitTopLevelCommaSeparated(inner);
        return def + "[" + string.Join(", ", args.Select(a => StripAssemblyQualifier(a.Trim()))) + "]";
    }

    private static List<string> SplitTopLevelCommaSeparated(string inner)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (c == '[' || c == '<') depth++;
            else if (c == ']' || c == '>') depth--;
            else if (c == ',' && depth == 0)
            {
                parts.Add(inner[start..i]);
                start = i + 1;
            }
        }

        parts.Add(inner[start..]);
        return parts;
    }

    private static string StripAssemblyQualifier(string typeNameWithOptionalAssembly)
    {
        var comma = typeNameWithOptionalAssembly.IndexOf(',', StringComparison.Ordinal);
        return comma >= 0 ? typeNameWithOptionalAssembly[..comma].Trim() : typeNameWithOptionalAssembly.Trim();
    }

    /// <summary>
    /// True if both strings refer to the same constructed generic (after normalizing CLR vs bracket forms).
    /// </summary>
    public static bool AreSameConstructedGeneric(string? a, string? b)
    {
        if (a is null || b is null)
            return false;
        if (string.Equals(a, b, StringComparison.Ordinal))
            return true;

        var ca = ToCanonicalClosedGenericFullName(a);
        var cb = ToCanonicalClosedGenericFullName(b);
        return ca is not null && cb is not null && string.Equals(ca, cb, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the open generic definition full name (with <c>`n</c> arity) for a closed generic key,
    /// e.g. <c>Ns.EntityDeletedEvent[Ns.C]</c> → <c>Ns.EntityDeletedEvent`1</c>.
    /// </summary>
    public static string? GetOpenGenericDefinitionWithArity(string closedOrClrFullName)
    {
        var canon = ToCanonicalClosedGenericFullName(closedOrClrFullName);
        if (canon is null || canon[^1] != ']')
            return null;

        var openBracket = canon.IndexOf('[', StringComparison.Ordinal);
        if (openBracket < 0)
            return null;

        var defPart = canon[..openBracket];
        if (defPart.IndexOf('`', StringComparison.Ordinal) >= 0)
            return defPart;

        var inner = canon[(openBracket + 1)..^1];
        var arity = SplitTopLevelCommaSeparated(inner).Count;
        return arity > 0 ? $"{defPart}`{arity}" : null;
    }

    /// <summary>
    /// Builds a C#-style display name (e.g. <c>EntityDeletedEvent&lt;Customer&gt;</c>) from a canonical
    /// bracket key such as <c>Ns.EntityDeletedEvent[Ns.Customer]</c>.
    /// </summary>
    public static string FormatAsCSharp(string canonicalBracketFullName)
    {
        var normalized = ToCanonicalClosedGenericFullName(canonicalBracketFullName) ?? canonicalBracketFullName;

        var open = normalized.LastIndexOf('[');
        if (open < 0 || normalized[^1] != ']')
            return normalized;

        var defFqn = StripGenericArity(normalized[..open]);
        var simpleDef = defFqn.Contains('.', StringComparison.Ordinal)
            ? defFqn[(defFqn.LastIndexOf('.') + 1)..]
            : defFqn;

        var inner = normalized[(open + 1)..^1];
        var args = SplitTopLevelCommaSeparated(inner);
        var shortArgs = args.Select(a =>
        {
            var t = a.Trim();
            return t.Contains('.', StringComparison.Ordinal)
                ? t[(t.LastIndexOf('.') + 1)..]
                : t;
        }).ToList();

        return shortArgs.Count == 0
            ? simpleDef
            : $"{simpleDef}<{string.Join(", ", shortArgs)}>";
    }
}
