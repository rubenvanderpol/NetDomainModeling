using System.Collections.Generic;

namespace DomainModeling;

/// <summary>
/// Shared helpers for CLR generic type names: short canonical forms and C#-style display names.
/// Used by the assembly scanner and any API surface that should show <c>MyEvent&lt;T&gt;</c> instead of <c>MyEvent`1</c>.
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
    /// Converts a CLR reflection constructed generic full name (possibly with assembly-qualified type arguments)
    /// to the short canonical form: <c>Ns.Event`1[[Ns.User]]</c>.
    /// Returns <c>null</c> if the name is not a constructed generic.
    /// </summary>
    public static string? ToCanonicalClosedGenericFullName(string clrFullName)
    {
        var outerStart = clrFullName.IndexOf("[[", StringComparison.Ordinal);
        if (outerStart < 0)
            return null;

        var prefix = clrFullName[..outerStart];
        var sb = new System.Text.StringBuilder(prefix);
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

    /// <summary>
    /// Builds a C#-style display name (e.g. <c>EntityDeletedEvent&lt;Organization&gt;</c>) from a short canonical
    /// constructed generic CLR name such as <c>Ns.EntityDeletedEvent`1[[Ns.Organization]]</c>.
    /// </summary>
    public static string FormatAsCSharp(string canonicalConstructedClrFullName)
    {
        var tick = canonicalConstructedClrFullName.IndexOf('`', StringComparison.Ordinal);
        if (tick < 0)
            return canonicalConstructedClrFullName;

        var defFqn = canonicalConstructedClrFullName[..tick];
        var simpleDef = defFqn.Contains('.', StringComparison.Ordinal)
            ? defFqn[(defFqn.LastIndexOf('.') + 1)..]
            : defFqn;

        var innerStart = canonicalConstructedClrFullName.IndexOf("[[", StringComparison.Ordinal);
        if (innerStart < 0)
            return StripGenericArity(simpleDef);

        var args = new List<string>();
        var i = innerStart + 1;
        while (i < canonicalConstructedClrFullName.Length)
        {
            if (canonicalConstructedClrFullName[i] != '[')
            {
                i++;
                continue;
            }

            i++;
            var argStart = i;
            var depth = 1;
            while (i < canonicalConstructedClrFullName.Length && depth > 0)
            {
                if (canonicalConstructedClrFullName[i] == '[') depth++;
                else if (canonicalConstructedClrFullName[i] == ']') depth--;
                i++;
            }

            var argSegment = canonicalConstructedClrFullName[argStart..(i - 1)];
            var comma = argSegment.IndexOf(',', StringComparison.Ordinal);
            var argType = (comma >= 0 ? argSegment[..comma] : argSegment).Trim();
            var shortArg = argType.Contains('.', StringComparison.Ordinal)
                ? argType[(argType.LastIndexOf('.') + 1)..]
                : argType;
            args.Add(shortArg);

            if (i < canonicalConstructedClrFullName.Length && canonicalConstructedClrFullName[i] == ',')
                i++;
        }

        return args.Count == 0
            ? StripGenericArity(simpleDef)
            : $"{StripGenericArity(simpleDef)}<{string.Join(", ", args)}>";
    }
}
