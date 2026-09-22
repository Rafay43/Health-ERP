using System.Reflection;
using Shouldly;
using NetArchTest.Rules;

namespace HealthERP.Domain.UnitTests;

public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly =
        typeof(HealthERP.Domain.AssemblyReference).Assembly;

    [Fact]
    public void Domain_Should_Not_HaveDependencyOnOtherProjects()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "HealthERP.Application",
                "HealthERP.Infrastructure",
                "HealthERP.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}