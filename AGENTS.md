# Codex Agent Instructions — ASnapdo

Read **`PRODUCT_SPEC.md`** before changing code. That file is the source of truth for product decisions and acceptance criteria.

## Mission

Maintain and improve **ASnapdo**, a Windows 10/11 screen capture and annotation editor. Deliver a stable, professional, minimalist, **original** Windows UI with multi-tab capture and rich annotation workflows.

## Naming migration (first task)

Current implementation originated as **ACapdo v0.8**. Rename identifiers and assets to ASnapdo consistently (assembly, root namespace, filenames, publish scripts, app data path, title, log prefix and documentation) while preserving user settings through a deliberate migration. Do not consider the rename completed solely because these documents use the new name.

## Repository baseline

- C# / .NET 8 / Windows Forms (initial v0.8 source).
- Project file: `src/ASnapdo/ASnapdo.csproj`.
- Build: `dotnet build src/ASnapdo/ASnapdo.csproj -c Release` on Windows.
- Publish: run `build-exe.cmd` on Windows; output `release\app\ASnapdo.exe` (native resident launcher) and `release\app\editor\` (editor EXE/DLL). Build the installer with `build-installer.ps1`; output `release\setup\asnapdo_inst_x64_1.0.0.exe`. Default installation is Program Files\ASnapdo and requires administrator approval. See PRODUCT_SPEC.md for the idle memory budget and session lifecycle.
- Visual reference: `docs/design/ui-concept-reference.png` (concept, not an actual application screenshot).
- Original source has **not been verified by Windows compilation/runtime tests**. Verify before claiming success.

## Non-negotiable functional requirements

1. Region and full-screen capture with **configurable global hotkeys** (default Print Screen and Ctrl+Shift+1). Handle registration conflicts safely; don't claim to know which process owns a key.
2. Every new capture creates an independent tab titled `yyyy-MM-dd_HH-mm-ss.png`, with `×` close control, unsaved marker, and unsaved-changes confirmation.
3. `Ctrl+C` copies **composited active-tab image** to the system clipboard, except when text editing is focused.
4. `Ctrl+Shift+C` copies all annotation objects into an app-local buffer; `Ctrl+Alt+C` copies selected annotation; `Ctrl+Shift+V` pastes onto another capture tab. This must not overwrite the destination image.
5. Annotation objects are individually selectable, movable, resizable, deletable, and undoable. Preserve image-relative coordinates independent of zoom.
6. `Ctrl+mouse wheel` zooms the editor; fit to screen via Ctrl+0.
7. Tray minimize is quiet: **no large/repeated balloon notifications**; at most a discreet one-time message. Closing the window should not silently terminate the tray-resident app; tray Exit really exits.
8. Recent thumbnail/gallery strip remains hidden by default and optional.
9. Preserve capture/editor feature set and use the ASnapdo name. Publisher/company is AAStudio.kr. The license is free for individuals, businesses and public institutions; LICENSE.txt is the shared installer/help license text. Keep the compact app header as icon + ASnapdo.

## UI rules

- Independent layout: top compact actions, document tabs, narrow left tools, dominant central canvas, contextual right inspector, slim bottom status bar.
- Visually clean and premium: whitespace, consistent spacing, restrained blue `#2563EB`, soft neutrals, minimal decoration.
- Use ASnapdo's original layouts, interaction patterns, and icons. Do not copy proprietary assets.
- Choose WinForms incremental improvement or staged WPF shell migration based on regression risk; explain the choice. Never discard working functionality merely for a UI rewrite.
- Design must work at Windows 125%, 150%, 200% DPI and narrow widths.

## Engineering workflow

1. Inspect current code; list verified behavior and any existing failures.
2. Make small, coherent commits/steps and keep project buildable.
3. Separate capture/document/annotation/clipboard/hotkey responsibilities where practical; avoid growing `MainForm.cs` monolith.
4. For image manipulation: dispose Bitmaps/Graphics safely; marshal UI updates to UI thread; avoid leaking GDI handles or touching disposed tabs.
5. Keep image clipboard contents and internal annotation buffer separate.
6. Check shortcut interactions with focused text inputs and system-reserved key combinations.
7. Test save naming collisions, mixed-DPI displays, multiple tabs, dirty close, background capture, and destination images with different sizes.
8. Run build and tests when a Windows SDK environment exists; otherwise state precise limitations and do not fabricate test results.

## Release and deployment rules

- Every deployment must create a versioned GitHub Release in `mant81/ASnapdo` and attach the matching Windows installer (`asnapdo_inst_x64_<version>.exe`). A source-only tag or release is not a completed deployment.
- Automatically include the current version's `CHANGELOG.md` section and GitHub-generated release notes. Use `publish-release.ps1` after building the installer and committing/pushing the release source.
- Keep the editor, native launcher, installer, product documentation and download links on the same version. Never replace an existing published version with different binaries; increment the version.
- Build and run relevant checks before publishing. Create the release as a draft, upload and verify the installer asset, then publish. Report the release URL, installer path and SHA-256.
- Never commit signing keys or access tokens. Release publishing uses `GH_TOKEN`/`GITHUB_TOKEN` or the configured Git credential helper without printing credentials.

## Acceptance and communication

Follow P0 → P1 → P2 checklists in `PRODUCT_SPEC.md`. After changes report files modified, build/test commands and actual results, artifacts, remaining blockers, and screenshot(s) of the running UI when possible. Do not present concept images as real application screenshots.
