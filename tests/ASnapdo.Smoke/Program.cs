using System.Reflection;
using ASnapdo;
using ASnapdo.Models;
using ASnapdo.Services;
using ASnapdo.UI;

internal static class Program
{
    private static void TestGalleryClose()
    {
        string? previous = Environment.GetEnvironmentVariable("ASNAPDO_DATA_DIR");
        string folder = Path.Combine(Path.GetTempPath(), "ASnapdo-gallery-" + Guid.NewGuid());
        Environment.SetEnvironmentVariable("ASNAPDO_DATA_DIR", folder);
        try
        {
            SettingsService.Save(new AppSettings { ShowGallery = true });
            using var form = new MainForm(); form.Show();
            for (int i = 0; i < 8; i++) typeof(MainForm).GetMethod("AddDocument", Private)!.Invoke(form, [Sample(400, 300)]);
            Application.DoEvents();
            var gallery = (CaptureGallery)typeof(MainForm).GetField("gallery", Private)!.GetValue(form)!;
            var tabs = (TabControl)typeof(MainForm).GetField("tabs", Private)!.GetValue(form)!;
            Check(gallery.Visible && gallery.ThumbnailCount == 8, "Gallery shows captures");
            var close = gallery.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<ChromeButton>()).Single();
            Check(close.Visible && close.Parent!.Right == gallery.ClientSize.Width, "Gallery close remains at far right");
            close.PerformClick(); Application.DoEvents();
            Check(!gallery.Visible && gallery.ThumbnailCount == 0 && tabs.TabCount == 8, "Gallery closes and frees thumbnails without closing tabs");
            Check(!SettingsService.Load().ShowGallery, "Gallery close persists hidden setting");
            form.Hide();
            Console.WriteLine("PASS: gallery right close button hides gallery, releases thumbnails and persists setting while retaining tabs.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASNAPDO_DATA_DIR", previous);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
    private static void TestContextualToolbar()
    {
        using var form = new MainForm();
        form.Show();
        typeof(MainForm).GetMethod("AddDocument", Private)!.Invoke(form, [Sample(1048, 563)]);
        var editor = (EditorCanvas)typeof(MainForm).GetProperty("Editor", Private)!.GetValue(form)!;
        IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
        var controls = Descendants(form).ToArray();
        var fill = controls.OfType<CheckBox>().Single(c => c.Text == "채우기");
        var dashed = controls.OfType<CheckBox>().Single(c => c.Text == "점선");
        var bold = controls.OfType<CheckBox>().Single(c => c.Text == "굵게");
        var duplicate = controls.OfType<ChromeButton>().Single(c => c.Text == "복제");
        var refresh = typeof(MainForm).GetMethod("RefreshStatus", Private)!;
        foreach (var kind in new AnnotationKind?[] { null, AnnotationKind.Rectangle, AnnotationKind.Arrow, AnnotationKind.Text, AnnotationKind.Number, AnnotationKind.Image })
        {
            editor.Tool = kind; refresh.Invoke(form, null); Application.DoEvents();
            Check(fill.Visible == (kind == AnnotationKind.Rectangle), "Fill visibility matches tool");
            Check(dashed.Visible == (kind is AnnotationKind.Rectangle or AnnotationKind.Arrow), "Dash visibility matches tool");
            Check(bold.Visible == (kind == AnnotationKind.Text), "Text controls match tool");
            Check(!duplicate.Visible, "No object commands without selection");
        }
        editor.Document.Annotations.Add(new Annotation { Kind = AnnotationKind.Rectangle, Bounds = new(30, 30, 120, 80) });
        editor.Document.Selection = editor.Document.Annotations.Last().Id;
        editor.Tool = null; refresh.Invoke(form, null); Application.DoEvents();
        Check(fill.Visible && duplicate.Visible, "Selected object reveals its properties and commands");
        int visibilityChanges = 0, positionChanges = 0;
        var inspector = (FlowLayoutPanel)fill.Parent!;
        var snapshot = inspector.Controls.Cast<Control>().ToDictionary(c => c, c => (c.Visible, c.Bounds));
        foreach (Control control in inspector.Controls)
        {
            control.VisibleChanged += (_, _) => visibilityChanges++;
            control.LocationChanged += (_, _) => positionChanges++;
        }
        for (int i = 0; i < 20; i++) refresh.Invoke(form, null);
        Check(visibilityChanges == 0 && positionChanges == 0, "Repeated selection refresh never toggles or moves inspector controls");
        Drag(editor, new(60, 60), new(80, 90));
        Check(visibilityChanges == 0 && positionChanges == 0, "Moving selected object keeps inspector stable");
        Check(snapshot.All(item => item.Key.Visible == item.Value.Visible && item.Key.Bounds == item.Value.Bounds), "Inspector geometry stays unchanged after movement");
        foreach (int width in new[] { 1160, 900 })
        {
            form.Width = width; Application.DoEvents();
            foreach (var button in controls.OfType<ChromeButton>().Where(c => c.Visible && c.Parent is FlowLayoutPanel && c.Parent.Parent?.AccessibleName is "선택" or "주석" or "이미지 편집" or "이미지 영역"))
                Check(button.Right <= button.Parent!.ClientSize.Width, "Tool group contains every button");
        }
        form.Width = 1160; Application.DoEvents();
        Save(form, Path.Combine(Environment.CurrentDirectory, "docs", "screenshots", "asnapdo-contextual-toolbar.png"));
        form.Hide();
        Console.WriteLine("PASS: contextual controls, selected object actions, toolbar groups at 1160/900 widths; running UI screenshot saved.");
    }
    private static void TestAutomaticRegionCopy()
    {
        using var form = new MainForm();
        form.Show();
        typeof(MainForm).GetMethod("AddDocument", Private)!.Invoke(form, [Sample(400, 300)]);
        var editor = (EditorCanvas)typeof(MainForm).GetProperty("Editor", Private)!.GetValue(form)!;
        using var expected = editor.Document.Compose();
        editor.StartCrop(true);
        Drag(editor, new(20, 30), new(180, 130));
        Check(!editor.CropMode, "Successful automatic copy ends selection mode");
        using var copied = Clipboard.GetImage()!;
        Check(copied.Size == new Size(160, 100), "Mouse release copies selected region automatically");
        using var pixels = new Bitmap(copied);
        Check(pixels.GetPixel(10, 10) == expected.GetPixel(30, 40), "Automatic region copy has correct pixels");
        editor.StartCrop(true);
        Drag(editor, new(50, 50), new(50, 50));
        using var afterEmpty = Clipboard.GetImage()!;
        Check(afterEmpty.Size == copied.Size && editor.CropMode, "Empty click does not copy");
        Mouse(editor, "OnMouseDown", new(20, 20));
        Mouse(editor, "OnMouseMove", new(80, 80));
        editor.CancelCrop();
        Mouse(editor, "OnMouseUp", new(80, 80));
        using var afterCancel = Clipboard.GetImage()!;
        Check(afterCancel.Size == copied.Size && !editor.CropMode, "Cancelled drag does not copy");
        Check(editor.Document.Image.Size == new Size(400, 300), "Region copy preserves source image");
        var panel = (RegionClipboardPanel)typeof(MainForm).GetField("regionClipboard", Private)!.GetValue(form)!;
        Check(panel.Visible && panel.Snapshot?.Size == copied.Size, "Successful region copy displays preview");
        typeof(MainForm).GetMethod("AddDocument", Private)!.Invoke(form, [Sample(600, 400)]);
        var destination = (EditorCanvas)typeof(MainForm).GetProperty("Editor", Private)!.GetValue(form)!;
        var background = destination.Document.Image;
        using (var unrelated = Sample(10, 10)) Clipboard.SetImage(unrelated);
        IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
        Descendants(panel).OfType<ChromeButton>().Single(b => b.Text == "현재 탭에 붙여넣기").PerformClick();
        Check(destination.Document.Annotations.Single().Bounds.Size == new SizeF(160, 100), "Panel pastes displayed snapshot after tab and OS clipboard changes");
        Check(ReferenceEquals(background, destination.Document.Image), "Panel paste preserves destination background");
        form.Height = 800;
        Application.DoEvents();
        Check(panel.Height < panel.Parent!.ClientSize.Height, "Clipboard is a compact card instead of full height sidebar");
        Check(panel.Right < panel.Parent.ClientSize.Width && panel.Top > 0, "Clipboard card has edge spacing");
        int cardHeight = panel.Height;
        form.Height = 900; Application.DoEvents();
        Check(panel.Height == cardHeight, "Taller window does not stretch clipboard card");
        Check(panel.Parent.Controls.Cast<Control>().Any(c => c != panel && c.Width == panel.Parent.ClientSize.Width), "Clipboard card preserves full canvas width");
        form.Height = 620; Application.DoEvents();
        Application.DoEvents();
        using (var screenshot = new Bitmap(panel.Width, panel.Height))
        {
            panel.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size));
            screenshot.Save(Path.Combine(Environment.CurrentDirectory, "docs", "screenshots", "asnapdo-clipboard-card.png"));
        }
        Descendants(panel).OfType<ChromeButton>().Single(b => b.AccessibleName == "클립보드 패널 닫기").PerformClick();
        Check(!panel.Visible && panel.ItemCount == 1, "Closing panel hides history without deleting it");
        for (int i = 0; i < 14; i++)
        {
            using var item = Sample(40 + i, 30);
            panel.ShowSnapshot(item);
        }
        Check(panel.ItemCount == 12 && panel.Snapshot!.Width == 53, "Clipboard history is bounded and newest item selected");
        panel.SelectItem(5);
        Check(panel.Snapshot!.Width == 48, "Older clipboard item can be selected");
        Descendants(panel).OfType<ChromeButton>().Single(b => b.Text == "현재 탭에 붙여넣기").PerformClick();
        Check(destination.Document.Annotations.Last().Bounds.Width == 48, "Selected older item is pasted");
        panel.DeleteSelected(); Check(panel.ItemCount == 11, "Individual history item can be deleted");
        form.Height = 900; Application.DoEvents();
        using (var screenshot = new Bitmap(panel.Width, panel.Height))
        {
            panel.DrawToBitmap(screenshot, new Rectangle(Point.Empty, screenshot.Size));
            screenshot.Save(Path.Combine(Environment.CurrentDirectory, "docs", "screenshots", "asnapdo-clipboard-list.png"));
        }
        panel.Clear(); Check(panel.ItemCount == 0 && panel.Snapshot == null, "Clear releases entire history");
        form.Hide();
        Console.WriteLine("PASS: mouse release automatically copies region; empty click and cancelled drag preserve clipboard.");
    }
    private static void TestNumberStamp()
    {
        using var document = new CaptureDocument(Sample(400, 300), "numbers.png");
        using var canvas = new EditorCanvas(document) { Tool = AnnotationKind.Number };
        canvas.CreateControl();
        foreach (float zoom in new[] { 1f, .5f, 2f })
        {
            document.Zoom = zoom;
            int x = (int)((40 + document.Annotations.Count * 60) * zoom);
            int y = (int)(80 * zoom);
            // Reproduce the automatic capture performed by WinForms before MouseDown.
            canvas.Capture = true;
            typeof(EditorCanvas).GetMethod("OnMouseDown", Private)!.Invoke(canvas, [new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)]);
            Check(!canvas.Capture, "Number stamp ends mouse capture");
            typeof(EditorCanvas).GetMethod("OnMouseMove", Private)!.Invoke(canvas, [new MouseEventArgs(MouseButtons.Left, 0, x + 2, y + 2, 0)]);
            typeof(EditorCanvas).GetMethod("OnMouseUp", Private)!.Invoke(canvas, [new MouseEventArgs(MouseButtons.Left, 1, x + 2, y + 2, 0)]);
            var number = document.Annotations.Last();
            Check(number.Text == document.Annotations.Count.ToString() && number.Bounds.Size == new SizeF(36, 36), "Sequential number retains default image-relative size after pointer movement");
            Check(canvas.Tool == AnnotationKind.Number, "Number tool stays active");
        }
        document.Undo();
        Check(document.Annotations.Count == 2 && document.Annotations.All(a => a.Bounds.Size == new SizeF(36, 36)), "One Undo removes only the latest number");
        Console.WriteLine("PASS: number stamps retain default size and sequence at 50%, 100%, 200% zoom; Undo removes one stamp.");
    }
    private static void TestCaptureStartup()
    {
        string? previous = Environment.GetEnvironmentVariable("ASNAPDO_DATA_DIR");
        string folder = Path.Combine(Path.GetTempPath(), "ASnapdo-capture-" + Guid.NewGuid());
        Environment.SetEnvironmentVariable("ASNAPDO_DATA_DIR", folder);
        try
        {
            foreach (string action in new[] { "--capture-region", "--capture-full" })
            {
                using var form = new MainForm(true, action);
                bool prematureVisibility = false, selectionSeen = false, editorSeen = false, timedOut = false;
                form.VisibleChanged += (_, _) =>
                {
                    if (!form.Visible) return;
                    var tabs = (TabControl)typeof(MainForm).GetField("tabs", Private)!.GetValue(form)!;
                    if (tabs.TabCount == 0 || !(bool)typeof(MainForm).GetField("capturing", Private)!.GetValue(form)!) prematureVisibility = true;
                    editorSeen = true;
                };
                using var timer = new System.Windows.Forms.Timer { Interval = 20 };
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                timer.Tick += (_, _) =>
                {
                    foreach (Form overlay in Application.OpenForms.Cast<Form>().ToArray())
                        if (overlay != form && overlay.GetType().Name == "SelectionForm" && overlay.Visible)
                        {
                            selectionSeen = true;
                            overlay.DialogResult = DialogResult.Cancel;
                        }
                    if (editorSeen) form.Close();
                    if (elapsed.Elapsed.TotalSeconds > 15) { timedOut = true; form.Close(); }
                };
                timer.Start();
                Application.Run(form);
                Check(!timedOut && !prematureVisibility, action + ": no editor flash before capture");
                Check(action == "--capture-region" ? selectionSeen && !editorSeen : editorSeen,
                    action + ": cancel stays hidden / success opens editor");
                Console.WriteLine("PASS: " + action + " startup visibility and capture outcome.");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASNAPDO_DATA_DIR", previous);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Run(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            Environment.ExitCode = 1;
        }
    }
    private static void Run(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--gallery-close-test")) { TestGalleryClose(); return; }
        if (args.Contains("--contextual-toolbar-test")) { TestContextualToolbar(); return; }
        if (args.Contains("--automatic-region-copy-test")) { TestAutomaticRegionCopy(); return; }
        if (args.Contains("--number-stamp-test")) { TestNumberStamp(); return; }
        if (args.Length == 2 && args[0] == "--session-fixture")
        {
            using var doc = new CaptureDocument(Sample(800, 600), "fixture.png");
            doc.Checkpoint(); doc.Annotations.Add(new Annotation { Kind = AnnotationKind.Rectangle, Color = Color.Red, Bounds = new(20, 30, 100, 80) });
            doc.ApplyPixelEffect(new(15, 25, 120, 100), PixelEffectKind.Blur, 8);
            SessionService.Save([doc], [Point.Empty], 0, args[1]); return;
        }
        if (args.Contains("--capture-startup-test"))
        {
            TestCaptureStartup();
            return;
        }
        if (args.Contains("--security-test")) { SecurityChecks.Run(); return; }
        if (args.Length == 2 && args[0] == "--session-verify")
        {
            var session = SessionService.Load(args[1]);
            try
            {
                Check(session.Documents.Count == 1 && session.Documents[0].Name == "fixture.png", "Session was not preserved.");
                Check(session.Documents[0].Annotations.Any(a => a.Kind == AnnotationKind.PixelPatch), "Raster effect was lost across close.");
            }
            finally { foreach (var document in session.Documents) document.Dispose(); }
            Console.WriteLine("PASS: encrypted fixture session preserved."); return;
        }
        using var form = new MainForm { Size = new Size(1280, 800) };
        int result = 0;
        form.Shown += (_, _) => form.BeginInvoke(new Action(() =>
        {
            try
            {
                TestEditor();
                TestCrop();
                TestPixelEffects();
                TestRegionPaste();
                TestSession();
                TestAutoSave();
                TestOptions();
                TestStartupOptions();
                TestCaptureBlockedBySettings(form);
                using (var help = new HelpForm())
                {
                    help.Show(form); Application.DoEvents();
                    string helpOutput = Path.Combine(Environment.CurrentDirectory, "docs", "screenshots");
                    var pages = help.Controls.OfType<TabControl>().Single();
                    var license = pages.TabPages[0].Controls.OfType<TextBox>().Single().Text;
                    Check(license.Contains("MIT License") && license.Contains("Copyright (c) 2026 AAStudio.kr") && license.ReplaceLineEndings("\n") == LicenseInfo.Terms.ReplaceLineEndings("\n"), "Help displays shared MIT license and copyright");
                    foreach (TabPage page in pages.TabPages)
                    {
                        pages.SelectedTab = page; Application.DoEvents();
                        Save(help, Path.Combine(helpOutput, $"asnapdo-help-{pages.SelectedIndex}.png"));
                    }
                    help.Close();
                }
                TestHotkeyAvailability();
                TestSelectionOverlay();
                var add = typeof(MainForm).GetMethod("AddDocument", Private)!;
                for (int i = 0; i < 3; i++) add.Invoke(form, [Sample(1048, 563)]);
                Application.DoEvents();
                var tabs = (TabControl)typeof(MainForm).GetField("tabs", Private)!.GetValue(form)!;
                Check(tabs.TabCount == 3, "Three tabs accumulate");
                foreach (TabPage page in tabs.TabPages)
                {
                    tabs.SelectedTab = page; Application.DoEvents();
                    var canvas = page.Controls.OfType<Panel>().Single().Controls.OfType<EditorCanvas>().Single();
                    Check(canvas.Document.Zoom == 1f && canvas.Size == canvas.Document.Image.Size, "New captures open at 100 percent, including inactive tabs");
                }
                Check(tabs.TabPages.Cast<TabPage>().Select(p => ((CaptureDocument)p.Tag!).Name).Distinct().Count() == 3, "Names do not collide");
                var panel = tabs.SelectedTab!.Controls.OfType<Panel>().Single();
                var editor = panel.Controls.OfType<EditorCanvas>().Single();
                var rulers = tabs.SelectedTab.Controls.OfType<CanvasRuler>().ToArray();
                Check(rulers.Length == 2 && rulers.Count(r => r.Vertical) == 1, "Both image rulers exist");
                var horizontalRuler = rulers.Single(r => !r.Vertical);
                var verticalRuler = rulers.Single(r => r.Vertical);
                void CheckRulers()
                {
                    var origin = editor.PointToScreen(Point.Empty);
                    Check(Math.Abs(horizontalRuler.PointToScreen(new((int)horizontalRuler.ImageOrigin, 0)).X - origin.X) < 2, "Horizontal zero follows image origin");
                    Check(Math.Abs(verticalRuler.PointToScreen(new(0, (int)verticalRuler.ImageOrigin)).Y - origin.Y) < 2, "Vertical zero follows image origin");
                    Check(Math.Abs(horizontalRuler.PositionForPixel(100) - horizontalRuler.ImageOrigin - 100 * editor.Document.Zoom) < .01f, "Ruler uses image pixels at current zoom");
                }
                CheckRulers();
                typeof(MainForm).GetMethod("Fit", Private)!.Invoke(form, null);
                Check(editor.Document.Zoom < 1f, "Explicit fit still reduces large image");
                CheckRulers();
                typeof(MainForm).GetMethod("Zoom", Private)!.Invoke(form, [1f / editor.Document.Zoom, null]);
                Check(panel.Width > form.ClientSize.Width - 80, "Canvas spans width except ruler");
                editor.Document.Annotations.Add(new Annotation { Kind = AnnotationKind.Rectangle, Bounds = new(65, 165, 430, 220), Filled = true, Width = 3 });
                editor.Document.Annotations.Add(new Annotation { Kind = AnnotationKind.Text, Text = "Edit directly on the capture", Bounds = new(100, 195, 360, 130), FontSize = 28, Bold = true });
                editor.Document.Selection = editor.Document.Annotations[0].Id;
                editor.NotifyEdit();
                var first = (CaptureDocument)tabs.TabPages[0].Tag!;
                var clipboard = new ClipboardService(); clipboard.CopyAnnotations(editor.Document, false); clipboard.Paste(first);
                Check(first.Annotations.Count == 2 && !ReferenceEquals(first.Annotations[0], editor.Document.Annotations[0]), "Independent pasted objects");
                first.Undo(); Check(first.Annotations.Count == 0, "Paste undo");
                string output = Path.Combine(Environment.CurrentDirectory, "docs", "screenshots"); Directory.CreateDirectory(output);
                Save(form, Path.Combine(output, "asnapdo-wide-canvas.png"));
                editor.StartCrop(true); Drag(editor, new(60, 160), new(500, 400));
                Save(form, Path.Combine(output, "asnapdo-region-copy.png"));
                editor.CancelCrop();
                editor.StartCrop(); Drag(editor, new(60, 160), new(500, 400));
                Save(form, Path.Combine(output, "asnapdo-crop-selection.png"));
                typeof(MainForm).GetMethod("ApplyCrop", Private)!.Invoke(form, null);
                Check(editor.Document.Image.Size == new Size(440, 240) && editor.Size == new Size(440, 240), "Crop updates active canvas size");
                Check(first.Image.Size == new Size(1048, 563), "Crop does not affect other tab");
                typeof(MainForm).GetMethod("Undo", Private)!.Invoke(form, null);
                Check(editor.Size == new Size(1048, 563), "Crop undo restores canvas size");
                form.Size = new Size(900, 650); Application.DoEvents();
                Check(panel.Width > form.ClientSize.Width - 80, "Narrow canvas spans width except ruler");
                Check(Math.Abs(editor.Document.Zoom - 1f) < .001f && panel.HorizontalScroll.Visible && panel.VerticalScroll.Visible, "Narrow window preserves 100 percent and scrolls large image");
                Save(form, Path.Combine(output, "asnapdo-wide-canvas-narrow.png"));
                typeof(MainForm).GetMethod("Zoom", Private)!.Invoke(form, [2f, null]);
                panel.AutoScrollPosition = new Point(250, 180); Application.DoEvents();
                CheckRulers();
                Save(form, Path.Combine(output, "asnapdo-rulers-zoom-scroll.png"));
                Console.WriteLine("PASS: horizontal/vertical ruler origin at fit, 100 percent, 200 percent and scrolled viewport.");
                form.Size = new Size(1280, 800);
                typeof(MainForm).GetMethod("Zoom", Private)!.Invoke(form, [.5f, null]);
                var mainSettings = (AppSettings)typeof(MainForm).GetField("settings", Private)!.GetValue(form)!;
                var mainGallery = (CaptureGallery)typeof(MainForm).GetField("gallery", Private)!.GetValue(form)!;
                mainSettings.ShowGallery = true;
                ((Action)typeof(MainForm).GetField("relayout", Private)!.GetValue(form)!)();
                typeof(MainForm).GetMethod("RefreshStatus", Private)!.Invoke(form, null); Application.DoEvents();
                Check(mainGallery.Visible && mainGallery.ThumbnailCount == 3, "Optional gallery shows all open documents");
                var firstThumbnail = mainGallery.Controls.OfType<FlowLayoutPanel>().Single().Controls[0];
                typeof(Control).GetMethod("OnClick", Private)!.Invoke(firstThumbnail, [EventArgs.Empty]);
                Check(tabs.SelectedIndex == 0, "Gallery thumbnail switches active tab");
                tabs.SelectedIndex = 2; panel.AutoScrollPosition = Point.Empty;
                editor.StartPixelEffect(PixelEffectKind.Mosaic); Drag(editor, new(60, 60), new(650, 145));
                editor.SetEffectStrength(16); editor.ApplyPixelEffect();
                Application.DoEvents(); Save(form, Path.Combine(output, "asnapdo-gallery.png"));
                editor.StartPixelEffect(PixelEffectKind.Blur); Drag(editor, new(70, 180), new(500, 290));
                Save(form, Path.Combine(output, "asnapdo-blur-preview.png")); editor.CancelCrop();
                editor.Document.Undo(); editor.NotifyEdit();
                mainSettings.ShowGallery = false;
                ((Action)typeof(MainForm).GetField("relayout", Private)!.GetValue(form)!)();
                typeof(MainForm).GetMethod("RefreshStatus", Private)!.Invoke(form, null);
                Check(!mainGallery.Visible && mainGallery.ThumbnailCount == 0, "Disabling gallery releases all thumbnails");
                form.Close(); Application.DoEvents();
                Check(!form.IsDisposed && !form.Visible && tabs.TabCount == 3, "Window X hides to tray and retains dirty tabs");
                var tray = (NotifyIcon)typeof(MainForm).GetField("tray", Private)!.GetValue(form)!;
                Check(tray.Visible, "Tray icon remains after window close");
                typeof(MainForm).GetMethod("Restore", Private)!.Invoke(form, null);
                Check(form.Visible, "Tray restore shows window");
                tray.ContextMenuStrip!.Items.Cast<ToolStripItem>().Single(i => i.Text == "종료").PerformClick();
                Check(form.IsDisposed, "Tray Exit disposes form with dirty tabs without save prompt");
                Console.WriteLine($"PASS: interaction checks; three tabs; independent paste and undo; full-width canvas at 1280 and 900; DPI {form.DeviceDpi}.");
            }
            catch (Exception e) { Console.Error.WriteLine(e); result = 1; }
            finally
            {
                if (!form.IsDisposed) { typeof(MainForm).GetField("exiting", Private)!.SetValue(form, true); form.Close(); }
            }
        }));
        Application.Run(form); Environment.ExitCode = result;
    }
    private static void TestEditor()
    {
        using var host = new Form();
        using var doc = new CaptureDocument(Sample(800, 600), "test.png");
        using var editor = new EditorCanvas(doc); host.Controls.Add(editor); host.Show();
        editor.Tool = AnnotationKind.Rectangle;
        Drag(editor, new(100, 100), new(300, 250));
        Check(doc.Annotations.Count == 1 && editor.Tool == null, "Draw then select rectangle");
        var rectangle = doc.Annotations[0];
        var handles = new Point[] { new(100,100), new(200,100), new(300,100), new(300,175), new(300,250), new(200,250), new(100,250), new(100,175) };
        foreach (var point in handles)
        {
            doc.Selection = doc.Annotations[0].Id;
            var before = doc.Annotations[0].Bounds;
            Drag(editor, point, new(point.X + 10, point.Y + 10));
            Check(doc.Annotations[0].Bounds != before, "Every resize handle works");
            doc.Undo(); Check(doc.Annotations[0].Bounds == before, "Resize is a single undo");
        }
        doc.Selection = doc.Annotations[0].Id;
        editor.SetStyleOptions(true, true, 32, true);
        Check(editor.Selected!.Filled && editor.Selected.Dashed, "Shape styles applied");
        doc.Undo(); Check(!doc.Annotations[0].Filled, "Style undo");
        doc.Selection = doc.Annotations[0].Id; editor.DuplicateSelection();
        Check(doc.Annotations.Count == 2 && doc.Annotations[0].Id != doc.Annotations[1].Id, "Duplicate identity");
        doc.Undo();
        Mouse(editor, "OnMouseDown", new(180, 170)); Mouse(editor, "OnMouseUp", new(180, 170));
        doc.Undo(); Check(doc.Annotations.Count == 0, "Selection click adds no undo entry");
        editor.Tool = AnnotationKind.Arrow; Drag(editor, new(400, 300), new(100, 100));
        Check(doc.Annotations.Single().ReverseX && doc.Annotations.Single().ReverseY, "Arrow preserves reverse direction");
        doc.Undo();
        editor.Tool = AnnotationKind.Text; Mouse(editor, "OnMouseDown", new(80, 80));
        var input = editor.Controls.OfType<TextBox>().Single(); input.Text = "First line\r\nSecond line";
        editor.FinishTextEdit();
        Check(doc.Annotations.Count == 1 && doc.Annotations[0].Text.Contains("Second line"), "Inline multiline text creation");
        Mouse(editor, "OnMouseDown", new(100, 100)); Mouse(editor, "OnMouseUp", new(100, 100));
        Mouse(editor, "OnMouseDoubleClick", new(100, 100));
        input = editor.Controls.OfType<TextBox>().Single(); input.Text = "Edited"; editor.FinishTextEdit();
        Check(doc.Annotations[0].Text == "Edited", "Double click text edit");
        doc.Undo(); Check(doc.Annotations[0].Text.Contains("Second line"), "Text edit undo");
        editor.Tool = AnnotationKind.Text; Mouse(editor, "OnMouseDown", new(400, 400));
        editor.Controls.OfType<TextBox>().Single().Text = "Discard"; editor.FinishTextEdit(true);
        Check(doc.Annotations.Count == 1, "Text cancellation");
        using var composed = doc.Compose(); Check(composed.Size == doc.Image.Size, "Composition size");
        editor.Tool = AnnotationKind.Number;
        Mouse(editor, "OnMouseDown", new(450, 200)); Mouse(editor, "OnMouseUp", new(450, 200));
        Mouse(editor, "OnMouseDown", new(550, 200)); Mouse(editor, "OnMouseUp", new(550, 200));
        Check(doc.Annotations.Where(a => a.Kind == AnnotationKind.Number).Select(a => a.Text).SequenceEqual(["1", "2"]), "Consecutive number clicks");
        Mouse(editor, "OnMouseDown", new(460, 210)); Mouse(editor, "OnMouseUp", new(460, 210));
        Mouse(editor, "OnMouseDoubleClick", new(460, 210));
        editor.Controls.OfType<NumericUpDown>().Single().Value = 15;
        editor.FinishTextEdit();
        Check(doc.Annotations.First(a => a.Kind == AnnotationKind.Number).Text == "15", "Number double click edit");
        doc.Undo(); Check(doc.Annotations.First(a => a.Kind == AnnotationKind.Number).Text == "1", "Number edit undo");
        Mouse(editor, "OnMouseDown", new(460, 210)); Mouse(editor, "OnMouseUp", new(460, 210));
        Mouse(editor, "OnMouseDoubleClick", new(460, 210));
        editor.Controls.OfType<NumericUpDown>().Single().Value = 99; editor.FinishTextEdit(true);
        Check(doc.Annotations.First(a => a.Kind == AnnotationKind.Number).Text == "1", "Number edit cancel");
        editor.Tool = AnnotationKind.Number; Mouse(editor, "OnMouseDown", new(650, 200));
        Check(doc.Annotations.Last().Text == "3", "Number sequence after undo/cancel");
        host.Close();
    }
    private static void TestRegionPaste()
    {
        using var source = new CaptureDocument(Sample(400, 300), "source.png");
        source.Annotations.Add(new Annotation { Kind = AnnotationKind.Highlight, Bounds = new(30, 30, 120, 80), Color = Color.Red });
        using var expected = source.Compose();
        var clipboard = new ClipboardService();
        Check(!clipboard.CopyRegion(source, Rectangle.Empty), "Empty region does not replace clipboard");
        Check(clipboard.CopyRegion(source, new(20, 20, 160, 100)), "Region copy succeeds");
        using (var copied = new Bitmap(Clipboard.GetImage()!))
        {
            Check(copied.Size == new Size(160, 100), "Region clipboard dimensions");
            Check(copied.GetPixel(40, 40).ToArgb() == expected.GetPixel(60, 60).ToArgb(), "Region includes composited annotations");
        }
        using var destination = new CaptureDocument(Sample(80, 60), "destination.png");
        var original = destination.Image;
        Check(clipboard.PasteImage(destination), "Image paste succeeds");
        var pasted = destination.Annotations.Single();
        Check(ReferenceEquals(original, destination.Image) && pasted.Kind == AnnotationKind.Image, "Paste preserves destination background");
        Check(pasted.Bounds.Width == 80 && pasted.Bounds.Height == 50, "Oversized paste fits with aspect ratio");
        using (var result = destination.Compose()) Check(result.GetPixel(20, 25).ToArgb() != original.GetPixel(20, 25).ToArgb(), "Pasted pixels render");
        using var host = new Form(); using var editor = new EditorCanvas(destination); host.Controls.Add(editor); host.Show();
        Drag(editor, new(20, 25), new(20, 30));
        Check(editor.Selected != null, "Pasted image is selectable");
        editor.DeleteSelection(); Check(destination.Annotations.Count == 0, "Pasted image deletable");
        destination.Undo(); Check(destination.Annotations.Count == 1, "Image delete undo");
        // Undo movement if one was recorded, then undo the paste.
        for (int i = 0; i < 3 && destination.Annotations.Count > 0; i++) destination.Undo();
        Check(destination.Annotations.Count == 0, "Paste is undone in one step after movement undo");
        Check(ReferenceEquals(original, destination.Image), "Paste undo retains original bitmap");
        editor.StartCrop(true); Drag(editor, new(5, 5), new(40, 30));
        Check(editor.RegionCopyMode && !editor.ApplyCrop() && destination.Image.Size == new Size(80, 60), "Copy selection cannot crop destination");
        host.Close();
        Console.WriteLine("PASS: composited region copy, image paste, fit, selection/delete/undo and destination preservation.");
    }
    private static void TestPixelEffects()
    {
        using var checker = new Bitmap(64, 48);
        for (int y = 0; y < checker.Height; y++) for (int x = 0; x < checker.Width; x++) checker.SetPixel(x, y, (x + y) % 2 == 0 ? Color.White : Color.Black);
        using (var mosaic = PixelEffectService.Create(checker, new(0, 0, 64, 48), PixelEffectKind.Mosaic, 8))
            Check(mosaic.GetPixel(1, 1).R == 127 && mosaic.GetPixel(7, 7).R == 127 && mosaic.GetPixel(63, 47).R == 127, "Mosaic averages blocks including edges");
        using var impulse = new Bitmap(9, 9); using (var g = Graphics.FromImage(impulse)) g.Clear(Color.Black); impulse.SetPixel(4, 4, Color.White);
        using (var blur = PixelEffectService.Create(impulse, new(0, 0, 9, 9), PixelEffectKind.Blur, 2))
            Check(blur.GetPixel(4, 4).R == 10 && blur.GetPixel(0, 0).R == 0, "Blur spreads impulse and preserves distant pixels");
        using var alpha = new Bitmap(1, 2); alpha.SetPixel(0, 0, Color.Transparent); alpha.SetPixel(0, 1, Color.Red);
        foreach (var effect in new[] { PixelEffectKind.Mosaic, PixelEffectKind.Blur })
        {
            using var transparent = PixelEffectService.Create(alpha, new(0, 0, 1, 2), effect, 64);
            Check(transparent.GetPixel(0, 0).R == 255 && transparent.GetPixel(0, 0).A == 127, "Tiny transparent region retains weighted color");
        }
        using var doc = new CaptureDocument(new Bitmap(checker), "effects.png");
        var shape = new Annotation { Kind = AnnotationKind.Rectangle, Color = Color.Red, Filled = true, Bounds = new(20, 10, 20, 12) }; doc.Annotations.Add(shape); doc.Saved();
        var region = new Rectangle(12, 8, 32, 24);
        using var before = doc.Compose();
        using var expected = PixelEffectService.Create(before, region, PixelEffectKind.Mosaic, 8);
        Check(!doc.ApplyPixelEffect(Rectangle.Empty, PixelEffectKind.Mosaic, 8) && !doc.Dirty, "Empty effect is no-op");
        using (var preview = doc.PreviewPixelEffect(region, PixelEffectKind.Mosaic, 8)) Check(!doc.Dirty, "Effect preview does not change document");
        Check(doc.ApplyPixelEffect(region, PixelEffectKind.Mosaic, 8), "Mosaic applies to composited image");
        using (var after = doc.Compose())
        {
            for (int y = 0; y < after.Height; y++) for (int x = 0; x < after.Width; x++)
                Check(after.GetPixel(x, y).ToArgb() == (region.Contains(x, y) ? expected.GetPixel(x - region.X, y - region.Y) : before.GetPixel(x, y)).ToArgb(), $"Effect changes only selected composited region at {x},{y}: {after.GetPixel(x, y)}");
            using var stream = new MemoryStream(); after.Save(stream, System.Drawing.Imaging.ImageFormat.Png); stream.Position = 0;
            using var png = new Bitmap(stream); Check(png.GetPixel(14, 10).ToArgb() == after.GetPixel(14, 10).ToArgb(), "PNG includes raster effect");
        }
        Check(doc.Annotations.Count(a => a.IsSelectable) == 1 && doc.Annotations[0].Id == shape.Id, "Raster effect preserves editable objects");
        using var destination = new CaptureDocument(new Bitmap(64, 48), "target.png"); var buffer = new ClipboardService(); buffer.CopyAnnotations(doc, false); buffer.Paste(destination);
        Check(destination.Annotations.Count == 1 && destination.Annotations[0].Kind == AnnotationKind.Rectangle, "Raster effect is excluded from annotation buffer");
        doc.Undo(); using (var undone = doc.Compose()) Check(undone.GetPixel(20, 12).ToArgb() == before.GetPixel(20, 12).ToArgb() && doc.Annotations.Count == 1, "Effect undo is one operation");
        using var host = new Form(); using var editor = new EditorCanvas(doc); host.Controls.Add(editor); host.Show();
        doc.Zoom = .5f; editor.UpdateSize(); editor.StartPixelEffect(PixelEffectKind.Blur);
        Drag(editor, new(22, 16), new(6, 4));
        Check(editor.CropBounds == region && !editor.ApplyCrop(), "Reverse effect selection uses image coordinates and cannot crop");
        editor.SetEffectStrength(6); Check(editor.ApplyPixelEffect() && doc.Image.Size == checker.Size, "Effect UI commits without resizing image");
        doc.Undo(); Check(doc.Annotations.Count == 1, "Blur undo restores objects");
        editor.StartPixelEffect(PixelEffectKind.Mosaic); Drag(editor, new(6, 4), new(22, 16)); editor.CancelCrop(); Check(doc.Annotations.Count == 1, "Effect cancel leaves document intact");
        host.Close();
        Console.WriteLine("PASS: mosaic/blur pixels, alpha/edge cases, composited region/PNG, independent object edits, copy exclusion, zoom selection and single undo.");
    }
    private static void TestAutoSave()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ASnapdo-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var doc = new CaptureDocument(Sample(200, 100), "capture.png");
            doc.Annotations.Add(new Annotation { Kind = AnnotationKind.Highlight, Bounds = new(0, 0, 100, 80), Color = Color.Red });
            string first = AutoSaveService.SaveOriginal(doc, folder);
            string second = AutoSaveService.SaveOriginal(doc, folder);
            Check(first != second && Path.GetFileName(second) == "capture_2.png", "Auto-save names do not overwrite");
            using (var saved = new Bitmap(first))
                Check(saved.Size == doc.Image.Size && saved.GetPixel(20, 20) == doc.Image.GetPixel(20, 20), "Auto-save saves original without annotations");
            Check(doc.Dirty && doc.SavePath == null, "Auto-save keeps edited-result save separate");
            using var form = new MainForm(); form.Show();
            var settings = (AppSettings)typeof(MainForm).GetField("settings", Private)!.GetValue(form)!;
            var save = typeof(MainForm).GetMethod("AutoSaveCapture", Private)!;
            settings.AutoSaveFolder = folder; settings.AutoSave = false;
            save.Invoke(form, [doc]); Check(Directory.GetFiles(folder).Length == 2, "Disabled auto-save creates no file");
            settings.AutoSave = true; save.Invoke(form, [doc]);
            Check(Directory.GetFiles(folder).Length == 3, "Enabled capture integration saves file");
            settings.AutoSaveFolder = first; save.Invoke(form, [doc]);
            Check(doc.Image.Width == 200, "Failed auto-save preserves document");
            typeof(MainForm).GetField("exiting", Private)!.SetValue(form, true); form.Close();
        }
        finally
        {
            if (Directory.Exists(folder)) { foreach (string path in Directory.GetFiles(folder)) File.Delete(path); Directory.Delete(folder); }
        }
        Console.WriteLine("PASS: original auto-save, collision safety, enabled/disabled capture integration and failure recovery.");
    }
    private static void TestSession()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ASnapdo-session-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "session.json");
        try
        {
            using var source = new CaptureDocument(Sample(400, 300), "session.png");
            source.Annotations.Add(new Annotation { Kind = AnnotationKind.Rectangle, Color = Color.Red, Bounds = new(20, 30, 120, 80), Filled = true });
            source.Checkpoint(); source.Annotations[0].Color = Color.Blue;
            source.Crop(new(10, 10, 300, 200)); source.Zoom = 2;
            source.ApplyPixelEffect(new(5, 5, 50, 50), PixelEffectKind.Blur, 4);
            using var expected = source.Compose();
            SessionService.Save([source], [new Point(120, 80)], 0, path);
            var restored = SessionService.Load(path);
            using var doc = restored.Documents.Single();
            Check(doc.Image.Size == new Size(300, 200) && doc.Zoom == 2 && doc.Annotations[0].Bounds == new RectangleF(10, 20, 120, 80) && doc.Annotations[0].Color.ToArgb() == Color.Blue.ToArgb(), "Session restores image, geometry, color and zoom");
            Check(restored.Scrolls.Single() == new Point(120, 80) && restored.SelectedIndex == 0, "Session restores viewport");
            using (var composed = doc.Compose()) Check(doc.Annotations.Count(a => a.Kind == AnnotationKind.PixelPatch) == 1 && composed.GetPixel(12, 22) == expected.GetPixel(12, 22), "Session restores raster effect pixels");
            doc.Undo(); Check(doc.Annotations.All(a => a.Kind != AnnotationKind.PixelPatch), "Session restores effect undo");
            doc.Undo(); Check(doc.Image.Size == new Size(400, 300), "Session restores pixel undo");
            doc.Undo(); Check(doc.Annotations[0].Color.ToArgb() == Color.Red.ToArgb(), "Session restores annotation undo");
        }
        finally { if (File.Exists(path)) File.Delete(path); if (Directory.Exists(folder)) Directory.Delete(folder); }
        Console.WriteLine("PASS: disk session roundtrip including image, annotation color/geometry, zoom/scroll and pixel/object undo.");
    }
    private static void TestOptions()
    {
        var legacy = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{}")!;
        Check(legacy.NewCaptureSelectRegion, "Old settings default to region selection");
        Check(!legacy.ShowGallery, "Gallery is hidden by default including old settings");
        var original = new AppSettings(); AppSettings? applied = null;
        bool permit = true; int probes = 0;
        using var options = new OptionsForm(original, value => { applied = value; return null; }, _ => { probes++; return new(permit, permit ? "Available (test)" : "Unavailable (test)"); });
        options.Show();
        var controls = Descendants(options).ToArray();
        controls.OfType<ComboBox>().Single(c => c.AccessibleName == "새 캡처 기본 동작").SelectedIndex = 1;
        var autoSave = controls.OfType<CheckBox>().Single(c => c.AccessibleName == "자동 저장");
        var folder = controls.OfType<TextBox>().Single();
        Check(!folder.Enabled, "Folder controls disabled when auto-save is off");
        autoSave.Checked = true; Check(folder.Enabled, "Enabling auto-save enables folder controls");
        folder.Text = Environment.CurrentDirectory;
        controls.OfType<CheckBox>().Single(c => c.AccessibleName == "캡처 갤러리").Checked = true;
        string output = Path.Combine(Environment.CurrentDirectory, "docs", "screenshots"); Directory.CreateDirectory(output);
        var selectors = controls.OfType<HotkeySelector>().ToArray();
        var actions = new[] { controls.OfType<ChromeButton>().Single(c => c.AccessibleName == "변경 저장"), controls.OfType<ChromeButton>().Single(c => c.AccessibleName == "다시 확인") };
        options.Controls.OfType<Panel>().Single(p => p.Controls.OfType<TabControl>().Any()).Controls.OfType<TabControl>().Single().SelectedIndex = 1;
        selectors[1].SelectedKeys = original.RegionHotkey;
        Check(!actions[0].Enabled, "Duplicate shortcuts disable save");
        selectors[1].SelectedKeys = original.FullscreenHotkey;
        Check(actions[0].Enabled && probes >= 4, "Selection triggers availability check");
        permit = false; actions[0].PerformClick();
        Check(applied == null && !actions[0].Enabled, "Save rechecks changed availability");
        permit = true; actions[1].PerformClick(); Check(actions[0].Enabled, "Recheck enables save");
        var pages = controls.OfType<TabControl>().Single();
        for (int i = 0; i < pages.TabCount; i++)
        {
            pages.SelectedIndex = i; Application.DoEvents();
            Save(options, Path.Combine(output, $"asnapdo-settings-{i}.png"));
        }
        Save(options, Path.Combine(output, "asnapdo-autosave-options.png"));
        actions[0].PerformClick();
        Check(applied != null && !applied.NewCaptureSelectRegion && original.NewCaptureSelectRegion, "Options apply separate value");
        var roundtrip = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(applied))!;
        Check(roundtrip.AutoSave && roundtrip.AutoSaveFolder == Environment.CurrentDirectory, "Auto-save checkbox and folder persist");
        Check(roundtrip.ShowGallery && !original.ShowGallery, "Gallery option persists without mutating original settings");
        Check(!roundtrip.NewCaptureSelectRegion && roundtrip.RegionHotkey == Keys.PrintScreen, "Settings serialization");
        Console.WriteLine("PASS: sequential numbers, number edit/undo/cancel, options UI and settings serialization.");
    }
    private static void TestCrop()
    {
        using var doc = new CaptureDocument(Sample(800, 600), "crop.png");
        var partial = new Annotation { Kind = AnnotationKind.Rectangle, Bounds = new(60, 70, 260, 180), Width = 6, Filled = true };
        doc.Annotations.Add(partial);
        doc.Annotations.Add(new Annotation { Kind = AnnotationKind.Text, Text = "Editable", Bounds = new(180, 130, 180, 80) });
        using var before = doc.Compose();
        Check(doc.Crop(new(100, 100, 400, 300)), "Crop applies");
        Check(doc.Image.Size == new Size(400, 300) && doc.Annotations[0].Bounds == new RectangleF(-40, -30, 260, 180), "Crop translates objects without flattening or scaling");
        using (var after = doc.Compose())
        {
            for (int y = 0; y < after.Height; y += 13)
                for (int x = 0; x < after.Width; x += 13)
                    Check(after.GetPixel(x, y) == before.GetPixel(x + 100, y + 100), "Composited crop pixels match source");
            using var stream = new MemoryStream(); after.Save(stream, System.Drawing.Imaging.ImageFormat.Png); stream.Position = 0;
            using var png = new Bitmap(stream); Check(png.Size == new Size(400, 300), "Saved PNG has cropped dimensions");
        }
        doc.Checkpoint(); doc.Annotations[0].Color = Color.Red; doc.Changed();
        doc.Undo(); Check(doc.Image.Size == new Size(400, 300) && doc.Annotations[0].Color == partial.Color, "Annotation undo retains cropped bitmap");
        Check(doc.Crop(new(20, 30, 200, 100)), "Second crop");
        doc.Undo(); Check(doc.Image.Size == new Size(400, 300), "Second crop undo");
        doc.Undo(); Check(doc.Image.Size == new Size(800, 600) && doc.Annotations[0].Bounds == partial.Bounds, "Original image and objects restored in one crop undo");
        Check(!doc.Crop(Rectangle.Empty) && !doc.Crop(new(0, 0, 800, 600)), "Empty/full-image crop is no-op");
        using var host = new Form(); using var editor = new EditorCanvas(doc); host.Controls.Add(editor); host.Show();
        doc.Zoom = .5f; editor.UpdateSize(); editor.StartCrop();
        Drag(editor, new(200, 150), new(50, 40));
        Check(editor.CropBounds == new Rectangle(100, 80, 300, 220), "Reverse drag crop at 50 percent uses image coordinates");
        Drag(editor, new(80, 70), new(90, 80));
        Check(editor.CropBounds == new Rectangle(120, 100, 300, 220), "Crop selection movement");
        Drag(editor, new(210, 160), new(220, 170));
        Check(editor.CropBounds == new Rectangle(120, 100, 320, 240), "Crop selection resize");
        editor.CancelCrop(); Check(doc.Image.Size == new Size(800, 600), "Cancel leaves pixels unchanged");
        editor.StartCrop(); Drag(editor, new(200, 150), new(50, 40));
        Check(editor.ApplyCrop() && editor.Size == new Size(150, 110), "Crop preserves chosen zoom");
        host.Close();
        Console.WriteLine("PASS: crop pixels/PNG dimensions, editable annotations, consecutive crop and edit undo, zoom coordinates, movement/resize/cancel.");
    }
    private static void TestHotkeyAvailability()
    {
        using var owner = new Form(); using var other = new Form();
        using var service = new GlobalHotkeyService(owner.Handle);
        using var competitor = new GlobalHotkeyService(other.Handle);
        var candidates = Enumerable.Range(13, 12).Select(n => Keys.Control | Keys.Alt | Keys.Shift | (Keys.F1 + n - 1)).ToArray();
        var held = candidates.First(k => service.Set(1, k));
        Check(service.CheckAvailability(held).Available, "Own registered shortcut available");
        Check(!competitor.CheckAvailability(held).Available, "Native registration detects occupied key");
        var free = candidates.First(k => k != held && service.CheckAvailability(k).Available);
        for (int n = 0; n < 10; n++) Check(service.CheckAvailability(free).Available, "Repeat probe cleanup");
        Check(competitor.Set(1, free), "Probe releases native registration");
        Check(!service.CheckAvailability(free).Available, "New conflict detected");
        Check(!competitor.Set(2, held), "Probe preserves original active shortcut");
        competitor.Dispose(); Check(service.CheckAvailability(free).Available, "Released conflict becomes available");
        Check(!service.CheckAvailability(Keys.ControlKey).Available && !service.CheckAvailability(Keys.F12).Available, "Invalid/system reserved keys rejected");
        using var options = new OptionsForm(new AppSettings { RegionHotkey = held, FullscreenHotkey = free }, _ => null, service.CheckAvailability);
        options.Show(); Application.DoEvents();
        Save(options, Path.Combine(Environment.CurrentDirectory, "docs", "screenshots", "asnapdo-hotkey-options.png"));
        options.Close();
        Console.WriteLine("PASS: Windows RegisterHotKey own/occupied/free checks, probe cleanup, original registration retained, options duplicate and save-time recheck.");
    }
    private static void TestSelectionOverlay()
    {
        using var source = Sample(900, 600);
        using var overlay = new SelectionForm(new Bitmap(source), new Rectangle(40, 40, 900, 600));
        overlay.Show(); Application.DoEvents();
        void Send(string method, Point p, MouseButtons button = MouseButtons.Left) => typeof(SelectionForm).GetMethod(method, Private)!.Invoke(overlay, [new MouseEventArgs(button, 1, p.X, p.Y, 0)]);
        Send("OnMouseDown", new(650, 390)); Send("OnMouseMove", new(170, 140));
        Check(overlay.SelectedBounds == new Rectangle(210, 180, 480, 250), "Reverse drag screen coordinates and size");
        Save(overlay, Path.Combine(Environment.CurrentDirectory, "docs", "screenshots", "asnapdo-region-guides.png"));
        Send("OnMouseUp", new(170, 140));
        using var captured = overlay.CaptureSelection();
        Check(captured.Size == new Size(480, 250), "Region crop dimensions");
        for (int y = 0; y < captured.Height; y += 7)
            for (int x = 0; x < captured.Width; x += 7)
                Check(captured.GetPixel(x, y) == source.GetPixel(x + 170, y + 140), "Captured pixels exclude guides, shade and HUD");
        overlay.Close();
        using var negative = new SelectionForm(Sample(400, 300), new Rectangle(-400, -300, 400, 300));
        void NegativeMouse(string method, Point p, MouseButtons button = MouseButtons.Left) => typeof(SelectionForm).GetMethod(method, Private)!.Invoke(negative, [new MouseEventArgs(button, 1, p.X, p.Y, 0)]);
        NegativeMouse("OnMouseDown", new(350, 250)); NegativeMouse("OnMouseUp", new(-20, -30));
        Check(negative.SelectedBounds == new Rectangle(-400, -300, 350, 250), "Negative desktop origin and boundary clamp");
        using var cancelled = new SelectionForm(Sample(100, 100), new Rectangle(0, 0, 100, 100));
        var down = typeof(SelectionForm).GetMethod("OnMouseDown", Private)!;
        var up = typeof(SelectionForm).GetMethod("OnMouseUp", Private)!;
        var click = new MouseEventArgs(MouseButtons.Left, 1, 20, 20, 0);
        down.Invoke(cancelled, [click]); up.Invoke(cancelled, [click]);
        Check(cancelled.DialogResult != DialogResult.OK, "Empty click does not capture");
        down.Invoke(cancelled, [new MouseEventArgs(MouseButtons.Right, 1, 20, 20, 0)]);
        Check(cancelled.DialogResult == DialogResult.Cancel, "Right click cancels capture");
        Console.WriteLine("PASS: selection overlay reverse drag, size/coordinates, original crop pixels, negative origin, boundary clamp and cancellation.");
    }
    private static void Mouse(EditorCanvas e, string method, Point p) => typeof(EditorCanvas).GetMethod(method, Private)!.Invoke(e, [new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0)]);
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls) { yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
    }
    private static void TestCaptureBlockedBySettings(MainForm form)
    {
        Exception? failure = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 100 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var options = form.OwnedForms.OfType<OptionsForm>().Single();
            try
            {
                Check(options.Modal, "Settings opens as modal dialog");
                foreach (bool region in new[] { true, false })
                {
                    var task = (Task)typeof(MainForm).GetMethod("CaptureAsync", Private)!.Invoke(form, [region])!;
                    Check(task.IsCompletedSuccessfully && !(bool)typeof(MainForm).GetField("capturing", Private)!.GetValue(form)!, "All capture entry points blocked by settings");
                    var message = Message.Create(form.Handle, 0x0312, new IntPtr(region ? 1 : 2), IntPtr.Zero);
                    typeof(MainForm).GetMethod("WndProc", Private)!.Invoke(form, [message]);
                    Check(!(bool)typeof(MainForm).GetField("capturing", Private)!.GetValue(form)!, "Global hotkey ignored during settings");
                }
                Check(options.Visible, "Blocked capture keeps settings visible");
            }
            catch (Exception e) { failure = e; }
            finally { options.Close(); }
        };
        timer.Start(); typeof(MainForm).GetMethod("Configure", Private)!.Invoke(form, null);
        if (failure != null) throw failure;
        Check(form.Enabled, "Closing settings restores owner");
        Console.WriteLine("PASS: modal settings blocks region/full capture and WM_HOTKEY without closing settings.");
    }
    private static void TestStartupOptions()
    {
        List<bool> changes = []; bool fail = true;
        using var options = new OptionsForm(new AppSettings(), _ => fail ? "Test save failure" : null, _ => new(true, "Available"), false, changes.Add);
        options.Show(); Application.DoEvents();
        var controls = Descendants(options).ToArray();
        controls.OfType<TabControl>().Single().SelectedIndex = 3;
        Application.DoEvents();
        Save(options, Path.Combine(Environment.CurrentDirectory, "docs", "screenshots", "asnapdo-settings-general.png"));
        controls.OfType<CheckBox>().Single(c => c.AccessibleName == "자동 실행").Checked = true;
        var save = controls.OfType<ChromeButton>().Single(c => c.AccessibleName == "변경 저장");
        save.PerformClick(); Check(changes.SequenceEqual([true, false]) && options.Visible, "Failed settings save rolls back startup registration");
        fail = false; save.PerformClick(); Check(changes.SequenceEqual([true, false, true]), "Successful settings save applies startup option");
        Console.WriteLine("PASS: startup checkbox apply and rollback when settings save fails.");
    }
    private static void Drag(EditorCanvas e, Point from, Point to) { Mouse(e, "OnMouseDown", from); Mouse(e, "OnMouseMove", to); Mouse(e, "OnMouseUp", to); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Save(Form form, string path) { using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new(Point.Empty, bitmap.Size)); bitmap.Save(path); }
    private static Bitmap Sample(int w, int h)
    {
        var b = new Bitmap(w, h); using var g = Graphics.FromImage(b); g.Clear(Color.White);
        using var title = new Font("Segoe UI", 30, FontStyle.Bold, GraphicsUnit.Pixel);
        using var body = new Font("Segoe UI", 18, GraphicsUnit.Pixel);
        g.DrawString("ASnapdo workspace", title, Brushes.MidnightBlue, 64, 60);
        g.DrawString("Synthetic capture for interaction verification", body, Brushes.SlateGray, 66, 112);
        return b;
    }
}
