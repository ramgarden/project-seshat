using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.ThreadEngine;

/// <summary>
/// Drives research-thread workflows: creating a thread around a subject,
/// advancing it through its lifecycle, and storing working notes.
/// </summary>
public sealed class ResearchThreadEngine
{
    private readonly IResearchThreadRepository _repository;

    public ResearchThreadEngine(IResearchThreadRepository repository)
    {
        _repository = repository;
    }

    public async Task<ResearchThread> CreateThreadAsync(
        string subject,
        StarSystemId? systemId = null,
        CelestialBodyId? bodyId = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("A research thread needs a subject.", nameof(subject));
        }

        var now = DateTimeOffset.UtcNow;
        var thread = new ResearchThread(
            new ResearchThreadId(Guid.NewGuid()),
            subject.Trim(),
            notes,
            systemId,
            bodyId,
            ThreadStatus.Open,
            now,
            now);

        await _repository.SaveAsync(thread, cancellationToken);
        return thread;
    }

    public async Task<ResearchThread> AdvanceAsync(ResearchThreadId id, CancellationToken cancellationToken = default)
    {
        var thread = await RequireThreadAsync(id, cancellationToken);

        var next = thread.Status switch
        {
            ThreadStatus.Open => ThreadStatus.Investigating,
            ThreadStatus.Investigating => ThreadStatus.Concluded,
            ThreadStatus.Concluded => ThreadStatus.Archived,
            ThreadStatus.Archived => throw new InvalidOperationException("An archived research thread cannot be advanced."),
            _ => throw new InvalidOperationException($"Unknown thread status: {thread.Status}")
        };

        return await UpdateAsync(thread with { Status = next }, cancellationToken);
    }

    public async Task<ResearchThread> ReopenAsync(ResearchThreadId id, CancellationToken cancellationToken = default)
    {
        var thread = await RequireThreadAsync(id, cancellationToken);
        if (thread.Status is ThreadStatus.Open or ThreadStatus.Investigating)
        {
            return thread;
        }

        return await UpdateAsync(thread with { Status = ThreadStatus.Open }, cancellationToken);
    }

    public async Task<ResearchThread> ConcludeAsync(ResearchThreadId id, string? conclusion, CancellationToken cancellationToken = default)
    {
        var thread = await RequireThreadAsync(id, cancellationToken);
        var notes = string.IsNullOrWhiteSpace(conclusion)
            ? thread.Notes
            : conclusion.Trim();

        return await UpdateAsync(thread with { Status = ThreadStatus.Concluded, Notes = notes }, cancellationToken);
    }

    public Task<IReadOnlyList<ResearchThread>> ListAsync(int maxCount = 50, CancellationToken cancellationToken = default)
        => _repository.ListAsync(maxCount, cancellationToken);

    public async Task<ResearchThread?> FindAsync(ResearchThreadId id, CancellationToken cancellationToken = default)
        => await _repository.FindByIdAsync(id, cancellationToken);

    private async Task<ResearchThread> RequireThreadAsync(ResearchThreadId id, CancellationToken cancellationToken)
    {
        return await _repository.FindByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Research thread {id} not found");
    }

    private async Task<ResearchThread> UpdateAsync(ResearchThread updated, CancellationToken cancellationToken)
    {
        var saved = updated with { UpdatedAt = DateTimeOffset.UtcNow };
        await _repository.SaveAsync(saved, cancellationToken);
        return saved;
    }
}
