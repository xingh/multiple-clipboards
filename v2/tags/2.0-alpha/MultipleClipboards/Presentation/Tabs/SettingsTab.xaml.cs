﻿using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ApplicationBasics;
using MultipleClipboards.Entities;

namespace MultipleClipboards.Presentation.Tabs
{
	/// <summary>
	/// Interaction logic for SettingsTab.xaml
	/// </summary>
	public partial class SettingsTab : UserControl
	{
		public SettingsTab()
		{
			InitializeComponent();
			this.BindModifierKeyOneComboBox();
			this.AddNewClipboardButton.Click += this.AddNewClipboardButton_Click;
		}

		private void BindModifierKeyOneComboBox()
		{
			ICollectionView view = new CollectionViewSource
			{
				Source = Enum.GetValues(typeof(ModifierKeys))
			}.View;
			view.Filter = item => (ModifierKeys)item != ModifierKeys.None;
			view.MoveCurrentTo(ModifierKeys.Control);
			this.ModifierKeyOneComboBox.ItemsSource = view;
		}

		private void AddNewClipboardButton_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			ClipboardDefinition clipboard = new ClipboardDefinition
			{
				ModifierOneKey = Enum<ModifierKeys>.Parse(this.ModifierKeyOneComboBox.SelectedValue.ToString()),
				ModifierTwoKey = Enum<ModifierKeys>.Parse(this.ModifierKeyTwoComboBox.SelectedValue.ToString()),
				CopyKey = Enum<Key>.Parse(this.CopyKeyTextBox.Text),
				CutKey = Enum<Key>.Parse(this.CutKeyTextBox.Text),
				PasteKey = Enum<Key>.Parse(this.PasteKeyTextBox.Text)
			};

			AppController.ClipboardManager.AddClipboard(clipboard);
		}

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			ClipboardDefinition clipboard = ((FrameworkElement)sender).DataContext as ClipboardDefinition;

			if (clipboard != null)
			{
				AppController.ClipboardManager.RemoveClipboard(clipboard);
			}
		}
	}
}
