﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MultipleClipboards.Entities;
using MultipleClipboards.Messaging;
using MultipleClipboards.Presentation.Icons;
using log4net;
using wyDay.Controls;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Forms.ContextMenu;
using MenuItem = System.Windows.Forms.MenuItem;
using Timer = System.Timers.Timer;

namespace MultipleClipboards.Presentation.TrayIcon
{
	public sealed class TrayIconManager : IDisposable
	{
		private const string ClipboardEmptyMenuItemText = "Clipboard is empty";
		private static readonly ILog log = LogManager.GetLogger(typeof(TrayIconManager));
		private readonly IDictionary<ulong, MenuItem> menuItemsByClipboardDataId;
		private readonly NotifyIcon notifyIcon;
		private readonly VistaMenu menuHelper;
		private readonly Timer trayPopupTimer;
		private ContextMenu contextMenu;
		private MenuItem clipboardsMenuItem;
		private Popup trayPopup;
		private Border trayPopupBorder;
		private Image trayPopupIcon;
		private TextBlock trayPopupTextBlock;

		public TrayIconManager(NotifyIcon notifyIcon)
		{
			this.menuHelper = new VistaMenu();
			this.menuItemsByClipboardDataId = new Dictionary<ulong, MenuItem>();
			this.notifyIcon = notifyIcon;
			this.notifyIcon.MouseDoubleClick += NotifyIconMouseDoubleClick;
			this.trayPopupTimer = new Timer(5000);
			this.trayPopupTimer.Elapsed += (sender, args) => AppController.ExecuteOnUiThread(OnTrayPopupTimerStop);
			this.CacheTrayPopupElements();
			this.trayPopup.CustomPopupPlacementCallback = GetPopupPlacement;
			MessageBus.Instance.Subscribe<TrayNotification>(this.NotificationRecieved);
		}

		public void OnClipboardManagerInitialized()
		{
			AppController.ClipboardManager.ClipboardHistory.ObservableCollection.CollectionChanged += ClipboardHistoryCollectionChanged;
			AppController.ClipboardManager.AvailableClipboards.CollectionChanged += (sender, args) => this.RebuildClipboardsMenuItem();
			this.InitializeContextMenu();
			this.RebuildClipboardsMenuItem();
		}

		public void ShowTrayIcon()
		{
			this.notifyIcon.Visible = true;
		}

		public void HideTrayIcon()
		{
			this.notifyIcon.Visible = false;
		}

		public void Dispose()
		{
			this.notifyIcon.Dispose();
		}

		private static CustomPopupPlacement[] GetPopupPlacement(Size popupSize, Size targetSize, Point offset)
		{
			var point = SystemParameters.WorkArea.BottomRight;
			point.Y = point.Y - popupSize.Height;
			return new[] { new CustomPopupPlacement(point, PopupPrimaryAxis.Horizontal) };
		}

		private void CacheTrayPopupElements()
		{
			// HACK: This is kind of ugly, and there may be a better way to do this, but for such a simple control I do not care.
			//		 I need to find the child elements in the popup that have dynamic content, but VisualTreeHelper is of no use
			//		 because the popup has not been shown yet.  But, this is a popup that I have defined in my own resource dictionary,
			//		 so I will just find the controls by manually traversing the tree;
			//		 There are a lot of potential null refference exceptions in here, but that's OK since this would fail later on anyway without them.

			this.trayPopup = (Popup)Application.Current.FindResource("TrayPopup");
			this.trayPopupBorder = (Border)trayPopup.Child;
			var grid = (Grid)this.trayPopupBorder.Child;
			this.trayPopupIcon = (Image)grid.Children[1];
			this.trayPopupTextBlock = (TextBlock)grid.Children[2];
		}

