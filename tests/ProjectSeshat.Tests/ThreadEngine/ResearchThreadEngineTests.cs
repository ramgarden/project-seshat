using ProjectSeshat.Core.Domain;
using ProjectSeshat.ThreadEngine;
using ProjectSeshat.Core.Contracts;
using Xunit;

namespace ProjectSeshat.Tests.ThreadEngine;

public sealed class ResearchThreadEngineTests
{
    [Fact]
    public async Task CreateThread_StartsOpenWithSubject()
    {
        var repository = new InMemoryThreadRepository();
        var engine = new ResearchThreadEngine(repository);

        var thread = await engine.CreateThreadAsync("Investigate the anomalous signal");

        Assert.Equal(ThreadStatus.Open, thread.Status);
        Assert.Equal("Investigate the anomalous signal", thread.Subject);
        Assert.Equal(1, await repository.CountAsync());
    }

    [Fact]
    public async Task CreateThread_RequiresASubject()
    {
        var engine = new ResearchThreadEngine(new InMemoryThreadRepository());

        await Assert.ThrowsAsync<ArgumentException>(() => engine.CreateThreadAsync("   "));
    }

    [Fact]
    public async Task Advance_StepsThroughLifecycle()
    {
        var engine = new ResearchThreadEngine(new InMemoryThreadRepository());
        var thread = await engine.CreateThreadAsync("A body worth studying");

        var investigating = await engine.AdvanceAsync(thread.Id);
        Assert.Equal(ThreadStatus.Investigating, investigating.Status);

        var concluded = await engine.AdvanceAsync(thread.Id);
        Assert.Equal(ThreadStatus.Concluded, concluded.Status);

        var archived = await engine.AdvanceAsync(thread.Id);
        Assert.Equal(ThreadStatus.Archived, archived.Status);
    }

    [Fact]
    public async Task Advance_ArchivedThreadThrows()
    {
        var engine = new ResearchThreadEngine(new InMemoryThreadRepository());
        var thread = await engine.CreateThreadAsync("Done thread");
        await engine.AdvanceAsync(thread.Id);
        await engine.AdvanceAsync(thread.Id);
        await engine.AdvanceAsync(thread.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.AdvanceAsync(thread.Id));
    }

    [Fact]
    public async Task Conclude_SetsStatusAndNotes()
    {
        var engine = new ResearchThreadEngine(new InMemoryThreadRepository());
        var thread = await engine.CreateThreadAsync("Mystery source");

        var concluded = await engine.ConcludeAsync(thread.Id, "Resolved: it was a stellar phenomena.");

        Assert.Equal(ThreadStatus.Concluded, concluded.Status);
        Assert.Equal("Resolved: it was a stellar phenomena.", concluded.Notes);
        Assert.NotNull(concluded.UpdatedAt);
    }

    [Fact]
    public async Task Reopen_ReturnsConcludedToOpen()
    {
        var engine = new ResearchThreadEngine(new InMemoryThreadRepository());
        var thread = await engine.CreateThreadAsync("Revisit later");
        await engine.ConcludeAsync(thread.Id, "Not fully understood.");

        var reopened = await engine.ReopenAsync(thread.Id);

        Assert.Equal(ThreadStatus.Open, reopened.Status);
    }
}
