using TaskManager.Infrastructure.Services;
using Xunit;

namespace TaskManager.Api.Tests;

public sealed class StructureTests
{
    [Fact]
    public void NoTypesInTheOldApiNamespace()
    {
        var offenders = typeof(Program).Assembly.GetTypes()
            .Where(type => type.Namespace is { } ns && (ns.StartsWith("TaskManager.Api.Api") || ns.StartsWith("TaskManager.Api.Services")))
            .Select(type => type.FullName);
        Assert.Empty(offenders);
    }

    [Fact]
    public void InfrastructureDoesNotReferenceAspNetCore()
    {
        var references = typeof(FileStorage).Assembly.GetReferencedAssemblies()
            .Where(name => name.Name!.StartsWith("Microsoft.AspNetCore"));
        Assert.Empty(references);
    }
}
