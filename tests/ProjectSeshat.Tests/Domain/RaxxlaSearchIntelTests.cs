using ProjectSeshat.Core.Domain;
using Xunit;

namespace ProjectSeshat.Tests.Domain;

public sealed class RaxxlaSearchIntelTests
{
    [Theory]
    [InlineData("Earthlike body")]
    [InlineData("Water world")]
    [InlineData("Ammonia world")]
    [InlineData("High metal content body")]
    public void ReasonForBodyClass_FlagsNotableClasses(string planetClass)
    {
        var reason = RaxxlaSearchIntel.ReasonForBodyClass(planetClass);
        Assert.NotNull(reason);
        Assert.Contains("hunt community", reason);
    }

    [Fact]
    public void ReasonForBodyClass_IgnoresOrdinaryClasses()
    {
        Assert.Null(RaxxlaSearchIntel.ReasonForBodyClass("Rocky body"));
        Assert.Null(RaxxlaSearchIntel.ReasonForBodyClass(null));
    }

    [Fact]
    public void ReasonForEighthMoon_FlagsBareEighthMoon()
    {
        var reason = RaxxlaSearchIntel.ReasonForEighthMoon("LHS 3447 8");
        Assert.NotNull(reason);
        Assert.Contains("8th moon", reason);
        Assert.Contains("Dark Wheel", reason);
    }

    [Fact]
    public void ReasonForEighthMoon_FlagsMoonLetterAfterEight()
    {
        Assert.NotNull(RaxxlaSearchIntel.ReasonForEighthMoon("LHS 3447 8 A"));
        Assert.NotNull(RaxxlaSearchIntel.ReasonForEighthMoon("Col 285 Sector XY-Z d1-42 8 b"));
    }

    [Fact]
    public void ReasonForEighthMoon_IgnoresSeventhMoon()
    {
        Assert.Null(RaxxlaSearchIntel.ReasonForEighthMoon("LHS 3447 7 A"));
        Assert.Null(RaxxlaSearchIntel.ReasonForEighthMoon("LHS 3447 8A"));
    }

    [Theory]
    [InlineData("Other")]
    [InlineData("Non-Human")]
    [InlineData("Thargoid")]
    [InlineData("Guardian")]
    [InlineData("Unregistered")]
    [InlineData("Wakes")]
    [InlineData("Anomaly")]
    [InlineData("Unknown")]
    public void ReasonForSignalTypes_FlagsSuspiciousTerms(string term)
    {
        var reason = RaxxlaSearchIntel.ReasonForSignalTypes(term);
        Assert.NotNull(reason);
        Assert.Contains("flags these", reason);
    }

    [Fact]
    public void ReasonForSignalTypes_IgnoresCommonTypes()
    {
        Assert.Null(RaxxlaSearchIntel.ReasonForSignalTypes("Biological,Geological"));
        Assert.Null(RaxxlaSearchIntel.ReasonForSignalTypes(null));
    }

    [Theory]
    [InlineData("Raxxla")]
    [InlineData("Omphalos")]
    [InlineData("Dark Wheel")]
    [InlineData("Astrophel")]
    [InlineData("Delphi")]
    [InlineData("Witchspace")]
    public void ReasonForLoreName_FlagsLoreTerms(string name)
    {
        var reason = RaxxlaSearchIntel.ReasonForLoreName(name);
        Assert.NotNull(reason);
        Assert.Contains("lore hint", reason);
    }

    [Fact]
    public void ReasonForLoreName_IgnoresOrdinaryNames()
    {
        Assert.Null(RaxxlaSearchIntel.ReasonForLoreName("LHS 3447"));
        Assert.Null(RaxxlaSearchIntel.ReasonForLoreName(null));
    }

    [Fact]
    public void IsWithinSolSearchArea_HonoursBubble()
    {
        Assert.True(RaxxlaSearchIntel.IsWithinSolSearchArea(new GalacticCoordinates(10, -20, 30)));
        Assert.True(RaxxlaSearchIntel.IsWithinSolSearchArea(new GalacticCoordinates(0, 0, 0)));
        Assert.False(RaxxlaSearchIntel.IsWithinSolSearchArea(new GalacticCoordinates(5000, 0, 0)));
        Assert.False(RaxxlaSearchIntel.IsWithinSolSearchArea(null));
    }
}