using System.IO;
using System.Text;
using ProjectSeshat.Core.Contracts;

namespace ProjectSeshat.Journals;

/// <summary>Describes the outcome of a journal scan pass.</summary>
public sealed record JournalScanResult(int FilesChanged, int LinesImported);

/// <summary>
/// Watches the Elite Dangerous journal directory and tail-imports new events as the game
/// writes them, raising <see cref="Imported"/> whenever new journal lines are ingested so
/// the UI can refresh live without manual action.
/// </summary>
public sealed class JournalWatcher : IDisposable
{
    private readonly JournalReader _reader;
    private readonly string _directory;
    private readonly IStarSystemRepository _starSystemRepository;
    private readonly ICommanderRepository _commanderRepository;
    private readonly IEvidenceRepository _evidenceRepository;
    private readonly IJournalImportTrackerRepository? _importTrackerRepository;
    private readonly ICelestialBodyRepository? _celestialBodyRepository;
    private readonly ICodexEntryRepository? _codexEntryRepository;
    private readonly IBeaconRepository? _beaconRepository;
    private readonly INavigationStateRepository? _navigationRepository;
    private readonly Dictionary<string, long> _offsets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _fullyImported = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly object _timerLock = new();
    private readonly byte[] _newline = { (byte)'\n' };

    private FileSystemWatcher? _watcher;
    private Timer? _debounce;

    /// <summary>Raised after a scan pass that imported new journal lines.</summary>
    public event EventHandler<JournalScanResult>? Imported;

    public JournalWatcher(
        string directory,
        JournalReader reader,
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        IJournalImportTrackerRepository? importTrackerRepository = null,
        ICelestialBodyRepository? celestialBodyRepository = null,
        ICodexEntryRepository? codexEntryRepository = null,
        IBeaconRepository? beaconRepository = null,
        INavigationStateRepository? navigationRepository = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("A journal directory is required.", nameof(directory));
        }

