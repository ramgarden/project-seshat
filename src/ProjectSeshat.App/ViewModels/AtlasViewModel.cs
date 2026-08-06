using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ProjectSeshat.Atlas;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Presentation data for the guided search page (honk - FSS - DSS).</summary>
public sealed class AtlasViewModel : ViewModelBase
{
    private readonly AtlasService? _atlas;
    private readonly IStarSystemRepository? _systemRepository;
    private readonly ICelestialBodyRepository? _bodyRepository;
    private string _summaryText = "No surveyed systems yet. Import journal files to build a search guide.";
    private GuideTarget? _selectedTarget;

    public AtlasViewModel(
        AtlasService? atlas = null,
        IStarSystemRepository? systemRepository = null,
        ICelestialBodyRepository? bodyRepository = null)
    {
        _atlas = atlas;
        _systemRepository = systemRepository;
        _bodyRepository = bodyRepository;
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    public ObservableCollection<GuideTarget> HonkItems { get; } = new();

    public ObservableCollection<GuideTarget> FssItems { get; } = new();

    public ObservableCollection<GuideTarget> DssItems { get; } = new();

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public GuideTarget? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (SetProperty(ref _selectedTarget, value))
            {
                OnPropertyChanged(nameof(SelectedTargetTitle));
                OnPropertyChanged(nameof(SelectedTargetDetail));
            }
        }
    }

    public string SelectedTargetTitle => SelectedTarget?.Title ?? "Nothing selected";

    public string SelectedTargetDetail => SelectedTarget?.Detail ?? "Select an entry above to see where to search next.";

    public ICommand RefreshCommand { get; }

    public void Refresh()
    {
        HonkItems.Clear();
        FssItems.Clear();
        DssItems.Clear();

        if (_atlas is null || _systemRepository is null || _bodyRepository is null)
        {
            SummaryText = "Search guide is unavailable in this context.";
            return;
        }

        var guide = _atlas.BuildSearchGuideAsync(_systemRepository, _bodyRepository).GetAwaiter().GetResult();

        foreach (var target in guide.NeedHonk)
        {
            HonkItems.Add(GuideTarget.Honk(target));
        }

        foreach (var target in guide.NeedFss)
        {
            FssItems.Add(GuideTarget.Fss(target));
        }

        foreach (var target in guide.NeedDss)
        {
            DssItems.Add(GuideTarget.Dss(target));
        }

        SummaryText = $"Next: {guide.HonkCount} system{(guide.HonkCount == 1 ? "" : "s")} to honk, then {guide.FssCount} to FSS, and {guide.DssCount} bod{(guide.DssCount == 1 ? "y" : "ies")} worth a DSS scan.";
    }

    private sealed class RelayCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}

public sealed record GuideTarget(string Title, string Subtitle, string Detail)
{
    public static GuideTarget Honk(HonkTarget t)
    {
        var distance = t.DistanceLy is null ? "position unknown" : $"{(int)t.DistanceLy.Value:N0} LY away";
        return new GuideTarget(t.SystemName, $"Needs honk \u00b7 {distance}", $"Discovery-scan: {t.SystemName}. {distance} from your survey area.");
    }

    public static GuideTarget Fss(FssTarget t)
    {
        var distance = t.DistanceLy is null ? "position unknown" : $"{(int)t.DistanceLy.Value:N0} LY away";
        var signals = $"{t.Signals} signal{(t.Signals == 1 ? "" : "s")} hinting at something worth resolving";
        return new GuideTarget(t.SystemName, $"Needs FSS \u00b7 {signals}", $"Full Spectrum Scanner: {t.SystemName}. {signals} ({distance}).");
    }

    public static GuideTarget Dss(DssTarget t)
    {
        return new GuideTarget(
            t.BodyName,
            $"{t.SystemName} \u00b7 {(int)t.DistanceLs:N0} ls \u00b7 {t.Reason}",
            $"Detailed Surface Scanner: {t.BodyName} in {t.SystemName}. {(int)t.DistanceLs:N0} ls from arrival. Mapping worth it \u2014 {t.Reason}.");
    }
}
