using DomainModeling.AspNetCore;
using FluentAssertions;
using Xunit;

namespace DomainModeling.Tests;

public sealed class DomainModelStoragePathResolverTests
{
    [Fact]
    public void Resolve_without_root_full_paths_relative_path_against_current_directory()
    {
        var result = DomainModelStoragePathResolver.Resolve(null, "./metadata");
        result.Should().Be(Path.GetFullPath("./metadata"));
    }

    [Fact]
    public void Resolve_without_root_treats_empty_root_as_absent()
    {
        var result = DomainModelStoragePathResolver.Resolve("   ", "./diagram-layout");
        result.Should().Be(Path.GetFullPath("./diagram-layout"));
    }

    [Fact]
    public void Resolve_with_root_joins_relative_storage_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "dm-root-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = DomainModelStoragePathResolver.Resolve(root, "./metadata");
            result.Should().Be(Path.GetFullPath(Path.Combine(root, "metadata")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_with_root_leaves_absolute_storage_path_unchanged()
    {
        var absolute = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "abs-meta"));
        var root = Path.Combine(Path.GetTempPath(), "ignored-root");
        var result = DomainModelStoragePathResolver.Resolve(root, absolute);
        result.Should().Be(absolute);
    }

    [Fact]
    public void Resolve_throws_when_path_is_empty()
    {
        var act = () => DomainModelStoragePathResolver.Resolve("/tmp", "  ");
        act.Should().Throw<ArgumentException>();
    }
}
