using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoadoutTool;

/// <summary>
/// Searchable trait picker: clicking opens the full list, typing filters it by
/// "contains" (e.g. "白龙" shows "白龙 xxx" entries), clicking an entry fills
/// the text box.
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

    private void OnSelectedTraitChanged()
    {
        if (!_pickingOption && Input.Text != SelectedTrait)
            Input.Text = SelectedTrait;
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshOptions();
        if (!_pickingOption && SelectedTrait != Input.Text)
            SelectedTrait = Input.Text;
    }

    private void RefreshOptions()
    {
        var list = new List<string>();
        string keyword = Input.Text.Trim();
        foreach (string name in TraitData.Names)
        {
            if (keyword.Length == 0 || name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                list.Add(name);
        }
        Options.ItemsSource = list;
        if (keyword.Length > 0 && !Dropdown.IsOpen)
            Dropdown.IsOpen = true;
    }

    private void Input_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        RefreshOptions();
        Dropdown.IsOpen = true;
    }

    private void Input_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_pickingOption)
            Dropdown.IsOpen = false;
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
        OnSelectedTraitChanged();
        _pickingOption = false;
        Dropdown.IsOpen = false;
        Input.Focus();
    }
}
