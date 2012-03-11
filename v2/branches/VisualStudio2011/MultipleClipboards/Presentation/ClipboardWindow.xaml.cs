﻿using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MultipleClipboards.ClipboardManagement;
using MultipleClipboards.Entities;
using MultipleClipboards.Interop;
using log4net;

namespace MultipleClipboards.Presentation
{
	/// <summary>
	/// Interaction logic for ClipboardWindow.xaml
	/// </summary>
	public partial class ClipboardWindow : Window, IDisposable
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(ClipboardWindow));

		public ClipboardWindow()
		{
			// Must set the window style properties before we aquire the handle to the window.
			this.WindowStyle = WindowStyle.None;
			this.Width = 0;
			this.Height = 0;
			this.ShowInTaskbar = false;
			this.ShowActivated = false;

			// Get a handle to this main window and hook into the message loop.
			WindowInteropHelper interopHelper = new WindowInteropHelper(this);
			this.Handle = interopHelper.EnsureHandle();
			HwndSource hwndSource = HwndSource.FromHwnd(this.Handle);

			if (hwndSource != null)
			{
				AppController.InitializeClipboardManager(this.Handle);
				hwndSource.AddHook(this.WndProc);
				this.NextClipboardViewerHandle = Win32API.SetClipboardViewer(this.Handle);
			}
			else
			{
				// TODO: Once the ability to publish messages to tray popups is in place publish a message here.
				//		 We can't use the regular pub/sub here because the main window has not been constructed yet.
				log.Error("Unable to aquire the handle to the clipboard message window.  This means we cannot intercept the message loop and perform clipboard actions.");
			}

			InitializeComponent();
			log.DebugFormat("Clipboard message window initialized!  Handle: {0}", this.Handle);
		}

		public void Dispose()
		{
			Win32API.ChangeClipboardChain(this.Handle, this.NextClipboardViewerHandle);
			log.Debug("Clipboard Manager has been disposed and the clipboard message window is closing.");
		}

		private bool HasProcessedFirstMessage
		{
			get;
			set;
		}

		private bool IsClipboardManagerInUse
		{
			get;
			set;
		}

		private IntPtr Handle
		{
			get;
			set;
		}

		private IntPtr NextClipboardViewerHandle
		{
			get;
			set;
		}

		private WindowsMessage CurrentMessage
		{
			get;
			set;
		}

		private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			// Here's the deal.  Weird shit happens in a message loop.
			// My own logs have proved this and some former native C++ developers that I know confirmed this.
			// The OS can re-enter this function on the same thread that you are currently doing work on.
			// I have not taken the time to figure out how this works, but it does.
			// Therefore, any kind of traditional locking mechanisms do nothing because we're on the same thread.
			// Once I figured this out it actually made things much easier.  Just set and check flags to make
			// sure you don't process the same message more than once.
			//
			// However, I only care about 3 message types, so bail on this whole thing if it's not a message I care about.
			handled = false;

			if (msg != Win32API.WM_HOTKEY && msg != Win32API.WM_DRAWCLIPBOARD && msg != Win32API.WM_CHANGECBCHAIN)
			{
				return IntPtr.Zero;
			}

			var currentMessage = new WindowsMessage
			{
				Hwnd = hwnd,
				Msg = msg,
				WParam = wParam,
				LParam = lParam
			};

			if (this.CurrentMessage == currentMessage)
			{
				log.DebugFormat("WndProc(): New {0} message recieved, but it is the same as the last messaage of this type that was processed.  Skipping.", currentMessage.MessageType);
				return IntPtr.Zero;
			}
			
			this.CurrentMessage = currentMessage;
			log.DebugFormat("WndProc(): New message recieved:\r\n{0}", currentMessage);

			switch (msg)
			{
				case Win32API.WM_HOTKEY:
					if (this.IsClipboardManagerInUse)
					{
						log.Warn("WndProc(): Unable to process hotkey message because the clipboard manager is in use by another message. Skipping.");
					}
					else
					{
						// Set the flag to indicate that the clipboard manager is in use.
						this.IsClipboardManagerInUse = true;

						// Figure out what key was pressed and send it along to the clipboard manager.
						HotKey hotKey = HotKey.FromWindowsMessage(currentMessage);

						// Wait while there are any modifier keys held down.
						// This causes unpredictable results when the user has setup a combination of different hotkeys.
						while (ModifierKeysPressed())
						{
							Thread.Sleep(10);
						}

						var arguments = new ProcessHotKeyArguments
						{
							HotKey = hotKey,
							Callback = () => this.IsClipboardManagerInUse = false
						};

						// Process the hot key in a seperate thread.
						// We are about to perform clipboard operations.  That involves opening the system clipboard and sending system
						// keystokes (Ctrl + C, etc).  Since the system clipboard is just one big race condition between all running applications,
						// there is a lot of waiting involved.  These delays vary tremendously depending on the application that currently has focus.
						// See comments in ClipboardManager for more info.  All that matters here is that we don't block the message loop thread.
						//
						// It concerns me that hot key operations use the one and only clipboard manager in a seperate thread while everything else
						// continues to use it on the main UI thread.  However, the only errors I have ever encountered have been while processing
						// hot keys, so hopefully this is OK.
						var clipboardThread = new Thread(AppController.ClipboardManager.ProcessHotKey);
						clipboardThread.SetApartmentState(ApartmentState.STA);
						clipboardThread.IsBackground = true;
						clipboardThread.Start(arguments);
					}

					handled = true;
					break;

				case Win32API.WM_DRAWCLIPBOARD:
					if (this.HasProcessedFirstMessage)
					{
						if (this.IsClipboardManagerInUse)
						{
							log.Debug("WndProc(): System clipboard has changed, but we are currently processing another clipboard message.  Skipping.");
						}
						else
						{
							// Set the flag to indicate that the clipboard manager is in use.
							this.IsClipboardManagerInUse = true;

							// The data on the clipboard has changed.
							// This means the user used the regular windows clipboard.
							// Track the data on the clipboard for the history viewer.
							// Data coppied using any additional clipboards will be tracked internally.
							try
							{
								log.Debug("WndProc(): System clipboard has changed.  About to store the contents of the clipboard.");
								AppController.ClipboardManager.StoreClipboardContents();
								log.Debug("WndProc(): Stored clipboard contents successfully.");
							}
							catch (Exception e)
							{
								log.Error("WndProc(): Error storing clipboard contents", e);
							}

							this.IsClipboardManagerInUse = false;
						}
					}

					// Send the message to the next app in the clipboard chain.
					Win32API.SendMessage(this.NextClipboardViewerHandle, msg, wParam, lParam);
					this.HasProcessedFirstMessage = true;
					handled = true;
					break;

				case Win32API.WM_CHANGECBCHAIN:
					if (wParam == this.NextClipboardViewerHandle)
					{
						this.NextClipboardViewerHandle = lParam;
					}
					else
					{
						Win32API.SendMessage(this.NextClipboardViewerHandle, msg, wParam, lParam);
					}
					handled = true;
					break;
			}

			return IntPtr.Zero;
		}

		/// <summary>
		/// Check if a certain key is held down.
		/// </summary>
		/// <param name="key">The key to check.</param>
		/// <returns>True if the key is pressed, false if it is not.</returns>
		private static bool IsKeyPressed(Key key)
		{
			return (Win32API.GetAsyncKeyState(KeyInterop.VirtualKeyFromKey(key)) & 0x8000) == 0x8000;
		}

		/// <summary>
		/// Checks if any of the possible modifier keys are held down.
		/// </summary>
		/// <returns>True if any of the modifier keys are pressed, false if not.</returns>
		private static bool ModifierKeysPressed()
		{
			return (IsKeyPressed(Key.LeftShift) ||
					IsKeyPressed(Key.RightShift) ||
					IsKeyPressed(Key.LeftCtrl) ||
					IsKeyPressed(Key.RightCtrl) ||
					IsKeyPressed(Key.LWin) ||
					IsKeyPressed(Key.RWin) ||
					IsKeyPressed(Key.LeftAlt) ||
					IsKeyPressed(Key.RightAlt));
		}
	}
}