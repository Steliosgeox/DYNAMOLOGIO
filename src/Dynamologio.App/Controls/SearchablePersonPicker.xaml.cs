using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dynamologio.Core.Models;

namespace Dynamologio.App.Controls
{
    public class PersonPickerItem
    {
        public object SourceItem { get; set; }
        public string DisplayRank { get; set; } = string.Empty;
        public string DisplayFullName { get; set; } = string.Empty;
        public string DisplayUnit { get; set; } = string.Empty;
        public string DisplayAsm { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;

        public override string ToString() => $"{DisplayRank} {DisplayFullName} - {DisplayUnit} ({DisplayAsm})".Trim();
    }

    public partial class SearchablePersonPicker : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(SearchablePersonPicker), new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(SearchablePersonPicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        private readonly List<PersonPickerItem> _allItems = new List<PersonPickerItem>();

        public IReadOnlyList<PersonPickerItem> FilteredItems
        {
            get
            {
                if (PersonnelListBox?.ItemsSource is IEnumerable<PersonPickerItem> list)
                {
                    return list.ToList();
                }
                return _allItems;
            }
        }

        public SearchablePersonPicker()
        {
            InitializeComponent();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = d as SearchablePersonPicker;
            if (picker != null)
            {
                picker.RebuildItemList(e.NewValue as IEnumerable);
            }
        }

        public void RebuildItemList(IEnumerable rawItems)
        {
            _allItems.Clear();
            if (rawItems == null)
            {
                ApplyFilter(string.Empty);
                return;
            }

            foreach (var item in rawItems)
            {
                if (item is PersonnelLookupItem lookup)
                {
                    _allItems.Add(new PersonPickerItem
                    {
                        SourceItem = lookup,
                        DisplayRank = lookup.RankName ?? "",
                        DisplayFullName = lookup.FullName ?? "",
                        DisplayUnit = lookup.UnitName ?? "",
                        DisplayAsm = lookup.MilitaryServiceNumber ?? "",
                        Specialty = lookup.Specialty ?? ""
                    });
                }
                else if (item is Personnel p)
                {
                    _allItems.Add(new PersonPickerItem
                    {
                        SourceItem = p,
                        DisplayRank = "",
                        DisplayFullName = p.FullName,
                        DisplayUnit = p.CompanyOrSection ?? "",
                        DisplayAsm = p.MilitaryServiceNumber ?? "",
                        Specialty = p.Specialty ?? ""
                    });
                }
                else if (item is PersonPickerItem ppi)
                {
                    _allItems.Add(ppi);
                }
                else
                {
                    throw new InvalidOperationException($"SearchablePersonPicker does not support type {item.GetType().Name}. Use PersonnelLookupItem instead.");
                }
            }

            ApplyFilter(FilterTextBox?.Text ?? string.Empty);
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = d as SearchablePersonPicker;
            if (picker != null)
            {
                if (e.NewValue == null)
                {
                    picker.SelectedTextDisplay.Text = "Επιλέξτε Στέλεχος / Οπλίτη...";
                }
                else
                {
                    var match = picker._allItems.FirstOrDefault(i => ReferenceEquals(i.SourceItem, e.NewValue) || Equals(i.SourceItem, e.NewValue) || ReferenceEquals(i, e.NewValue));
                    if (match != null)
                    {
                        string prefix = !string.IsNullOrEmpty(match.DisplayRank) ? $"[{match.DisplayRank}] " : "";
                        string suffix = !string.IsNullOrEmpty(match.DisplayAsm) ? $" ({match.DisplayAsm})" : "";
                        picker.SelectedTextDisplay.Text = $"{prefix}{match.DisplayFullName}{suffix}".Trim();
                    }
                    else if (e.NewValue is Personnel p)
                    {
                        picker.SelectedTextDisplay.Text = $"{p.FullName} ({p.MilitaryServiceNumber})";
                    }
                    else
                    {
                        picker.SelectedTextDisplay.Text = e.NewValue.ToString();
                    }
                }
            }
        }

        private void DisplayBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SearchPopup.IsOpen = true;
            FilterTextBox.Focus();
            FilterTextBox.SelectAll();
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(FilterTextBox.Text);
        }

        public void ApplyFilter(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                if (PersonnelListBox != null)
                {
                    PersonnelListBox.ItemsSource = _allItems;
                }
            }
            else
            {
                string s = query.Trim();
                // 5-dimensional search: Rank, LastName/FullName, Unit, ASM, Specialty
                var filtered = _allItems.Where(item =>
                    (item.DisplayRank?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (item.DisplayFullName?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (item.DisplayUnit?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (item.DisplayAsm?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (item.Specialty?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0).ToList();

                if (PersonnelListBox != null)
                {
                    PersonnelListBox.ItemsSource = filtered;
                }
            }
        }

        private void PersonnelListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PersonnelListBox.SelectedItem is PersonPickerItem pickerItem)
            {
                SelectedItem = pickerItem.SourceItem ?? pickerItem;
                SearchPopup.IsOpen = false;
            }
        }

        private void FilterTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && PersonnelListBox.Items.Count > 0)
            {
                PersonnelListBox.Focus();
                PersonnelListBox.SelectedIndex = 0;
            }
            else if (e.Key == Key.Escape)
            {
                SearchPopup.IsOpen = false;
            }
        }

        private void PersonnelListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && PersonnelListBox.SelectedItem is PersonPickerItem pickerItem)
            {
                SelectedItem = pickerItem.SourceItem ?? pickerItem;
                SearchPopup.IsOpen = false;
            }
            else if (e.Key == Key.Escape)
            {
                SearchPopup.IsOpen = false;
            }
        }
    }
}
