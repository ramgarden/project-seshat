using ProjectSeshat.Core.Domain;
using ProjectSeshat.Investigations;
using ProjectSeshat.Tests.ThreadEngine;
using Xunit;

namespace ProjectSeshat.Tests.Investigations;

public sealed class InvestigationServiceTests
{
    private static InvestigationService CreateService(out InMemoryThreadRepository threads, out InMemoryEvidenceRepository evidence)
    {
        threads = new InMemoryThreadRepository();
        evidence = new InMemoryEvidenceRepository();
        return new InvestigationService(evidence, threads);
    }

    private static async Task<ResearchThread> SaveThreadAsync(InMemoryThreadRepository threads, string subject)
    {
        var thread = new ResearchThread(
            new ResearchThreadId(Guid.NewGuid()),
            subject,
            null,
            null,
            null,
            ThreadStatus.Open,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        await threads.SaveAsync(thread);
        return thread;
    }

    [Fact]
    public async Task CaptureEvidence_AttachesToThread()
    {
        var service = CreateService(out var threads, out _);
        var thread = await SaveThreadAsync(threads, "Trace the anomalous emission");

        var captured = await service.CaptureEvidenceAsync(thread.Id, EvidenceKind.Observation, "Emission periodicity observed");

        Assert.Equal(thread.Id, captured.ThreadId);
        Assert.Equal(EvidenceKind.Observation, captured.Kind);
        Assert.Equal("Emission periodicity observed", captured.Summary);
        Assert.Equal(1, await service.CountForThreadAsync(thread.Id));
    }

    [Fact]
    public async Task CaptureEvidence_RequiresExistingThread()
    {
        var service = CreateService(out _, out _);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CaptureEvidenceAsync(new ResearchThreadId(Guid.NewGuid()), EvidenceKind.Observation, "x"));
    }

    [Fact]
    public async Task CaptureEvidence_RequiresSummary()
    {
        var service = CreateService(out var threads, out _);
        var thread = await SaveThreadAsync(threads, "Subject");

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CaptureEvidenceAsync(thread.Id, EvidenceKind.Discovery, "   "));
    }

    [Fact]
    public async Task GetEvidenceForThread_ReturnsOnlyThatThreadsEvidence()
    {
        var service = CreateService(out var threads, out _);
        var threadA = await SaveThreadAsync(threads, "A");
        var threadB = await SaveThreadAsync(threads, "B");

        await service.CaptureEvidenceAsync(threadA.Id, EvidenceKind.Observation, "A1");
        await service.CaptureEvidenceAsync(threadA.Id, EvidenceKind.Investigation, "A2");
        await service.CaptureEvidenceAsync(threadB.Id, EvidenceKind.Observation, "B1");

        var evidenceA = await service.GetEvidenceForThreadAsync(threadA.Id);

        Assert.Equal(2, evidenceA.Count);
        Assert.All(evidenceA, e => Assert.Equal(threadA.Id, e.ThreadId));
    }
}