		private void ClipboardHistoryCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			try
			{
				if (e.Action == NotifyCollectionChangedAction.Reset)
				{
					foreach (ulong clipboardDataId in this.menuItemsByClipboardDataId.Keys.ToList())
					{
						this.RemoveClipboardHistoryMenuItem(clipboardDataId);
					}

					return;
				}

				// First, remove all items from the menu that have been cleared from the history queue.
				if (e.OldItems != null)
				{
					foreach (var clipboardData in e.OldItems.Cast<ClipboardData>())
					{
						this.RemoveClipboardHistoryMenuItem(clipboardData.Id);
					}
				}

				// Now add the new ones.
				if (e.NewItems != null)
				{
					this.AddClipboardHistoryItemsToConextMenu(e.NewItems.Cast<ClipboardData>());
				}
			}
			catch (Exception exception)
			{
				log.Error("There was an error updating the tray icon context menu after the clipboard history collection was changed.", exception);
				MessageBus.Instance.Publish(new TrayNotification
				{
					MessageBody = "Unexpected error updating the clipboard history context menu.  Data may be inaccurate.",
					IconType = IconType.Error
				});
			}
		}

		private static void NotifyIconMouseDoubleClick(object sender, MouseEventArgs e)
		{
			AppController.ShowMainWindow();
		}

		private void InitializeContextMenu()
		{
			var exitMenuItem = new MenuItem("Exit", (sender, args) => AppController.Shutdown());
			var mainWindowMenuItem = new MenuItem("Multiple Clipboards...", (sender, args) => AppController.ShowMainWindow())
			{
				DefaultItem = true
			};

			var clearHistoryMenuItem = new MenuItem("Clear History", (sender, args) => AppController.ClipboardManager.ClearClipboardHistory());
			this.clipboardsMenuItem = new MenuItem("Clipboards");
			var seperator = new MenuItem("-");
			this.contextMenu = new ContextMenu(new[] { seperator, clipboardsMenuItem, clearHistoryMenuItem, mainWindowMenuItem, exitMenuItem });

			this.menuHelper.SetImage(exitMenuItem, IconFactory.GetTrayContextMenuBitmap(IconType.Exit));
			this.menuHelper.SetImage(mainWindowMenuItem, IconFactory.GetTrayContextMenuBitmap(IconType.Clipboard));
			this.menuHelper.SetImage(clearHistoryMenuItem, IconFactory.GetTrayContextMenuBitmap(IconType.Clear));
			this.menuHelper.Refresh();

			this.notifyIcon.ContextMenu = this.contextMenu;
		}

		private void RebuildClipboardsMenuItem()
		{
			while (this.clipboardsMenuItem.MenuItems.Count > 0)
			{
				var menuItem = this.clipboardsMenuItem.MenuItems[0];
				menuItem.Popup -= OnClipboardMenuItemPopup;

				while (menuItem.MenuItems.Count > 0)
				{
					var subMenuItem = menuItem.MenuItems[0];
					menuItem.MenuItems.Remove(subMenuItem);
					this.menuHelper.RemoveMenuItem(subMenuItem);
					subMenuItem.Dispose();
				}

				menuItem.Dispose();
			}

			this.clipboardsMenuItem.MenuItems.Clear();

			foreach (var clipborad in AppController.ClipboardManager.AvailableClipboards)
			{
				int clipboardId = clipborad.ClipboardId;
				var clearClipboardMenuItem = new MenuItem("Clear Clipboard", (sender, args) => AppController.ClipboardManager.ClearClipboardContents(clipboardId));
				var clipboardMenuItem = new ClipboardMenuItem(clipborad.ToString(), clipboardId, new[] { clearClipboardMenuItem });
				clipboardMenuItem.Popup += OnClipboardMenuItemPopup;
				this.clipboardsMenuItem.MenuItems.Add(clipboardMenuItem);
				this.menuHelper.SetImage(clearClipboardMenuItem, IconFactory.GetTrayContextMenuBitmap(IconType.Clear));
			}
		}

		private void OnClipboardMenuItemPopup(object sender, EventArgs args)
		{
			var parentMenuItem = (ClipboardMenuItem)sender;

			if (parentMenuItem.MenuItems.Count > 1)
			{
				var itemToRemove = parentMenuItem.MenuItems[0];
				parentMenuItem.MenuItems.Remove(itemToRemove);
				this.menuHelper.RemoveMenuItem(itemToRemove);
				itemToRemove.Dispose();
			}

			var data = AppController.ClipboardManager.GetClipboardDataByClipboardId(parentMenuItem.ClipboardId);
			var dataPreviewMenuItem = new MenuItem(data == null ? ClipboardEmptyMenuItemText : data.DataPreview);
			dataPreviewMenuItem.Enabled = false;
			parentMenuItem.MenuItems.Add(0, dataPreviewMenuItem);

			if (data != null)
			{
				this.menuHelper.SetImage(dataPreviewMenuItem, IconFactory.GetTrayContextMenuBitmap(data.IconType));
			}
		}

