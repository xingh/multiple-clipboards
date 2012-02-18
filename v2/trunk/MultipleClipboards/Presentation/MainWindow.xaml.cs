﻿using System;
using System.Timers;
using System.Windows;
using System.Windows.Input;
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
			MessageBus.Instance.Subscribe<Notification>(NotificationRecieved);
			notificationPopupTimer = new Timer(5000);
			notificationPopupTimer.Elapsed += (sender, args) => Application.Current.Dispatcher.Invoke(new Action(OnNotificationTimerStop));
		}

		private void MainWindowLoaded(object sender, RoutedEventArgs e)
		{
			var wndHelper = new WindowInteropHelper(this);
			int exStyle = (int)Win32API.GetWindowLong(wndHelper.Handle, Win32API.GWL_EXSTYLE);
			exStyle |= Win32API.WS_EX_TOOLWINDOW;
			Win32API.SetWindowLong(wndHelper.Handle, Win32API.GWL_EXSTYLE, (IntPtr)exStyle);
		}

		private void CloseButtonClick(object sender, RoutedEventArgs e)
		{
			this.Close();
			// TODO: Figure out why setting the window to minimized breaks the hover close button once the window is restored.
			//WindowState = WindowState.Minimized;
		}

		//protected override void OnStateChanged(EventArgs e)
		//{
		//    base.OnStateChanged(e);
		//    this.ShowInTaskbar = this.WindowState != WindowState.Minimized;
		//}

		private void DragStart(object sender, MouseButtonEventArgs e)
		{
			DragMove();
			e.Handled = true;
		}

		private void NotificationRecieved(Notification notification)
		{
			if (notification.BorderBrush == null)
			{
				switch (notification.IconType)
				{
					case IconType.Error:
						this.NotificationPresenterBorder.BorderBrush = Brushes.Red;
						break;

					case IconType.Warning:
						this.NotificationPresenterBorder.BorderBrush = Brushes.Yellow;
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
				this.NotificationPresenterBorder.BorderBrush = notification.BorderBrush;
			}

			var bitmap = new BitmapImage();
			bitmap.BeginInit();
			bitmap.UriSource = new Uri(IconFactory.GetIcon32(notification.IconType), UriKind.Relative);
			bitmap.DecodePixelWidth = 32;
			bitmap.EndInit();

			NotificationPresenterIcon.Source = bitmap;
			NotificationPresenterTextBlock.Text = notification.MessageBody;
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
