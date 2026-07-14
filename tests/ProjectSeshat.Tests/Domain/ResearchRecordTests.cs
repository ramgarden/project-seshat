using ProjectSeshat.Core.Domain;
using Xunit;

namespace ProjectSeshat.Tests.Domain;

public sealed class ResearchRecordTests
{
    [Fact]
    public void StarSystem_RetainsItsIdentityAndName()
    {
        var id = new StarSystemId(10477373803);
        var system = new StarSystem(id, "Sol");

        Assert.Equal(id, system.Id);
        Assert.Equal("Sol", system.Name);
    }

    [Fact]
    public void EvidenceRecord_RetainsItsResearchMetadata()
    {
        var recordedAt = new DateTimeOffset(3309, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var evidence = new EvidenceRecord(
            new EvidenceId(Guid.NewGuid()),
            EvidenceKind.Observation,
            "Initial observation",
            recordedAt);

        Assert.Equal(EvidenceKind.Observation, evidence.Kind);
        Assert.Equal(recordedAt, evidence.RecordedAt);
    }
}
