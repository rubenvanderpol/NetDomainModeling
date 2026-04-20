using System.Linq;
using DomainModeling;
using DomainModeling.Tests.SampleDomain;
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
        canonical.Should().Be("Ns.EntityDeletedEvent[Ns.Organization]");
    }

    [Fact]
    public void FormatAsCSharp_UsesAngleBrackets()
    {
        var canonical =
            "DomainModeling.Example.Domain.EntityDeletedEvent[DomainModeling.Example.Domain.Organization]";
        GenericTypeDisplayNames.FormatAsCSharp(canonical).Should().Be("EntityDeletedEvent<Organization>");
    }

    [Fact]
    public void GetOpenGenericDefinitionWithArity_FromBracketForm()
    {
        GenericTypeDisplayNames.GetOpenGenericDefinitionWithArity(
                "DomainModeling.Example.Domain.EntityDeletedEvent[DomainModeling.Example.Domain.Customer]")
            .Should().Be("DomainModeling.Example.Domain.EntityDeletedEvent`1");
    }

    [Fact]
    public void Handler_InterfaceTypeArg_NormalizesToSameKey_AsSyntheticEventNode()
    {
        var handler = typeof(OrderDeletedEventHandler);
        var iface = handler.GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition().Name == "IEventHandler`1");
        var arg = iface.GetGenericArguments()[0];
        arg.FullName.Should().NotBeNull();
        var fromHandler = GenericTypeDisplayNames.ToCanonicalClosedGenericFullName(arg.FullName!);
        fromHandler.Should().NotBeNull();
        var expected =
            "DomainModeling.Tests.SampleDomain.EntityDeletedEvent[DomainModeling.Tests.SampleDomain.Order]";
        fromHandler.Should().Be(expected);
        GenericTypeDisplayNames.AreSameConstructedGeneric(fromHandler!, expected).Should().BeTrue();
    }
}
