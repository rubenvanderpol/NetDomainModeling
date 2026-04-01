namespace DomainModeling.AspNetCore;

/// <summary>
/// Resolves persisted storage directories for the domain model explorer.
/// When <paramref name="storageRoot"/> is set, relative <paramref name="path"/> values
/// are resolved under that root; absolute <paramref name="path"/> values are unchanged.
/// </summary>
internal static class DomainModelStoragePathResolver
{
    /// <summary>
    /// Returns a full directory path for storage.
    /// </summary>
    /// <param name="storageRoot">Optional base directory; null or whitespace means <paramref name="path"/> is resolved alone (current behavior).</param>
    /// <param name="path">Directory path, typically a default like <c>./metadata</c> or a custom relative/absolute path.</param>
    public static string Resolve(string? storageRoot, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var trimmedPath = path.Trim();

        if (string.IsNullOrWhiteSpace(storageRoot))
            return Path.GetFullPath(trimmedPath);

        var trimmedRoot = storageRoot.Trim();
        if (Path.IsPathRooted(trimmedPath))
            return Path.GetFullPath(trimmedPath);

        var rootFull = Path.GetFullPath(trimmedRoot);
        return Path.GetFullPath(Path.Combine(rootFull, trimmedPath));
    }
}
