using ProjectSeshat.App.Elite;
using ProjectSeshat.Atlas;
using Xunit;

namespace ProjectSeshat.Tests.App;

public sealed class BindingsParserTests
{
    private const string SampleBinds = """
        <?xml version="1.0" encoding="UTF-8" ?>
        <Root PresetName="Custom">
          <MouseXMode Value="Yaw" />
          <HyperSuperCombination Primary="Keyboard" Secondary="Keyboard2" Device="" Key="Key_J" />
          <SelectTarget Primary="Keyboard" Device="" Key="Key_T" />
          <DriveAssist Primary="Joystick" Device="" Key="Joy_1" />
          <Unbound Primary="Keyboard" Device="" Key="" />
        </Root>
        """;

    [Fact]
    public void Parse_ExtractsKeyboardBindingsWithKeys()
    {
        var bindings = BindingsParser.Parse(SampleBinds);

        Assert.Contains(bindings, b => b.Name == "HyperSuperCombination" && b.IsKeyboard && b.Key == "Key_J");
        Assert.Contains(bindings, b => b.Name == "SelectTarget" && b.IsKeyboard && b.Key == "Key_T");
    }

    [Fact]
    public void Parse_SkipsEmptyKeysAndNonKeyboardDevices()
    {
        var bindings = BindingsParser.Parse(SampleBinds);

        Assert.DoesNotContain(bindings, b => b.Name == "Unbound" && !string.IsNullOrEmpty(b.Key));
        Assert.Contains(bindings, b => b.Name == "DriveAssist" && !b.IsKeyboard);
    }

    [Fact]
    public void FindKeyboard_ReturnsBindingIgnoreCase()
    {
        var bindings = BindingsParser.Parse(SampleBinds);

        var jump = BindingsParser.FindKeyboard(bindings, "HYPERSUPERCOMBINATION");

        Assert.NotNull(jump);
        Assert.Equal("Key_J", jump!.Key);
    }

    [Fact]
    public void FindKeyboard_MissingReturnsNull()
    {
        var bindings = BindingsParser.Parse(SampleBinds);

        Assert.Null(BindingsParser.FindKeyboard(bindings, "NotARealBinding"));
    }

    [Fact]
    public void Parse_MalformedXmlReturnsEmpty()
    {
        Assert.Empty(BindingsParser.Parse("this is { not xml"));
        Assert.Empty(BindingsParser.Parse(""));
    }
}

public sealed class BindingsPathResolverTests
{
    [Fact]
    public void ResolveBindingsFile_PicksFirstBindsInAnyCandidate()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "seshat-binds-" + System.Guid.NewGuid().ToString("N"));
        var bindingsDir = System.IO.Path.Combine(root, "Options", "Bindings");
        System.IO.Directory.CreateDirectory(bindingsDir);
        var file = System.IO.Path.Combine(bindingsDir, "Custom.4.0.binds");
        System.IO.File.WriteAllText(file, "<Root />");

        try
        {
            var resolver = new BindingsPathResolver(new[] { bindingsDir });
            Assert.Equal(file, resolver.ResolveBindingsFile());
        }
        finally
        {
            System.IO.Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveBindingsFile_NoMatchesReturnsNull()
    {
        var resolver = new BindingsPathResolver(new[] { "C:\\definitely\\not\\here" });
        Assert.Null(resolver.ResolveBindingsFile());
    }
}

public sealed class KeyAutomationServiceTests
{
    private const string BindsXml = """
        <?xml version="1.0" encoding="UTF-8" ?>
        <Root PresetName="Custom">
          <HyperSuperCombination Primary="Keyboard" Device="" Key="Key_J" />
          <SelectTarget Primary="Keyboard" Device="" Key="Key_T" />
        </Root>
        """;

    private static KeyAutomationService NewService(FakeInput input, string? bindsDir, string? xml = null)
    {
        string? ReadFile(string path) => File.Exists(path) ? xml : null;
        var resolver = new BindingsPathResolver(bindsDir is null ? Enumerable.Empty<string>() : new[] { bindsDir });
        return new KeyAutomationService(resolver, input, ReadFile);
    }

