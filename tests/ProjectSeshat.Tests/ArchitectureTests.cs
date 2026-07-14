using ProjectSeshat.Core;
using Xunit;

namespace ProjectSeshat.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void CoreMarker_CanBeCreated() => Assert.NotNull(new DomainMarker());
}
