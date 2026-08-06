namespace ProjectSeshat.App.ViewModels;

/// <summary>Classifies how a point should be rendered on the galactic sky-map.</summary>
public enum SkyPointKind
{
    /// <summary>A surveyed star system.</summary>
    System,

    /// <summary>A largely undiscovered region worth surveying.</summary>
    Region,

    /// <summary>The commander's current position.</summary>
    Current,

    /// <summary>The next jump target.</summary>
    Next
}

/// <summary>A single point rendered on the 3D galactic sky-map.</summary>
public sealed record SkyPoint(double X, double Y, double Z, string Name, SkyPointKind Kind, double Intensity = 1.0)
{
    public SkyPoint(Core.Domain.GalacticCoordinates position, string name, SkyPointKind kind, double intensity = 1.0)
        : this(position.X, position.Y, position.Z, name, kind, intensity)
    {
    }
}
