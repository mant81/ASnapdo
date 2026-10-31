# ASnapdo 1.0.2 release verification

- Environment: Windows, .NET SDK 10.0.403, WinForms/.NET 10.
- `build-installer.ps1`: passed; native launcher, self-contained win-x64 editor and installer generated.
- Installer: `release/setup/asnapdo_inst_x64_1.0.2.exe`; product version 1.0.2.
- Targeted smoke tests: number stamps at 50/100/200% zoom and undo; automatic region copy, cancellation, clipboard history selection/paste/delete/clear and 12-item limit; gallery close/persist/release; contextual toolbar, stable inspector and 900/1160px widths. All passed.
- Installer installation/uninstallation was not rerun for this version. Mixed-display DPI and interactive visual checks at 125/150/200% remain unverified. Number stamp zoom checks are not DPI checks.
- Screen capture via CopyFromScreen failed in the restricted test desktop. That temporary test path was removed; UI control rendering is used for inspection. Test runner exceptions are reported on stderr with exit code 1.
- Code signing and antivirus validation are not performed for this release. Existing native idle-memory limitation recorded in SECURITY_REVIEW.md remains.
- Release publishing automatically includes this version's changelog, GitHub-generated notes and installer SHA-256; installer is uploaded before the draft becomes public.
