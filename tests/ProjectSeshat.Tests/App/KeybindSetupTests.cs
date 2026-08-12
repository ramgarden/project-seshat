using ProjectSeshat.App.Elite;
using ProjectSeshat.App.ViewModels;
using Xunit;

namespace ProjectSeshat.Tests.App;

public sealed class StartPresetResolverTests
{
    [Fact]
    public void ResolveActiveBindingsFile_PrefersStartPreset()
    {
        var root = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(root, "StartPreset.start"), "Custom.4.0");
            File.WriteAllText(Path.Combine(root, "Default.binds"), "<Root />");
            File.WriteAllText(Path.Combine(root, "Custom.4.0.binds"), "<Root />");

            var resolved = StartPresetResolver.ResolveActiveBindingsFile(root);

            Assert.Equal(Path.Combine(root, "Custom.4.0.binds"), resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveActiveBindingsFile_NoMarker_FallsBackToFirstBinds()
    {
        var root = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(root, "b.binds"), "<Root />");
            File.WriteAllText(Path.Combine(root, "a.binds"), "<Root />");

            var resolved = StartPresetResolver.ResolveActiveBindingsFile(root);

            Assert.Equal(Path.Combine(root, "a.binds"), resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveActiveBindingsFile_MissingDirReturnsNull()
        => Assert.Null(StartPresetResolver.ResolveActiveBindingsFile(Path.Combine(TempDir(), "nope")));

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "seshat-startpreset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public sealed class BindingsWriterTests
{
    [Fact]
    public void WriteDefaultBindings_CreatesFileAndStartPreset()
    {
        var root = TempDir();
        try
        {
            var writer = new BindingsWriter();

            var path = writer.WriteDefaultBindings(root);

            Assert.True(File.Exists(path));
            Assert.EndsWith(".binds", path, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Path.Combine(root, "StartPreset.start"), Path.Combine(root, "StartPreset.start"));

            var marker = File.ReadAllText(Path.Combine(root, "StartPreset.start")).Trim();
            Assert.NotEmpty(marker);

            // The written file must parse back to our two keyboard actions.
            var xml = File.ReadAllText(path);
            var bindings = BindingsParser.Parse(xml);
            var target = BindingsParser.FindKeyboard(bindings, KeyAutomationService.TargetBindingName);
            var jump = BindingsParser.FindKeyboard(bindings, KeyAutomationService.JumpBindingName);
            Assert.NotNull(target);
            Assert.NotNull(jump);
            Assert.Equal("Key_T", target!.Key);
            Assert.Equal("Key_J", jump!.Key);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HasBindings_TrueWhenBindingsExist()
    {
        var root = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(root, "Default.binds"), "<Root />");
            var writer = new BindingsWriter();
            Assert.True(writer.HasBindings(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WriteDefaultBindings_IsIdempotent()
    {
        var root = TempDir();
        try
        {
            var writer = new BindingsWriter();
            writer.WriteDefaultBindings(root);
            var first = new FileInfo(Path.Combine(root, "Seshat.Auto.4.0.binds")).Length;
            writer.WriteDefaultBindings(root);
            var second = new FileInfo(Path.Combine(root, "Seshat.Auto.4.0.binds")).Length;
            Assert.Equal(first, second);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "seshat-writer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public sealed class KeybindSetupViewModelTests
{
    [Fact]
    public void Redetect_Ready_ShowsKeys()
    {
        var (dir, _) = WriteBinds();
        try
        {
            var vm = new KeybindSetupViewModel(new KeyAutomationService(
                new BindingsPathResolver(new[] { dir }),
                new FakeInput(),
                path => File.Exists(path) ? File.ReadAllText(path) : null));

            Assert.True(vm.IsReady);
            Assert.Equal("T", vm.TargetKeyText);
            Assert.Equal("J", vm.JumpKeyText);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Redetect_NotReady_ShowsPlaceholders()
    {
        var vm = new KeybindSetupViewModel(new KeyAutomationService(
            new BindingsPathResolver(new[] { Path.Combine(TempDir(), "missing") }),
            new FakeInput(),
            path => null));

        Assert.False(vm.IsReady);
        Assert.Equal("—", vm.JumpKeyText);
    }

    [Fact]
    public void TestJump_GameNotFocused_ReportsNotSent()
    {
        var (dir, _) = WriteBinds();
        try
        {
            var vm = new KeybindSetupViewModel(new KeyAutomationService(
                new BindingsPathResolver(new[] { dir }),
                new FakeInput { EliteInForeground = false },
                path => File.Exists(path) ? File.ReadAllText(path) : null));

            vm.TestJumpCommand.Execute(null);

            Assert.Contains("Not sent", vm.TestResultText);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TestJump_GameFocused_ReportsTransmitted()
    {
        var (dir, _) = WriteBinds();
        try
        {
            var fakeInput = new KeystrokeRecordingInput { EliteInForeground = true };
            var vm = new KeybindSetupViewModel(new KeyAutomationService(
                new BindingsPathResolver(new[] { dir }),
                fakeInput,
                path => File.Exists(path) ? File.ReadAllText(path) : null));

            vm.TestJumpCommand.Execute(null);

            Assert.Contains("Transmitted", vm.TestResultText);
            Assert.Contains("Key_J", fakeInput.Pressed);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static (string Dir, string File) WriteBinds()
    {
        var dir = Path.Combine(Path.GetTempPath(), "seshat-setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "Custom.binds");
        File.WriteAllText(file, """
            <?xml version="1.0" encoding="UTF-8" ?>
            <Root PresetName="Custom">
              <HyperSuperCombination Primary="Keyboard" Device="" Key="Key_J" />
              <SelectTarget Primary="Keyboard" Device="" Key="Key_T" />
            </Root>
            """);
        return (dir, file);
    }

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "seshat-setup-" + Guid.NewGuid().ToString("N"));

    private sealed class FakeInput : IGameInputSender
    {
        public bool EliteInForeground { get; set; }
        public bool IsEliteInForeground => EliteInForeground;
        public void Press(string key, string? modifier = null)
        {
        }
    }

    private sealed class KeystrokeRecordingInput : IGameInputSender
    {
        public bool EliteInForeground { get; set; }
        public bool IsEliteInForeground => EliteInForeground;
        public List<string> Pressed { get; } = new();
        public void Press(string key, string? modifier = null) => Pressed.Add(key);
    }
}
