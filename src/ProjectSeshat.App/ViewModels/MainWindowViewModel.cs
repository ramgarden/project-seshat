namespace ProjectSeshat.App.ViewModels;

/// <summary>Presentation data for the initial research dashboard.</summary>
public sealed class MainWindowViewModel
{
    public string WindowTitle => "Project Seshat - Galactic Research Platform";

    public string ProjectName => "PROJECT SESHAT";

    public string PlatformName => "Galactic Research Platform";

    public string StatisticsHeading => "STATISTICS";

    public string SystemsIndexedLabel => "SYSTEMS INDEXED";

    public int SystemsIndexedCount => 0;

    public string CommanderRecordsLabel => "COMMANDER RECORDS";

    public int CommanderRecordsCount => 0;

    public string EvidenceRecordsLabel => "EVIDENCE RECORDS";

    public int EvidenceRecordsCount => 0;

    public string StatusMessage => "Awaiting Exploration Data";
}
