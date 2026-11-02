param([string]$Package = "")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing
Add-Type @'
using System; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class LicenseWindow {
 public delegate bool Callback(IntPtr w,IntPtr p);
 [DllImport("user32.dll")] public static extern bool EnumWindows(Callback cb,IntPtr p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr w,StringBuilder s,int n);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr w,out uint p);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr w,out Rect r);
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr w,int m,IntPtr p,IntPtr l);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr w,IntPtr dc,uint flags);
 public struct Rect { public int Left,Top,Right,Bottom; }
 public static IntPtr Find() { IntPtr found=IntPtr.Zero; EnumWindows((w,p)=> { var name=new StringBuilder(128); GetClassName(w,name,128); if(name.ToString()=="TWizardForm") found=w; return true; },IntPtr.Zero); return found; }
}
'@
$root = [IO.Path]::GetFullPath("$PSScriptRoot\..")
if (-not $Package) { $version = ([xml](Get-Content -LiteralPath "$root\src\ASnapdo\ASnapdo.csproj" -Raw)).Project.PropertyGroup.Version; $Package = "$root\release\setup\asnapdo_inst_x64_$version.exe" }
$process = $null; $wizardProcess = 0
try {
    if ([LicenseWindow]::Find() -ne [IntPtr]::Zero) { throw 'Another installer wizard is open.' }
    $process = Start-Process -FilePath $Package -ArgumentList '/LANG=korean','/NOICONS',"/DIR=$root\release\license-preview" -WindowStyle Hidden -PassThru
    $until = [DateTime]::UtcNow.AddSeconds(20); $window = [IntPtr]::Zero
    do { Start-Sleep -Milliseconds 100; $window = [LicenseWindow]::Find() } while ($window -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $until)
    if ($window -eq [IntPtr]::Zero) { throw 'Installer wizard not found.' }
    [uint32]$ownerPid = 0; [void][LicenseWindow]::GetWindowThreadProcessId($window,[ref]$ownerPid); $wizardProcess=$ownerPid
    Start-Sleep -Seconds 1
    $automation = [System.Windows.Automation.AutomationElement]::FromHandle($window)
    $controls = $automation.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
    $next = $null; $accept = $null; $license = ''
    foreach ($element in $controls) {
        if ($element.Current.Name.Contains('다음')) { $next=$element }
        if ($element.Current.Name.Contains('동의합니다')) { $accept=$element }
        $pattern = $null
        if ($element.TryGetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern,[ref]$pattern)) { $license += $pattern.DocumentRange.GetText(-1) }
    }
    if (-not $next -or -not $accept) { foreach ($element in $controls) { Write-Output ($element.Current.ControlType.ProgrammaticName + ' : ' + $element.Current.Name) }; throw 'License controls not found.' }
    if ($next.Current.IsEnabled) { throw 'Next is enabled before accepting the license.' }
    if (-not $license.Contains('MIT License') -or -not $license.Contains('AAStudio.kr')) { throw 'Installer license text does not contain the MIT license or publisher.' }
    [void][LicenseWindow]::SendMessage([IntPtr]$accept.Current.NativeWindowHandle,0xF5,[IntPtr]::Zero,[IntPtr]::Zero)
    Start-Sleep -Milliseconds 200
    if (-not $next.Current.IsEnabled) { throw 'Next remains disabled after accepting the license.' }
    $rect = New-Object LicenseWindow+Rect; [void][LicenseWindow]::GetWindowRect($window,[ref]$rect)
    $bitmap = New-Object Drawing.Bitmap ($rect.Right-$rect.Left),($rect.Bottom-$rect.Top)
    try {
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try { $dc=$graphics.GetHdc(); try { [void][LicenseWindow]::PrintWindow($window,$dc,2) } finally { $graphics.ReleaseHdc($dc) } } finally { $graphics.Dispose() }
        $bitmap.Save("$root\docs\screenshots\asnapdo-installer-license.png",[Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
    Write-Output 'PASS: installer shows free-use policy/publisher; Next disabled before agreement and enabled after agreement. No installation performed.'
} finally {
    if ($wizardProcess) { Stop-Process -Id $wizardProcess -ErrorAction SilentlyContinue }
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
}