        _directory = directory;
        _reader = reader;
        _starSystemRepository = starSystemRepository;
        _commanderRepository = commanderRepository;
        _evidenceRepository = evidenceRepository;
        _importTrackerRepository = importTrackerRepository;
        _celestialBodyRepository = celestialBodyRepository;
        _codexEntryRepository = codexEntryRepository;
        _beaconRepository = beaconRepository;
        _navigationRepository = navigationRepository;
    }

    /// <summary>Begins watching and runs an initial scan so existing journals are imported immediately.</summary>
    public void Start()
    {
        if (_watcher is not null)
        {
            return;
        }

        if (Directory.Exists(_directory))
        {
            _watcher = new FileSystemWatcher(_directory, "Journal*.log")
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _watcher.Created += OnJournalActivity;
            _watcher.Changed += OnJournalActivity;
            _watcher.Deleted += OnJournalActivity;
        }

        _ = RunInitialScanAsync();
    }

    /// <summary>
    /// Reads any appended bytes of every journal file in the directory since the last scan.
    /// Also used directly by tests, which bypasses the file-system watcher.
    /// </summary>
    public async Task<JournalScanResult> ScanDirectoryAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory))
        {
            return new JournalScanResult(0, 0);
        }

        var files = Directory.EnumerateFiles(_directory, "Journal*.log", SearchOption.TopDirectoryOnly);
        var filesChanged = 0;
        var linesImported = 0;

        foreach (var file in files)
        {
            var result = await ScanFileAsync(file, cancellationToken);
            filesChanged += result.FilesChanged;
            linesImported += result.LinesImported;
        }

        return new JournalScanResult(filesChanged, linesImported);
    }

    /// <summary>
    /// Scans a single journal file. The first time a file is seen it is imported in full with
    /// duplicate / already-loaded checks (content fingerprint and path). After that, only the
    /// lines appended since the previous scan are imported, using tracked byte offsets so
    /// re-scans stay cheap and never replay events.
    /// </summary>
    public async Task<JournalScanResult> ScanFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            lock (_offsets)
            {
                _offsets.Remove(path);
            }

            _fullyImported.Remove(path);
            return new JournalScanResult(0, 0);
        }

        long offset;
        lock (_offsets)
        {
            if (!_offsets.TryGetValue(path, out offset) || offset < 0 || info.Length < offset)
            {
                offset = 0;
            }

            if (info.Length == offset)
            {
                return new JournalScanResult(0, 0);
            }
        }

        // First touch: import the file head-to-tail once, honouring duplicate / already-loaded
        // checks so re-runs never double-ingest previously imported content.
        if (offset == 0)
        {
            return await ImportWholeFileAsync(path, info.Length, cancellationToken);
        }

        // Subsequent touches: import only the lines appended since the last committed offset.
        var lines = await ReadNewLinesAsync(path, cancellationToken);
        if (lines.Count == 0)
        {
            return new JournalScanResult(0, 0);
        }

        using var text = new StringReader(string.Join('\n', lines));
        await _reader.ImportAsync(
            text,
            _starSystemRepository,
            _commanderRepository,
            _evidenceRepository,
            beaconRepository: _beaconRepository,
            celestialBodyRepository: _celestialBodyRepository,
            codexEntryRepository: _codexEntryRepository,
            navigationRepository: _navigationRepository,
            cancellationToken: cancellationToken);

        return new JournalScanResult(1, lines.Count);
    }

    private async Task<JournalScanResult> ImportWholeFileAsync(string path, long length, CancellationToken cancellationToken)
    {
        lock (_fullyImported)
        {
            if (_fullyImported.Contains(path))
            {
                lock (_offsets)
                {
                    _offsets[path] = length;
                }

                return new JournalScanResult(0, 0);
            }

            _fullyImported.Add(path);
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        var fingerprint = JournalReader.ComputeFingerprint(content);

        // Skip files whose content or path was already imported on a previous run.
        if (_importTrackerRepository is not null && fingerprint.Length > 0)
        {
            var alreadyImported = await _importTrackerRepository.HasImportedByFingerprintAsync(fingerprint, cancellationToken);
            if (!alreadyImported && !string.IsNullOrWhiteSpace(path))
            {
                alreadyImported = await _importTrackerRepository.HasImportedAsync(path, cancellationToken);
            }

            if (alreadyImported)
            {
                lock (_offsets)
                {
                    _offsets[path] = length;
                }

                return new JournalScanResult(0, 0);
            }
        }

        var lines = content
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (lines.Count == 0)
        {
            lock (_offsets)
            {
                _offsets[path] = length;
            }

            return new JournalScanResult(0, 0);
        }

        await _reader.ImportAsync(
            new StringReader(content),
            _starSystemRepository,
            _commanderRepository,
            _evidenceRepository,
            importTrackerRepository: _importTrackerRepository,
            filePath: path,
            cancellationToken: cancellationToken,
            celestialBodyRepository: _celestialBodyRepository,
            codexEntryRepository: _codexEntryRepository,
            beaconRepository: _beaconRepository,
            contentFingerprint: fingerprint,
            navigationRepository: _navigationRepository);

        lock (_offsets)
        {
            _offsets[path] = length;
        }

        return new JournalScanResult(1, lines.Count);
    }

    private async Task<List<string>> ReadNewLinesAsync(string path, CancellationToken cancellationToken)
    {
        var result = new List<string>();
        long offset;
        lock (_offsets)
        {
            offset = _offsets.TryGetValue(path, out var known) ? known : 0;
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (offset > stream.Length)
        {
            offset = 0;
        }

        stream.Seek(offset, SeekOrigin.Begin);
        var remaining = (int)(stream.Length - offset);
        var buffer = new byte[remaining];
        var read = await stream.ReadAsync(buffer.AsMemory(0, remaining), cancellationToken);

        // Walk the buffer by newline bytes so committed offsets stay byte-accurate even when a
        // line is mid-write or spans a multi-byte character.
        var committed = 0;
        var lineStart = 0;
        while (lineStart < read)
        {
            var newlineIndex = Array.IndexOf(buffer, _newline[0], lineStart, read - lineStart);
            if (newlineIndex < 0)
            {
                // No trailing newline: the final line is still being written. Skip it and let
                // the next scan pick it up once complete.
                break;
            }

            var lineEnd = newlineIndex;
            if (lineEnd > lineStart && buffer[lineEnd - 1] == (byte)'\r')
            {
                lineEnd--;
            }

            var lineText = Encoding.UTF8.GetString(buffer, lineStart, lineEnd - lineStart);
            var trimmed = lineText.Trim();
            if (trimmed.Length > 0)
            {
                result.Add(trimmed);
            }

            committed = newlineIndex + 1;
            lineStart = newlineIndex + 1;
        }

        if (committed > 0)
        {
            lock (_offsets)
            {
                _offsets[path] = offset + committed;
            }
        }

        return result;
    }

    private void OnJournalActivity(object sender, FileSystemEventArgs e)
    {
        lock (_timerLock)
        {
            _debounce?.Dispose();
            _debounce = new Timer(
                _ => _ = ScanAndNotifyAsync(),
                null,
                TimeSpan.FromMilliseconds(500),
                Timeout.InfiniteTimeSpan);
        }
    }

    private async Task RunInitialScanAsync()
    {
        try
        {
            await ScanAndNotifyAsync();
        }
        catch (Exception)
        {
            // A transient IO error during startup should not crash the app.
        }
    }

    private async Task ScanAndNotifyAsync()
    {
        if (!await _gate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var result = await ScanDirectoryAsync(_cts.Token);
            if (result.LinesImported > 0)
            {
                Imported?.Invoke(this, result);
            }
        }
        catch (OperationCanceledException)
        {
            // Watcher disposed.
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Clears internal tracking state and triggers a full re-scan of all journal files.
    /// Useful after database reset or when import tracker was cleared.
    /// </summary>
    public async Task<JournalScanResult> ResetAndRescanAsync(CancellationToken cancellationToken = default)
    {
        lock (_offsets)
        {
            _offsets.Clear();
        }
        lock (_fullyImported)
        {
            _fullyImported.Clear();
        }
        return await ScanDirectoryAsync(cancellationToken);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _debounce?.Dispose();
        if (_watcher is not null)
        {
            _watcher.Created -= OnJournalActivity;
            _watcher.Changed -= OnJournalActivity;
            _watcher.Deleted -= OnJournalActivity;
            _watcher.Dispose();
            _watcher = null;
        }

        _gate.Dispose();
    }
}