using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Core.Contracts;

/// <summary>Defines persistence operations for commander records without choosing a storage technology.</summary>
public interface ICommanderRepository
{
    ValueTask<Commander?> FindByIdAsync(CommanderId id, CancellationToken cancellationToken = default);

    ValueTask SaveAsync(Commander commander, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
