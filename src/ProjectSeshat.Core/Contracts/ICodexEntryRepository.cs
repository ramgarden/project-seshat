using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Core.Contracts;

/// <summary>Defines persistence operations for codex discovery entries.</summary>
public interface ICodexEntryRepository
{
    ValueTask<CodexEntry?> FindByIdAsync(CodexEntryId id, CancellationToken cancellationToken = default);

    ValueTask SaveAsync(CodexEntry entry, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CodexEntry>> FindByCategoryAsync(CodexCategory category, CancellationToken cancellationToken = default);
}
