namespace ProjectSeshat.Core.Domain;

/// <summary>
/// Remembers where the commander currently is so the search guide can plot the next jump.
/// Captured from the most recent <c>FSDJump</c>/<c>Location</c> journal event.
/// </summary>
public sealed record NavigationState(
    NavigationStateId Id,
    StarSystemId? CurrentSystemId,
    DateTimeOffset LastUpdatedAt);
