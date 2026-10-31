using System.ComponentModel;

namespace ASnapdo.UI;

public sealed class HotkeySelector : UserControl
{
    private sealed record Choice(string Label, Keys Keys) { public override string ToString() => Label; }
    private readonly ComboBox modifiers = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "조합 키" };
    private readonly ComboBox key = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "기본 키" };
    public event EventHandler? SelectionChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Keys SelectedKeys
    {
        get => ((Choice)modifiers.SelectedItem!).Keys | ((Choice)key.SelectedItem!).Keys;
        set
        {
            Select(modifiers, value & Keys.Modifiers);
            Select(key, value & Keys.KeyCode);
        }
    }
    public HotkeySelector(Keys initial)
    {
        Width = 400; Height = 34; Margin = new Padding(0, 0, 0, 3);
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        row.Controls.Add(modifiers, 0, 0); row.Controls.Add(key, 1, 0); Controls.Add(row);
        modifiers.Items.AddRange(new object[]
        {
            new Choice("조합 키 없음", Keys.None), new Choice("Ctrl", Keys.Control), new Choice("Shift", Keys.Shift), new Choice("Alt", Keys.Alt),
            new Choice("Ctrl + Shift", Keys.Control | Keys.Shift), new Choice("Ctrl + Alt", Keys.Control | Keys.Alt),
            new Choice("Alt + Shift", Keys.Alt | Keys.Shift), new Choice("Ctrl + Alt + Shift", Keys.Control | Keys.Alt | Keys.Shift)
        });
        key.Items.Add(new Choice("Print Screen", Keys.PrintScreen));
        for (int n = 1; n <= 24; n++) if (n != 12) key.Items.Add(new Choice($"F{n}", Keys.F1 + (n - 1)));
        for (int n = 0; n <= 9; n++) key.Items.Add(new Choice(n.ToString(), Keys.D0 + n));
        for (int n = 0; n < 26; n++) key.Items.Add(new Choice(((char)('A' + n)).ToString(), Keys.A + n));
        foreach (var k in new[] { Keys.Insert, Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown, Keys.Pause }) key.Items.Add(new Choice(k.ToString(), k));
        SelectedKeys = initial;
        modifiers.SelectedIndexChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        key.SelectedIndexChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private static void Select(ComboBox box, Keys value)
    {
        var item = box.Items.Cast<Choice>().FirstOrDefault(x => x.Keys == value);
        if (item == null) { item = new Choice(value.ToString(), value); box.Items.Add(item); }
        box.SelectedItem = item;
    }
}
