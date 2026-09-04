using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoadoutTool;

/// <summary>
/// Search-only trait picker: the text box is a filter (never a value), the
/// value can only be chosen from the filtered list (mirrors the in-game sigil
/// picker: click to open, type to filter, click an entry to select).
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
        Options.ItemsSource = TraitData.Names;
    }

    public string SelectedTrait
    {
        get => (string)GetValue(SelectedTraitProperty);
        set => SetValue(SelectedTraitProperty, value);
    }

    private string Filter => Input.Text.Trim();

    private void OnSelectedTraitChanged()
    {
        if (!_pickingOption && Input.Text != SelectedTrait)
            Input.Text = SelectedTrait;
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Typing only filters the list; it never becomes the value.
        RefreshOptions();
        Placeholder.Visibility = Input.Text.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // User is typing: show the filtered list. Programmatic text changes
        // (initialization) never open the popup.
        if (!Dropdown.IsOpen)
        {
            RefreshOptions();
            Dropdown.IsOpen = true;
        }
    }

    private void RefreshOptions()
    {
        var list = new List<string>();
        string filter = Filter;
        foreach (string name in TraitData.Names)
        {
            if (filter.Length == 0 || name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                list.Add(name);
        }
        Options.ItemsSource = list;
    }

    private void Input_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        RefreshOptions();
        Dropdown.IsOpen = true;
    }

    private void Input_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_pickingOption)
            return;
        Dropdown.IsOpen = false;
        // Revert invalid free text back to the selected value.
        if (Input.Text != SelectedTrait)
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
        Dropdown.IsOpen = false;
    }
}
