using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Core.Contracts;

/// <summary>Defines persistence for the commander's current navigation state.</summary>
public interface INavigationStateRepository
{
    Task<NavigationState?> GetAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(NavigationState state, CancellationToken cancellationToken = default);
}
