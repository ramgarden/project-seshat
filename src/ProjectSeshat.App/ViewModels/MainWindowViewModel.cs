using ProjectSeshat.Core.Contracts;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Presentation data for the initial research dashboard.</summary>
public sealed class MainWindowViewModel
{
    private readonly IStarSystemRepository _starSystemRepository;
    private readonly ICommanderRepository _commanderRepository;
    private readonly IEvidenceRepository _evidenceRepository;

    public MainWindowViewModel(
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository)
    {
        _starSystemRepository = starSystemRepository;
        _commanderRepository = commanderRepository;
        _evidenceRepository = evidenceRepository;
    }

    public string WindowTitle => "Project Seshat - Galactic Research Platform";

    public string ProjectName => "PROJECT SESHAT";

    public string PlatformName => "Galactic Research Platform";

    public string StatisticsHeading => "STATISTICS";

    public string SystemsIndexedLabel => "SYSTEMS INDEXED";

    public int SystemsIndexedCount => _starSystemRepository.CountAsync().GetAwaiter().GetResult();

    public string CommanderRecordsLabel => "COMMANDER RECORDS";

    public int CommanderRecordsCount => _commanderRepository.CountAsync().GetAwaiter().GetResult();

    public string EvidenceRecordsLabel => "EVIDENCE RECORDS";

    public int EvidenceRecordsCount => _evidenceRepository.CountAsync().GetAwaiter().GetResult();

    public string StatusMessage => "Persistence is live";
}
