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

        private List<Personnel> _allItems = new List<Personnel>();

        public SearchablePersonPicker()
        {
            InitializeComponent();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = d as SearchablePersonPicker;
            if (picker != null && e.NewValue is IEnumerable enumerable)
            {
                picker._allItems = enumerable.OfType<Personnel>().ToList();
                picker.ApplyFilter(picker.FilterTextBox?.Text ?? string.Empty);
            }
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = d as SearchablePersonPicker;
            if (picker != null)
            {
                if (e.NewValue is Personnel p)
                {
                    picker.SelectedTextDisplay.Text = $"{p.FullName} ({p.MilitaryServiceNumber})";
                }
                else
                {
                    picker.SelectedTextDisplay.Text = "Επιλέξτε Στέλεχος / Οπλίτη...";
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

        private void ApplyFilter(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                PersonnelListBox.ItemsSource = _allItems;
            }
            else
            {
                string s = query.Trim();
                PersonnelListBox.ItemsSource = _allItems.Where(item =>
                    (item.LastName?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (item.FirstName?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (item.MilitaryServiceNumber?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (item.Specialty?.IndexOf(s, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0).ToList();
            }
        }

        private void PersonnelListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PersonnelListBox.SelectedItem != null)
            {
                SelectedItem = PersonnelListBox.SelectedItem;
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
            if (e.Key == Key.Enter && PersonnelListBox.SelectedItem != null)
            {
                SelectedItem = PersonnelListBox.SelectedItem;
                SearchPopup.IsOpen = false;
            }
            else if (e.Key == Key.Escape)
            {
                SearchPopup.IsOpen = false;
            }
        }
    }
}
