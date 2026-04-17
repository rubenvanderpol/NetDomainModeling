using DomainModeling;
using FluentAssertions;
using Xunit;

namespace DomainModeling.Tests;

public class GenericTypeDisplayNamesTests
{
    [Fact]
    public void ToCanonicalClosedGenericFullName_StripsAssemblyQualifiers()
    {
        var fq =
            "Ns.EntityDeletedEvent`1[[Ns.Organization, MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]";
        var canonical = GenericTypeDisplayNames.ToCanonicalClosedGenericFullName(fq);
        canonical.Should().Be("Ns.EntityDeletedEvent`1[[Ns.Organization]]");
    }

    [Fact]
    public void FormatAsCSharp_UsesAngleBrackets()
    {
        var canonical = "DomainModeling.Example.Domain.EntityDeletedEvent`1[[DomainModeling.Example.Domain.Organization]]";
        GenericTypeDisplayNames.FormatAsCSharp(canonical).Should().Be("EntityDeletedEvent<Organization>");
    }
}