    [Fact]
    public void TryEnable_DiscoversKeysFromBindings()
    {
        var input = new FakeInput { EliteInForeground = true };
        var (bindsDir, _) = CreateTempBinds(BindsXml);
        try
        {
            var service = NewService(input, bindsDir, BindsXml);

            Assert.True(service.TryEnable());
            Assert.True(service.CanAutoTarget);
            Assert.Equal("Key_J", service.JumpKey);
            Assert.Equal("Key_T", service.TargetKey);
            Assert.Contains("auto-target", service.StatusText, System.StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(bindsDir, recursive: true);
        }
    }

    [Fact]
    public void TryEnable_MissingBindingsDegradesGracefully()
    {
        var input = new FakeInput();
        var service = NewService(input, null, null);

        Assert.False(service.TryEnable());
        Assert.False(service.CanAutoTarget);
        Assert.Contains("overlay names the star", service.StatusText, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AutoTargetNextStar_SendsTargetThenJump_WhenArmedAndFocused()
    {
        var input = new FakeInput { EliteInForeground = true };
        var (bindsDir, _) = CreateTempBinds(BindsXml);
        try
        {
            var service = NewService(input, bindsDir, BindsXml);
            service.TryEnable();

            service.AutoTargetNextStar(new CrawlStep("Jump", "Sol", "Nearest unsearched star"));

            Assert.Contains("Key_T", input.Pressed);
            Assert.Contains("Key_J", input.Pressed);
        }
        finally
        {
            Directory.Delete(bindsDir, recursive: true);
        }
    }

    [Fact]
    public void AutoTargetNextStar_NotArmed_NoInputSent()
    {
        var input = new FakeInput { EliteInForeground = true };
        var (bindsDir, _) = CreateTempBinds(BindsXml);
        try
        {
            var service = NewService(input, bindsDir, null);
            service.TryEnable();

            service.AutoTargetNextStar(new CrawlStep("Jump", "Sol", "reason"));

            Assert.Empty(input.Pressed);
        }
        finally
        {
            Directory.Delete(bindsDir, recursive: true);
        }
    }

    [Fact]
    public void AutoTargetNextStar_Fss_TargetsTheSignal()
    {
        var input = new FakeInput { EliteInForeground = true };
        var (bindsDir, _) = CreateTempBinds(BindsXml);
        try
        {
            var service = NewService(input, bindsDir, BindsXml);
            service.TryEnable();

            service.AutoTargetNextStar(new CrawlStep("FSS", "Sol", "resolve signals"));

            Assert.Contains("Key_T", input.Pressed);
            Assert.DoesNotContain("Key_J", input.Pressed);
        }
        finally
        {
            Directory.Delete(bindsDir, recursive: true);
        }
    }

    [Fact]
    public void AutoTargetNextStar_Dss_TargetsTheBody()
    {
        var input = new FakeInput { EliteInForeground = true };
        var (bindsDir, _) = CreateTempBinds(BindsXml);
        try
        {
            var service = NewService(input, bindsDir, BindsXml);
            service.TryEnable();

            service.AutoTargetNextStar(new CrawlStep("DSS", "Sol 1", "Terraformable world"));

            Assert.Contains("Key_T", input.Pressed);
            Assert.DoesNotContain("Key_J", input.Pressed);
        }
        finally
        {
            Directory.Delete(bindsDir, recursive: true);
        }
    }

    [Fact]
    public void AutoTargetNextStar_GameNotFocused_NoInputSent()
    {
        var input = new FakeInput { EliteInForeground = false };
        var (bindsDir, _) = CreateTempBinds(BindsXml);
        try
        {
            var service = NewService(input, bindsDir, BindsXml);
            service.TryEnable();

            service.AutoTargetNextStar(new CrawlStep("Jump", "Sol", "reason"));

            Assert.Empty(input.Pressed);
        }
        finally
        {
            Directory.Delete(bindsDir, recursive: true);
        }
    }

    private static (string Dir, string File) CreateTempBinds(string xml)
    {
        var dir = Path.Combine(Path.GetTempPath(), "seshat-ed-binds-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "Custom.4.0.binds");
        File.WriteAllText(file, xml);
        return (dir, file);
    }

    private sealed class FakeInput : IGameInputSender
    {
        public bool EliteInForeground { get; set; }

        public List<string> Pressed { get; } = new();

        public bool IsEliteInForeground => EliteInForeground;

        public void Press(string key, string? modifier = null) => Pressed.Add(key);
    }
}