		private void AddClipboardHistoryItemsToConextMenu(IEnumerable<ClipboardData> clipboardDataItems)
		{
			foreach (var clipboardData in clipboardDataItems)
			{
				var menuItem = BuildClipboardHistoryMenuItem(clipboardData);
				this.contextMenu.MenuItems.Add(0, menuItem);
				this.menuHelper.SetImage(menuItem, IconFactory.GetTrayContextMenuBitmap(clipboardData.IconType));
				this.menuItemsByClipboardDataId.Add(clipboardData.Id, menuItem);
			}

			this.menuHelper.Refresh();
		}

		private void RemoveClipboardHistoryMenuItem(ulong clipboardDataId)
		{
			var menuItem = this.menuItemsByClipboardDataId[clipboardDataId];
			menuItem.Click -= OnClipboardHistoryMenuItemClick;
			this.contextMenu.MenuItems.Remove(menuItem);
			this.menuItemsByClipboardDataId.Remove(clipboardDataId);
			this.menuHelper.RemoveMenuItem(menuItem);
			menuItem.Dispose();
		}

		private static MenuItem BuildClipboardHistoryMenuItem(ClipboardData clipboardData)
		{
			var menuItem = new ClipboardHistoryMenuItem(string.Format("{0}\t{1}", clipboardData.DataPreview, clipboardData.TimeStamp.ToString("M/d h:mm:ss tt")), clipboardData.Id);
			menuItem.Click += OnClipboardHistoryMenuItemClick;
			return menuItem;
		}

		private static void OnClipboardHistoryMenuItemClick(object sender, EventArgs args)
		{
			try
			{
				ulong clipboardDataId = ((ClipboardHistoryMenuItem)sender).ClipboardDataId;
				AppController.ClipboardManager.PlaceHistoricalEntryOnClipboard(ClipboardDefinition.SystemClipboardId, clipboardDataId);
			}
			catch (Exception exception)
			{
				const string errorMessage = "An unexpected error occured while placing data on the system clipboard.";
				log.Error(errorMessage, exception);
				MessageBus.Instance.Publish(new TrayNotification
				{
					MessageBody = errorMessage,
					IconType = IconType.Error
				});
			}
		}

		private void NotificationRecieved(TrayNotification notification)
		{
			if (!AppController.Settings.ShowMessagesFromTray)
			{
				return;
			}

			if (this.trayPopupBorder == null || this.trayPopupIcon == null || this.trayPopupTextBlock == null)
			{
				// Even though this is an exceptional case, it cannot throw.
				// This method is called from the highest-level catch block in background threads which absolutely cannot fail.
				log.Error("Unable to show notification popup because the required UI elements could not be found.  If an error caused this notification it should be logged as well.");
				return;
			}

			Brush brush;

			if (notification.BorderBrush == null)
			{
				switch (notification.IconType)
				{
					case IconType.Error:
						brush = Brushes.Red;
						break;

					case IconType.Warning:
						brush = Brushes.Orange;
						break;

					case IconType.Success:
						brush = Brushes.Green;
						break;

					default:
						brush = Brushes.Black;
						break;
				}
			}
			else
			{
				brush = notification.BorderBrush;
			}

			AppController.ExecuteOnUiThread(
				() =>
				{
					var bitmap = new BitmapImage();
					bitmap.BeginInit();
					bitmap.UriSource = new Uri(IconFactory.GetIconPath32(notification.IconType), UriKind.Relative);
					bitmap.DecodePixelWidth = 32;
					bitmap.EndInit();

					this.trayPopupBorder.BorderBrush = brush;
					this.trayPopupIcon.Source = bitmap;
					this.trayPopupTextBlock.Text = notification.MessageBody;
					this.trayPopup.IsOpen = true;
				});
			
			this.trayPopupTimer.Start();
		}

		private void OnTrayPopupTimerStop()
		{
			this.trayPopupTimer.Stop();
			AppController.ExecuteOnUiThread(() => this.trayPopup.IsOpen = false);
		}
	}
}
