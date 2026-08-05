using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Tests.ThreadEngine;

public sealed class InMemoryThreadRepository : IResearchThreadRepository
{
    private readonly List<ResearchThread> _threads = new();

    public ValueTask<ResearchThread?> FindByIdAsync(ResearchThreadId id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_threads.FirstOrDefault(t => t.Id == id));

    public ValueTask SaveAsync(ResearchThread thread, CancellationToken cancellationToken = default)
    {
        var index = _threads.FindIndex(t => t.Id == thread.Id);
        if (index >= 0)
        {
            _threads[index] = thread;
        }
        else
        {
            _threads.Add(thread);
        }

        return ValueTask.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_threads.Count);

    public Task<IReadOnlyList<ResearchThread>> ListAsync(int maxCount, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ResearchThread>>(
            _threads.OrderByDescending(t => t.CreatedAt).Take(maxCount).ToList());

    public Task<IReadOnlyList<ResearchThread>> FindBySubjectAsync(
        StarSystemId? systemId,
        CelestialBodyId? bodyId,
        CancellationToken cancellationToken = default)
    {
        var matches = _threads
            .Where(t => (!systemId.HasValue || t.SystemId == systemId) && (!bodyId.HasValue || t.BodyId == bodyId))
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<ResearchThread>>(matches);
    }
}
