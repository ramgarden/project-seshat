using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using ProjectSeshat.App.ViewModels;
using ProjectSeshat.Atlas;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.App.Views;

/// <summary>
/// A small always-on-top caption over the game. Fully transparent (only the words and a tiny drag
/// grip render), no chrome, no taskbar entry. On Windows the whole window is click-through
/// (WM_NCHITTEST → HTTRANSPARENT) except for a small grip region (HTCAPTION), which lets the
/// player grab and reposition it without the caption blocking the cockpit. Shows the live crawl step.
/// </summary>
public sealed class GuidanceOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private const int WmNcHitTest = 0x0084;
    private const int WmMove = 0x0003;
    private const int WmExitSizeMove = 0x0232;
    private const int HtTransparent = -1;
    private const int HtCaption = 2;

    private const int GripWidthPx = 52;
    private const int GripHeightPx = 34;

    private readonly GuidanceOverlayViewModel _viewModel;
    private readonly OverlayPositionStore _positionStore;
    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _subclassId = new(1);
    private bool _hooked;
    private bool _restored;

    private static WinProcDelegate? s_winProc; // keep delegate alive while subclassed

    public GuidanceOverlayWindow(GuidanceOverlayViewModel viewModel, OverlayPositionStore? positionStore = null)
    {
        _viewModel = viewModel;
        _positionStore = positionStore ?? new OverlayPositionStore();
        Content = new GuidanceOverlayView();
        DataContext = viewModel;

        Title = "Project Seshat - Guidance";
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        Width = 520;
        Height = 96;
        // The window itself must be transparent (not just its content) or the theme's default
        // grey/white background paints behind the caption, showing an opaque box.
        Background = Avalonia.Media.Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };

        Opened += OnOpened;
        PositionChanged += OnPositionChanged;
        Closed += OnClosed;
    }

    public void Update(NextAction? action) => _viewModel.SetStep(action);

    private void OnOpened(object? sender, EventArgs e)
    {
        // Reopen where the player left it last session (default to bottom-centre on first run).
        var saved = _positionStore.Load();
        if (saved is { } pos)
        {
            Position = new PixelPoint(pos.X, pos.Y);
        }
        else
        {
            PositionAtBottomCenter();
        }

        _restored = true;
        HookNcHitTest();
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        // Belt-and-braces: save on Avalonia's event too. The Win32 WM_MOVE path is the primary
        // one, so every save goes through the native window rect (single source of truth) — never
        // through the stale Avalonia Position property, which native HTCAPTION drags don't update.
        if (_restored)
        {
            SaveFromNativeRect();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // One final save so the last dragged position is kept even if a drag ended on close.
        if (_restored)
        {
            SaveFromNativeRect();
        }

        UnhookNcHitTest();
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

    private void HookNcHitTest()
    {
        if (!OperatingSystem.IsWindows() || _hooked)
        {
            return;
        }

        try
        {
            var handle = TryGetPlatformHandle()?.Handle;
            if (handle is null || handle.Value == IntPtr.Zero)
            {
                return;
            }

            _hwnd = handle.Value;
            s_winProc = WinProc; // must stay alive for the lifetime of the subclass
            _hooked = SetWindowSubclass(_hwnd, s_winProc, _subclassId, IntPtr.Zero);

            // Suppress activation on click so the game keeps focus.
            var style = GetWindowLongPtr(_hwnd, GwlExStyle);
            SetWindowLongPtr(_hwnd, GwlExStyle, style | WsExToolWindow | WsExNoActivate);
        }
        catch
        {
            // Best-effort; a plain topmost caption is still usable.
        }
    }

    private void UnhookNcHitTest()
    {
        if (!OperatingSystem.IsWindows() || !_hooked || _hwnd == IntPtr.Zero || s_winProc is null)
        {
            return;
        }

        try
        {
            RemoveWindowSubclass(_hwnd, s_winProc, _subclassId);
        }
        catch
        {
            // Best-effort cleanup.
        }

        _hooked = false;
    }

    private IntPtr WinProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (msg == WmNcHitTest)
        {
            // lParam packs the screen coordinates of the cursor: low word = x, high word = y.
            var screenX = (short)(lParam.ToInt64() & 0xFFFF);
            var screenY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

            var clientPoint = ScreenToClient(hWnd, screenX, screenY);
            if (clientPoint.X is >= 0 and < GripWidthPx && clientPoint.Y is >= 0 and < GripHeightPx)
            {
                return new IntPtr(HtCaption); // grab to drag (also lets us keep no-activate)
            }

            return new IntPtr(HtTransparent); // click passes through to the game
        }

        if (msg is WmMove or WmExitSizeMove)
        {
            // Persist immediately as the window moves, straight from the native message loop.
            // This covers native HTCAPTION drags (which don't raise Avalonia's PositionChanged
            // reliably) and survives a crash after a move.
            SaveFromNativeRect();
            return DefSubclassProc(hWnd, msg, wParam, lParam);
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private void SaveFromNativeRect()
    {
        if (!_restored || _hwnd == IntPtr.Zero)
        {
            return; // ignore the transient position set while restoring
        }

        try
        {
            // Read the real OS position (native HTCAPTION drags move the OS window but don't
            // update Avalonia's Position), so this never writes a stale 0,0.
            if (GetWindowRect(_hwnd, out var rect))
            {
                _positionStore.Save(rect.Left, rect.Top);
            }
        }
        catch
        {
            // Best-effort; never let position saving take down the app.
        }
    }

    private static (int X, int Y) ScreenToClient(IntPtr hWnd, int screenX, int screenY)
    {
        var pt = new POINT { X = screenX, Y = screenY };
        _ = ScreenToClient(hWnd, ref pt);
        return (pt.X, pt.Y);
    }

    private delegate IntPtr WinProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("comctl32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, WinProcDelegate pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, WinProcDelegate pfnSubclass, IntPtr uIdSubclass);
}