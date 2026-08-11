using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using ProjectSeshat.App.ViewModels;
using ProjectSeshat.Atlas;

namespace ProjectSeshat.App.Views;

/// <summary>
/// A small always-on-top caption that floats over the game while playing. Transparent background,
/// no chrome, no taskbar entry, and click-through on Windows (WS_EX_TRANSPARENT) so it never
/// swallows input. Shows the live outward-crawl step.
/// </summary>
public sealed class GuidanceOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly GuidanceOverlayViewModel _viewModel;
    private bool _clickThroughApplied;

    public GuidanceOverlayWindow(GuidanceOverlayViewModel viewModel)
    {
        _viewModel = viewModel;
        Content = new GuidanceOverlayView();
        DataContext = viewModel;

        Title = "Project Seshat - Guidance";
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        Width = 520;
        Height = 110;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };

        Opened += OnOpened;
    }

    public void Update(CrawlStep? step)
    {
        _viewModel.SetStep(step);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        PositionAtBottomCenter();
        ApplyClickThrough();
    }

    private void PositionAtBottomCenter()
    {
        var screens = Screens.All;
        if (screens.Count == 0)
        {
            return;
        }

        var screen = screens[0];
        var workingArea = screen.WorkingArea;
        Position = new PixelPoint(
            workingArea.X + (workingArea.Width - (int)Width) / 2,
            workingArea.Y + workingArea.Height - (int)Height - 8);
    }

    private void ApplyClickThrough()
    {
        if (!OperatingSystem.IsWindows() || _clickThroughApplied)
        {
            return;
        }

        try
        {
            var handle = TryGetPlatformHandle()?.Handle;
            if (handle.HasValue && handle.Value != IntPtr.Zero)
            {
                var style = GetWindowLongPtr(handle.Value, GwlExStyle);
                SetWindowLongPtr(handle.Value, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
                _clickThroughApplied = true;
            }
        }
        catch
        {
            // Best-effort; a non-click-through overlay is still usable.
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}