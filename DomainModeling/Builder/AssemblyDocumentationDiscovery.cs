using System.Reflection;

namespace DomainModeling.Builder;

/// <summary>
/// Locates a <c>.csproj</c> for Roslyn documentation indexing by walking up from an assembly's output directory.
/// </summary>
internal static class AssemblyDocumentationDiscovery
{
    /// <summary>
    /// Returns the full path to a single unambiguous project file, or <c>null</c> if none is found.
    /// </summary>
    public static string? TryFindProjectForDocumentation(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var location = assembly.Location;
        if (string.IsNullOrEmpty(location))
            return null;

        var outputDir = Path.GetDirectoryName(location);
        if (string.IsNullOrEmpty(outputDir))
            return null;

        var preferredName = assembly.GetName().Name + ".csproj";

        for (var dir = outputDir; !string.IsNullOrEmpty(dir); dir = Directory.GetParent(dir)?.FullName ?? string.Empty)
        {
            string[] projects;
            try
            {
                projects = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            if (projects.Length == 0)
                continue;

            if (projects.Length == 1)
                return Path.GetFullPath(projects[0]);

            foreach (var p in projects)
            {
                if (string.Equals(Path.GetFileName(p), preferredName, StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(p);
            }

            // Several projects in one folder and none match the assembly name — avoid guessing.
            return null;
        }

        return null;
    }
}
