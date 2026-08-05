using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Investigations;

/// <summary>
/// Supports evidence capture and investigations: attaching evidence records to a
/// research thread and reviewing the evidence gathered for a line of enquiry.
/// </summary>
public sealed class InvestigationService
{
    private readonly IEvidenceRepository _evidenceRepository;
    private readonly IResearchThreadRepository _threadRepository;

    public InvestigationService(IEvidenceRepository evidenceRepository, IResearchThreadRepository threadRepository)
    {
        _evidenceRepository = evidenceRepository;
        _threadRepository = threadRepository;
    }

    public async Task<EvidenceRecord> CaptureEvidenceAsync(
        ResearchThreadId threadId,
        EvidenceKind kind,
        string summary,
        CancellationToken cancellationToken = default)
    {
        var thread = await _threadRepository.FindByIdAsync(threadId, cancellationToken)
            ?? throw new KeyNotFoundException($"Research thread {threadId} not found");

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Evidence needs a summary.", nameof(summary));
        }

        var evidence = new EvidenceRecord(
            new EvidenceId(Guid.NewGuid()),
            kind,
            summary.Trim(),
            DateTimeOffset.UtcNow,
            thread.Id);

        await _evidenceRepository.SaveAsync(evidence, cancellationToken);
        return evidence;
    }

    public Task<IReadOnlyList<EvidenceRecord>> GetEvidenceForThreadAsync(ResearchThreadId threadId, CancellationToken cancellationToken = default)
        => _evidenceRepository.FindByThreadIdAsync(threadId, cancellationToken);

    public Task<int> CountForThreadAsync(ResearchThreadId threadId, CancellationToken cancellationToken = default)
        => _evidenceRepository.CountByThreadIdAsync(threadId, cancellationToken);
}
