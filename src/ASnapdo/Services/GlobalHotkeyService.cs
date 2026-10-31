using System.Runtime.InteropServices;

namespace ASnapdo.Services;

public sealed record HotkeyAvailability(bool Available, string Message);

public sealed class GlobalHotkeyService(IntPtr handle) : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private readonly Dictionary<int, Keys> registered = [];
    private static bool Valid(Keys keys) => (keys & Keys.KeyCode) is not (Keys.None or Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin or Keys.F12)
        && (keys & Keys.KeyCode) <= (Keys)255 && (keys & ~(Keys.KeyCode | Keys.Control | Keys.Shift | Keys.Alt)) == 0;
    private static uint Modifiers(Keys keys)
    {
        uint modifiers = 0x4000;
        if ((keys & Keys.Control) != 0) modifiers |= 2;
        if ((keys & Keys.Shift) != 0) modifiers |= 4;
        if ((keys & Keys.Alt) != 0) modifiers |= 1;
        return modifiers;
    }
    public HotkeyAvailability CheckAvailability(Keys keys)
    {
        const int probeId = 0x3FFF;
        if (registered.ContainsKey(probeId))
        {
            if (!UnregisterHotKey(handle, probeId)) return new(false, "이전 검사 등록을 해제하지 못했습니다.");
            registered.Remove(probeId);
        }
        if (!Valid(keys)) return new(false, "사용할 수 없는 키 조합입니다.");
        if (registered.ContainsValue(keys)) return new(true, "사용 가능 · 현재 이 앱에 등록됨");
        if (!RegisterHotKey(handle, probeId, Modifiers(keys), (uint)(keys & Keys.KeyCode)))
        {
            int error = Marshal.GetLastWin32Error();
            return new(false, $"등록 불가 · 다른 앱 사용 또는 시스템 제한 (오류 {error})");
        }
        // Probe only: do not replace either active capture shortcut.
        if (!UnregisterHotKey(handle, probeId))
        {
            registered[probeId] = keys;
            return new(false, "검사 등록 해제에 실패했습니다. 옵션을 닫고 다시 시도하세요.");
        }
        return new(true, "사용 가능 · Windows 등록 확인");
    }
    public bool Set(int id, Keys keys)
    {
        if (!Valid(keys)) return false;
        if (registered.TryGetValue(id, out var current) && current == keys) return true;
        if (registered.Any(pair => pair.Key != id && pair.Value == keys)) return false;
        var key = keys & Keys.KeyCode;
        if (key is Keys.None or Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return false;
        registered.TryGetValue(id, out var old);
        UnregisterHotKey(handle, id);
        if (RegisterHotKey(handle, id, Modifiers(keys), (uint)key)) { registered[id] = keys; return true; }
        registered.Remove(id);
        if (old != Keys.None) Set(id, old);
        return false;
    }
    public void Dispose() { foreach (var id in registered.Keys) UnregisterHotKey(handle, id); registered.Clear(); }
}
