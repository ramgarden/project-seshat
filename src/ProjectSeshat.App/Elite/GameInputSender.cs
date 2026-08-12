using System.Runtime.InteropServices;

namespace ProjectSeshat.App.Elite;

/// <summary>
/// Sends synthetic key input to the Elite Dangerous window. Abstracted so the real Windows
/// <c>SendInput</c> implementation can be swapped for a fake in offline unit tests.
/// </summary>
public interface IGameInputSender
{
    /// <summary>True when Elite Dangerous is the foreground window, so automation only runs during play.</summary>
    bool IsEliteInForeground { get; }

    /// <summary>Presses and releases a key, holding an optional modifier (left Alt/Shift/Ctrl).</summary>
    void Press(string key, string? modifier = null);
}

/// <summary>Windows <c>SendInput</c>-based sender. Only meaningful on Windows with a game present.</summary>
public sealed class SendInputGameInputSender : IGameInputSender
{
    public bool IsEliteInForeground
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                var hwnd = GetForegroundWindow();
                var length = GetWindowTextLength(hwnd);
                if (length <= 0)
                {
                    return false;
                }

                var title = new string('\0', length + 1);
                _ = GetWindowText(hwnd, title, title.Length);
                var text = title.TrimEnd('\0');
                return text.Contains("Elite:Dangerous", StringComparison.OrdinalIgnoreCase) ||
                       text.Contains("Elite Dangerous", StringComparison.OrdinalIgnoreCase) ||
                       text.Contains("ED", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    public void Press(string key, string? modifier = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var vkModifier = KeyCode(modifier);
        var vk = KeyCode(key);
        if (vk == 0)
        {
            return;
        }

        try
        {
            if (vkModifier != 0)
            {
                SendInputKey(vkModifier, keyUp: false);
            }

            SendInputKey(vk, keyUp: false);
            SendInputKey(vk, keyUp: true);

            if (vkModifier != 0)
            {
                SendInputKey(vkModifier, keyUp: true);
            }
        }
        catch
        {
            // Best-effort; never let input synthesis crash the app.
        }
    }

    /// <summary>Maps an ED key name (e.g. "Key_J") to a virtual-key code. Returns 0 when unknown.</summary>
    private static ushort KeyCode(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return 0;
        }

        var k = key.Trim();

        // Keys come as "Key_A".."Key_Z", "Key_0".."Key_9", or plain codes.
        if (k.StartsWith("Key_", StringComparison.OrdinalIgnoreCase))
        {
            k = k.Substring(4);
        }

        if (k.Length == 1)
        {
            var c = char.ToUpperInvariant(k[0]);
            if (c is >= 'A' and <= 'Z')
            {
                return (ushort)c; // 'A' = 0x41 etc.; matches VK_A..VK_Z
            }

            if (c is >= '0' and <= '9')
            {
                return (ushort)c; // VK_0..VK_9
            }
        }

        return k.ToLowerInvariant() switch
        {
            "space" => 0x20,
            "enter" or "return" => 0x0D,
            "escape" or "esc" => 0x1B,
            "tab" => 0x09,
            "lshift" or "leftshift" => 0xA0,
            "rshift" or "rightshift" => 0xA1,
            "lctrl" or "leftctrl" => 0xA2,
            "rctrl" or "rightctrl" => 0xA3,
            "lalt" or "leftalt" => 0xA4,
            "ralt" or "rightalt" => 0xA5,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            "plus" => 0xBB,
            "minus" => 0xBD,
            _ => 0
        };
    }

    private static void SendInputKey(ushort vk, bool keyUp)
    {
        var input = new INPUT
        {
            type = 1, // INPUT_KEYBOARD
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = keyUp ? 0x0002u : 0u, // KEYEVENTF_KEYUP
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, string lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
