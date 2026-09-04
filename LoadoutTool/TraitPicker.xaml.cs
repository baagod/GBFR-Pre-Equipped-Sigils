using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoadoutTool;

/// <summary>
/// Simple in-layout picker: click opens the full trait list below the text box
/// (inline, so scrolling works and nothing floats), typing filters it by
/// "contains", clicking an entry fills the box. The value only ever comes from
/// a list selection.
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

    public TraitPicker()
    {
        InitializeComponent();
    }

    public string SelectedTrait
    {
        get => (string)GetValue(SelectedTraitProperty);
        set => SetValue(SelectedTraitProperty, value);
    }

    private void OnSelectedTraitChanged()
    {
        if (_pickingOption)
            return;
        if (Input.Text != SelectedTrait)
            Input.Text = SelectedTrait;

        Options.Visibility = Visibility.Collapsed;
    }

    private void Input_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        RefreshOptions(Input.Text.Trim());
        Options.Visibility = Visibility.Visible;
        Options.Focus();
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        Placeholder.Visibility = Input.Text.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (Options.Visibility == Visibility.Visible)
            RefreshOptions(Input.Text.Trim());
        // The value is only ever written by Options_SelectionChanged.
    }

    private void RefreshOptions(string filter)
    {
        var list = new List<string>();
        foreach (string name in TraitData.Names)
        {
            if (filter.Length == 0 || name.Contains(filter, System.StringComparison.OrdinalIgnoreCase))
                list.Add(name);
        }
        Options.ItemsSource = list;
    }

    private void Input_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_pickingOption)
            return;
        Options.Visibility = Visibility.Collapsed;
        // Revert invalid free text back to the selected value.
        if (!TraitData.Names.Contains(Input.Text))
            Input.Text = SelectedTrait;
    }

    private void Options_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _pickingOption = true;
    }

    private void Options_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Options.SelectedItem is not string selected)
            return;
        _pickingOption = true;
        SelectedTrait = selected;
        Input.Text = selected;
        _pickingOption = false;
        Options.Visibility = Visibility.Collapsed;
    }
}
