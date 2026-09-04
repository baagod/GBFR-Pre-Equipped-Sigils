using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace LoadoutTool;

/// <summary>
/// Trait picker built on the standard editable ComboBox (its dropdown follows
/// scrolling natively). Typing filters the list; only list items can become
/// the value; invalid free text reverts on focus loss. Each picker owns an
/// independent filtered view (the shared trait list must not leak filters).
/// </summary>
public partial class TraitPicker : UserControl
{
    public static readonly DependencyProperty SelectedTraitProperty =
        DependencyProperty.Register(
            nameof(SelectedTrait), typeof(string), typeof(TraitPicker),
            new FrameworkPropertyMetadata(
                "",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, _) => ((TraitPicker)d).OnSelectedTraitChanged()));

    private bool _pickingOption;
    private bool _suppressFilter;
    private readonly ListCollectionView _view;

    public TraitPicker()
    {
        InitializeComponent();
        // Independent view per control: GetDefaultView(shared list) would share
        // one view across all pickers and leak filters between them.
        _view = new ListCollectionView(new List<string>(TraitData.Names));
        Picker.ItemsSource = _view;
        // The editable ComboBox's inner TextBox bubbles TextBase.TextChanged.
        Picker.AddHandler(
            TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(Picker_TextChanged));
    }

    public string SelectedTrait
    {
        get => (string)GetValue(SelectedTraitProperty);
        set => SetValue(SelectedTraitProperty, value);
    }

    private void SetText(string text)
    {
        _suppressFilter = true;
        Picker.Text = text;
        _suppressFilter = false;
    }

    private void OnSelectedTraitChanged()
    {
        if (!_pickingOption && Picker.Text != SelectedTrait)
            SetText(SelectedTrait);
    }

    private void Picker_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFilter)
        {
            // Programmatic value writes: show the full list next open.
            _view.Filter = null;
            _view.Refresh();
            return;
        }
        string keyword = Picker.Text.Trim();
        _view.Filter = keyword.Length == 0
            ? null
            : item =>
                item is string name &&
                name.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        _view.Refresh();
    }

    private void Picker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Picker.SelectedItem is not string selected)
            return;
        _pickingOption = true;
        SelectedTrait = selected;
        SetText(selected);
        _pickingOption = false;
    }

    private void Picker_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_pickingOption)
            return;
        // Revert invalid free text back to the selected value.
        if (!ListContains(Picker.Text))
            SetText(SelectedTrait);
    }

    private static bool ListContains(string name) =>
        !string.IsNullOrEmpty(name) && TraitData.Names.Contains(name);
}
