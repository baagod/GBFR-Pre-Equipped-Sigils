using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace LoadoutTool;

/// <summary>
/// Trait picker built on the standard editable ComboBox (its dropdown follows
/// scrolling natively). Typing filters the list; only list items can become
/// the value; invalid free text reverts on focus loss.
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
    private readonly ICollectionView _view;

    public TraitPicker()
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(TraitData.Names);
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

    private void OnSelectedTraitChanged()
    {
        if (!_pickingOption && Picker.Text != SelectedTrait)
            Picker.Text = SelectedTrait;
    }

    private void Picker_TextChanged(object sender, TextChangedEventArgs e)
    {
        string keyword = Picker.Text.Trim();
        _view.Filter = null;
        if (keyword.Length > 0)
        {
            _view.Filter = item =>
                item is string name &&
                name.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }
        _view.Refresh();
        // While the user edits, do not write the value yet (only on selection).
    }

    private void Picker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Picker.SelectedItem is not string selected)
            return;
        _pickingOption = true;
        SelectedTrait = selected;
        _pickingOption = false;
    }

    private void Picker_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_pickingOption)
            return;
        // Revert invalid free text back to the selected value.
        if (!TraitData.Names.Contains(Picker.Text))
            Picker.Text = SelectedTrait;
    }
}
