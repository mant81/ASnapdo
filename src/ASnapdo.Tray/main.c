#define UNICODE
#define _UNICODE
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <shlobj.h>
#include <string.h>
// Never silently truncate or overflow paths.
static void CopyTextChecked(WCHAR* target, size_t capacity, const WCHAR* source) {
    size_t length = (size_t)lstrlenW(source);
    if (length >= capacity) ExitProcess(ERROR_BUFFER_OVERFLOW);
    for (size_t i = 0; i <= length; i++) target[i] = source[i];
}
static void AppendTextChecked(WCHAR* target, size_t capacity, const WCHAR* source) {
    size_t used = (size_t)lstrlenW(target), length = (size_t)lstrlenW(source);
    if (used >= capacity || length >= capacity - used) ExitProcess(ERROR_BUFFER_OVERFLOW);
    for (size_t i = 0; i <= length; i++) target[used + i] = source[i];
}
#define TEXT_CAPACITY(target) (sizeof(target) / sizeof((target)[0]))
#define CopyText(target, source) CopyTextChecked(target, TEXT_CAPACITY(target), source)
#define AppendText(target, source) AppendTextChecked(target, TEXT_CAPACITY(target), source)

static const WCHAR CLASS_NAME[] = L"ASnapdo.Tray";
static HWND window;
static HANDLE editorProcess;
static DWORD editorPid;
static WCHAR basePath[1024], editorPath[1024], command[2200], settingsPath[1024], sessionPath[1024];
static NOTIFYICONDATAW tray;
static UINT taskbarCreated;
static BOOL quitting;
static BOOL regionRegistered, fullRegistered;
static HWND editorWindow;

