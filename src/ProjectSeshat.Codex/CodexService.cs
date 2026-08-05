using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Codex;

/// <summary>Provides discovery and codex knowledge operations for the codex boundary.</summary>
public sealed class CodexService
{
    /// <summary>Returns all codex entries that belong to the given research category.</summary>
    public async Task<IReadOnlyList<CodexEntry>> GetEntriesByCategoryAsync(
        CodexCategory category,
        ICodexEntryRepository repository,
        CancellationToken cancellationToken = default)
    {
        return await repository.FindByCategoryAsync(category, cancellationToken);
    }
}
