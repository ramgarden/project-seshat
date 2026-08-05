using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Tests.Investigations;

public sealed class InMemoryEvidenceRepository : IEvidenceRepository
{
    private readonly List<EvidenceRecord> _records = new();

    public ValueTask<EvidenceRecord?> FindByIdAsync(EvidenceId id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_records.FirstOrDefault(r => r.Id == id));

    public ValueTask SaveAsync(EvidenceRecord evidence, CancellationToken cancellationToken = default)
    {
        var index = _records.FindIndex(r => r.Id == evidence.Id);
        if (index >= 0)
        {
            _records[index] = evidence;
        }
        else
        {
            _records.Add(evidence);
        }

        return ValueTask.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_records.Count);

    public Task<bool> ExistsBySummaryAsync(string summary, CancellationToken cancellationToken = default)
        => Task.FromResult(_records.Any(r => r.Summary == summary));

    public Task<IReadOnlyList<EvidenceRecord>> FindByThreadIdAsync(ResearchThreadId threadId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EvidenceRecord>>(
            _records.Where(r => r.ThreadId == threadId).OrderByDescending(r => r.RecordedAt).ToList());

    public Task<int> CountByThreadIdAsync(ResearchThreadId threadId, CancellationToken cancellationToken = default)
        => Task.FromResult(_records.Count(r => r.ThreadId == threadId));
}