static BOOL CALLBACK FindEditor(HWND candidate, LPARAM unused) {
    DWORD pid; WCHAR title[32]; (void)unused;
    GetWindowThreadProcessId(candidate, &pid);
    GetWindowTextW(candidate, title, 32);
    if (pid == editorPid && GetWindow(candidate, GW_OWNER) == NULL && !lstrcmpW(title, L"ASnapdo")) { editorWindow = candidate; return FALSE; }
    return TRUE;
}
static DWORD ReadKey(const char* json, const char* key, DWORD fallback) {
    const char* p; const char* a; const char* b;
    for (p = json; *p; p++) {
        a = p; b = key; while (*b && *a == *b) { a++; b++; }
        if (!*b) {
            DWORD value = 0;
            while (*a && *a != ':') a++;
            if (!*a) return fallback;
            a++; while (*a == ' ' || *a == '\r' || *a == '\n') a++;
            if (*a < '0' || *a > '9') return fallback;
            while (*a >= '0' && *a <= '9') value = value * 10 + (*a++ - '0');
            return value;
        }
    }
    return fallback;
}
static BOOL RegisterOne(int id, DWORD keys) {
    UINT modifiers = MOD_NOREPEAT;
    if (keys & 0x10000) modifiers |= MOD_SHIFT;
    if (keys & 0x20000) modifiers |= MOD_CONTROL;
    if (keys & 0x40000) modifiers |= MOD_ALT;
    return RegisterHotKey(window, id, modifiers, keys & 0xffff);
}
static void RegisterKeys(void) {
    DWORD region = VK_SNAPSHOT, full = 0x30031, count = 0;
    HANDLE file = CreateFileW(settingsPath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, 0, NULL);
    if (file != INVALID_HANDLE_VALUE) {
        char* json = HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, 65537);
        if (json) {
            if (ReadFile(file, json, 65536, &count, NULL)) {
                region = ReadKey(json, "\"RegionHotkey\"", region);
                full = ReadKey(json, "\"FullscreenHotkey\"", full);
            }
            HeapFree(GetProcessHeap(), 0, json);
        }
        CloseHandle(file);
    }
    regionRegistered = RegisterOne(1, region); fullRegistered = RegisterOne(2, full);
    CopyText(tray.szTip, regionRegistered && fullRegistered ? L"ASnapdo" : L"ASnapdo - 캡처 단축키 충돌: 설정에서 변경하세요");
    Shell_NotifyIconW(NIM_MODIFY, &tray);
}
static void ReleaseKeys(void) { UnregisterHotKey(window, 1); UnregisterHotKey(window, 2); }
static void Launch(int action) {
    STARTUPINFOW startup = { sizeof(startup) }; PROCESS_INFORMATION process;
    if (editorProcess && WaitForSingleObject(editorProcess, 0) == WAIT_TIMEOUT) {
        editorWindow = NULL; EnumWindows(FindEditor, 0);
        if (editorWindow) { AllowSetForegroundWindow(editorPid); PostMessageW(editorWindow, WM_APP + 41, action, 0); }
        return;
    }
    if (editorProcess) { CloseHandle(editorProcess); editorProcess = NULL; }
    ReleaseKeys();
    CopyText(command, L"\""); AppendText(command, editorPath); AppendText(command, L"\" --managed-tray ");
    AppendText(command, action == 1 ? L"--capture-region" : action == 2 ? L"--capture-full" : action == 3 ? L"--settings" : L"");
    if (CreateProcessW(editorPath, command, NULL, NULL, FALSE, 0, NULL, basePath, &startup, &process)) {
        editorProcess = process.hProcess; editorPid = process.dwProcessId; CloseHandle(process.hThread);
    } else { RegisterKeys(); MessageBoxW(window, L"편집기를 실행할 수 없습니다. 설치 파일을 다시 확인하세요.", L"ASnapdo", MB_OK | MB_ICONERROR); }
}
static void Quit(void) {
    quitting = TRUE;
    if (editorProcess && WaitForSingleObject(editorProcess, 0) == WAIT_TIMEOUT) {
        editorWindow = NULL; EnumWindows(FindEditor, 0);
        if (editorWindow) PostMessageW(editorWindow, WM_APP + 42, 0, 0);
    } else { DeleteFileW(sessionPath); DestroyWindow(window); }
}
static LRESULT CALLBACK WindowProc(HWND hwnd, UINT message, WPARAM w, LPARAM l) {
    if (message == taskbarCreated) { Shell_NotifyIconW(NIM_ADD, &tray); return 0; }
    switch (message) {
    case WM_APP + 1: if (!quitting) Launch((int)w); return 0;
    case WM_APP + 2: Quit(); return 0;
    case WM_HOTKEY: if (!quitting) Launch((int)w); return 0;
    case WM_TIMER:
        if (quitting && editorProcess && WaitForSingleObject(editorProcess, 0) == WAIT_TIMEOUT) {
            editorWindow = NULL; EnumWindows(FindEditor, 0);
            if (editorWindow) PostMessageW(editorWindow, WM_APP + 42, 0, 0);
        }
        if (editorProcess && WaitForSingleObject(editorProcess, 0) != WAIT_TIMEOUT) {
            CloseHandle(editorProcess); editorProcess = NULL;
            if (quitting) { DeleteFileW(sessionPath); DestroyWindow(window); } else RegisterKeys();
        }
        return 0;
    case WM_APP + 10:
        if (l == WM_LBUTTONDBLCLK) Launch(0);
        if (l == WM_RBUTTONUP || l == WM_CONTEXTMENU) {
            POINT point; HMENU menu = CreatePopupMenu(); UINT selected;
            AppendMenuW(menu, MF_STRING, 10, L"ASnapdo 열기");
            AppendMenuW(menu, MF_STRING, 11, L"영역 캡처"); AppendMenuW(menu, MF_STRING, 12, L"전체 화면 캡처");
            AppendMenuW(menu, MF_STRING, 13, L"설정"); AppendMenuW(menu, MF_SEPARATOR, 0, NULL); AppendMenuW(menu, MF_STRING, 14, L"종료");
            GetCursorPos(&point); SetForegroundWindow(hwnd);
            selected = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, point.x, point.y, 0, hwnd, NULL);
            DestroyMenu(menu); PostMessageW(hwnd, WM_NULL, 0, 0);
            if (selected == 14) Quit(); else if (selected >= 10 && selected <= 13) Launch(selected - 10);
        }
        return 0;
    case WM_CLOSE: Quit(); return 0;
    case WM_DESTROY: ReleaseKeys(); Shell_NotifyIconW(NIM_DELETE, &tray); PostQuitMessage(0); return 0;
    }
    return DefWindowProcW(hwnd, message, w, l);
}
int WINAPI wWinMain(HINSTANCE instance, HINSTANCE previous, PWSTR arguments, int showCommand) {
    (void)instance; (void)previous; (void)arguments; (void)showCommand;
    WNDCLASSW wc = { 0 }; MSG message; WCHAR* last; HWND existing; int argc = 0; WCHAR** argv;
    BOOL background = FALSE; BOOL enableStartup = FALSE; BOOL testStartup = FALSE; DWORD mutexStatus;
    HANDLE mutex; DWORD pathLength;
    // Exclude the working directory and PATH from subsequent DLL searches.
    if (!SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS)) ExitProcess(GetLastError());
    argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (!argv) ExitProcess(GetLastError());
    for (int i = 1; i < argc; i++) {
        if (!lstrcmpW(argv[i], L"--background")) background = TRUE;
        if (!lstrcmpW(argv[i], L"--enable-startup")) enableStartup = TRUE;
        if (!lstrcmpW(argv[i], L"--enable-startup-test")) { enableStartup = TRUE; testStartup = TRUE; }
    }
    LocalFree(argv);
    if (enableStartup) {
        HKEY key; LONG result;
        pathLength = GetModuleFileNameW(NULL, editorPath, TEXT_CAPACITY(editorPath));
        if (!pathLength || pathLength >= TEXT_CAPACITY(editorPath)) ExitProcess(ERROR_BUFFER_OVERFLOW);
        CopyText(command, L"\""); AppendText(command, editorPath); AppendText(command, L"\" --background");
        result = RegCreateKeyExW(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Run", 0, NULL, 0, KEY_SET_VALUE, NULL, &key, NULL);
        if (result == ERROR_SUCCESS) {
            result = RegSetValueExW(key, testStartup ? L"ASnapdo-InstallSmoke" : L"ASnapdo", 0, REG_SZ, (BYTE*)command, (lstrlenW(command) + 1) * sizeof(WCHAR)); RegCloseKey(key);
        }
        ExitProcess(result == ERROR_SUCCESS ? 0 : 1);
    }
    mutex = CreateMutexW(NULL, TRUE, L"Local\\ASnapdo.NativeTray"); mutexStatus = GetLastError();
    if (mutexStatus == ERROR_ALREADY_EXISTS || (existing = FindWindowW(CLASS_NAME, NULL)) != NULL) {
        existing = FindWindowW(CLASS_NAME, NULL); if (existing && !background) PostMessageW(existing, WM_APP + 1, 0, 0);
        if (mutex) CloseHandle(mutex); ExitProcess(0);
    }
    pathLength = GetModuleFileNameW(NULL, basePath, TEXT_CAPACITY(basePath));
    if (!pathLength || pathLength >= TEXT_CAPACITY(basePath)) ExitProcess(ERROR_BUFFER_OVERFLOW);
    last = basePath;
    for (WCHAR* p = basePath; *p; p++) if (*p == L'\\') last = p;
    *last = 0; CopyText(editorPath, basePath); AppendText(editorPath, L"\\editor\\ASnapdo.exe");
    pathLength = GetEnvironmentVariableW(L"ASNAPDO_DATA_DIR", settingsPath, TEXT_CAPACITY(settingsPath));
    if (pathLength >= TEXT_CAPACITY(settingsPath)) ExitProcess(ERROR_BUFFER_OVERFLOW);
    if (!pathLength) {
        pathLength = ExpandEnvironmentStringsW(L"%LOCALAPPDATA%", settingsPath, TEXT_CAPACITY(settingsPath));
        if (!pathLength || pathLength > TEXT_CAPACITY(settingsPath)) ExitProcess(ERROR_BUFFER_OVERFLOW);
        AppendText(settingsPath, L"\\ASnapdo");
    }
    CreateDirectoryW(settingsPath, NULL);
    CopyText(sessionPath, settingsPath); AppendText(sessionPath, L"\\session.json"); AppendText(settingsPath, L"\\settings.json");
    wc.lpfnWndProc = WindowProc; wc.hInstance = GetModuleHandleW(NULL); wc.lpszClassName = CLASS_NAME;
    wc.hIcon = LoadIconW(wc.hInstance, MAKEINTRESOURCEW(1)); RegisterClassW(&wc);
    window = CreateWindowExW(0, CLASS_NAME, L"ASnapdo", WS_OVERLAPPED, 0, 0, 0, 0, NULL, NULL, wc.hInstance, NULL);
    if (!window) ExitProcess(GetLastError());
    taskbarCreated = RegisterWindowMessageW(L"TaskbarCreated");
    tray.cbSize = sizeof(tray); tray.hWnd = window; tray.uID = 1; tray.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    tray.uCallbackMessage = WM_APP + 10; tray.hIcon = wc.hIcon; CopyText(tray.szTip, L"ASnapdo"); Shell_NotifyIconW(NIM_ADD, &tray);
    RegisterKeys(); SetTimer(window, 1, 500, NULL);
    if (!background) Launch(0);
    while (GetMessageW(&message, NULL, 0, 0) > 0) { TranslateMessage(&message); DispatchMessageW(&message); }
    if (editorProcess) CloseHandle(editorProcess); if (mutex) CloseHandle(mutex); ExitProcess(0);
}
