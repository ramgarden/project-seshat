using System.Windows.Input;
using ProjectSeshat.App.Elite;

namespace ProjectSeshat.App.ViewModels;

/// <summary>
/// Walks the player through setting up Elite Dangerous key bindings so auto-targeting can drive
/// the game. Detects current binds, offers a live test press, and can write a minimal default
/// preset (opt-in, best-effort) or guide the player through ED's in-game Controls screen.
/// </summary>
public sealed class KeybindSetupViewModel : ViewModelBase
{
    private readonly KeyAutomationService _automation;
    private string _statusText = "Checking key bindings…";
    private string _targetKeyText = "—";
    private string _jumpKeyText = "—";
    private string _bindsPathText = "—";
    private string _testResultText = string.Empty;
    private string _writeResultText = string.Empty;
    private bool _allowAutoWrite;

    public KeybindSetupViewModel(KeyAutomationService? automation = null)
    {
        _automation = automation ?? new KeyAutomationService();
        RedetectCommand = new RelayCommand(Redetect);
        TestJumpCommand = new RelayCommand(TestJump);
        WriteDefaultBindingsCommand = new RelayCommand(WriteDefaultBindings);
        Redetect();
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string TargetKeyText
    {
        get => _targetKeyText;
        private set => SetProperty(ref _targetKeyText, value);
    }

    public string JumpKeyText
    {
        get => _jumpKeyText;
        private set => SetProperty(ref _jumpKeyText, value);
    }

    public string BindsPathText
    {
        get => _bindsPathText;
        private set => SetProperty(ref _bindsPathText, value);
    }

    public string TestResultText
    {
        get => _testResultText;
        private set => SetProperty(ref _testResultText, value);
    }

    public string WriteResultText
    {
        get => _writeResultText;
        private set => SetProperty(ref _writeResultText, value);
    }

    public bool IsReady => _automation.CanAutoTarget;

    public bool AllowAutoWrite
    {
        get => _allowAutoWrite;
        set
        {
            if (SetProperty(ref _allowAutoWrite, value))
            {
                WriteResultText = string.Empty;
            }
        }
    }

    public string ReadySummary => IsReady
        ? "Auto-target is ready. Enable it from the sidebar and it will jump for you — just confirm the FSD charge."
        : "Auto-target can't drive the game yet. Fix the bindings below, then re-detect.";

    public ICommand RedetectCommand { get; }

    public ICommand TestJumpCommand { get; }

    public ICommand WriteDefaultBindingsCommand { get; }

    private void Redetect()
    {
        _automation.TryEnable();
        TargetKeyText = _automation.TargetKey?.Replace("Key_", "") ?? "—";
        JumpKeyText = _automation.JumpKey?.Replace("Key_", "") ?? "—";
        BindsPathText = _automation.BindingsFilePath ?? (string.IsNullOrEmpty(_automation.BindingsDirectory) ? "Not found" : "Bindings folder exists, no usable file");
        StatusText = _automation.StatusText;
        TestResultText = string.Empty;
        WriteResultText = string.Empty;

        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(ReadySummary));
        OnPropertyChanged(nameof(CanTest));
        OnPropertyChanged(nameof(CanWriteDefaults));
    }

    private void TestJump()
    {
        var sent = _automation.TestJump();
        TestResultText = sent
            ? "Transmitted — check the game engaged the FSD drive."
            : "Not sent — make sure Elite Dangerous is focused and a jump key is bound, then try again.";
    }

    private void WriteDefaultBindings()
    {
        if (!AllowAutoWrite)
        {
            WriteResultText = "Tick the confirmation box above first, then press Write default binds.";
            return;
        }

        try
        {
            var writer = new BindingsWriter();
            var path = writer.WriteDefaultBindings(_automation.BindingsDirectory);
            WriteResultText = $"Wrote {System.IO.Path.GetFileName(path)}. Restart Elite Dangerous, then re-detect.";
            Redetect();
        }
        catch (Exception ex)
        {
            WriteResultText = $"Could not write bindings: {ex.Message}";
        }
    }

    public bool CanTest => IsReady && !string.IsNullOrEmpty(JumpKeyText) && JumpKeyText != "—";

    public bool CanWriteDefaults => (string.IsNullOrEmpty(JumpKeyText) || JumpKeyText == "—") && !IsReady;

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