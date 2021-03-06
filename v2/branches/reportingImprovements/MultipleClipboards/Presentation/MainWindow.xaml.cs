﻿using System;
using System.Timers;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MultipleClipboards.Interop;
using MultipleClipboards.Messaging;
using MultipleClipboards.Presentation.Icons;

namespace MultipleClipboards.Presentation
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private readonly Timer notificationPopupTimer;

		public MainWindow()
		{
			InitializeComponent();
			Loaded += this.MainWindowLoaded;
		    MessageBus.Instance.Subscribe<MainWindowNotification>(message => AppController.ExecuteOnUiThread(() => NotificationRecieved(message)));
			notificationPopupTimer = new Timer(5000);
			notificationPopupTimer.Elapsed += (sender, args) => AppController.ExecuteOnUiThread(OnNotificationTimerStop);
		}

		private void MainWindowLoaded(object sender, RoutedEventArgs e)
		{
			var wndHelper = new WindowInteropHelper(this);
			int exStyle = (int)Win32API.GetWindowLong(wndHelper.Handle, Win32API.GWL_EXSTYLE);
			exStyle |= Win32API.WS_EX_TOOLWINDOW;
			Win32API.SetWindowLong(wndHelper.Handle, Win32API.GWL_EXSTYLE, (IntPtr)exStyle);
		}

		protected override void OnStateChanged(EventArgs e)
		{
			base.OnStateChanged(e);
			this.ShowInTaskbar = this.WindowState != WindowState.Minimized;
		}

		private void NotificationRecieved(MainWindowNotification mainWindowNotification)
		{
			if (mainWindowNotification.BorderBrush == null)
			{
				switch (mainWindowNotification.IconType)
				{
					case IconType.Error:
						this.NotificationPresenterBorder.BorderBrush = Brushes.Red;
						break;

					case IconType.Warning:
						this.NotificationPresenterBorder.BorderBrush = Brushes.Orange;
						break;

					case IconType.Success:
						this.NotificationPresenterBorder.BorderBrush = Brushes.Green;
						break;

					default:
						this.NotificationPresenterBorder.BorderBrush = Brushes.Black;
						break;
				}
			}
			else
			{
				this.NotificationPresenterBorder.BorderBrush = mainWindowNotification.BorderBrush;
			}

			var bitmap = new BitmapImage();
			bitmap.BeginInit();
			bitmap.UriSource = new Uri(IconFactory.GetIconPath32(mainWindowNotification.IconType), UriKind.Relative);
			bitmap.DecodePixelWidth = 32;
			bitmap.EndInit();

			NotificationPresenterIcon.Source = bitmap;
			NotificationPresenterTextBlock.Text = mainWindowNotification.MessageBody;
			NotificationPresenterBorder.Visibility = Visibility.Visible;
			notificationPopupTimer.Start();
		}

		private void OnNotificationTimerStop()
		{
			notificationPopupTimer.Stop();
			NotificationPresenterBorder.Visibility = Visibility.Collapsed;
		}
	}
}
