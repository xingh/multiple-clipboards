﻿using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MultipleClipboards.Entities;
using MultipleClipboards.Interop;
using MultipleClipboards.Persistence;

namespace MultipleClipboards.Presentation
{
	/// <summary>
	/// Interaction logic for ClipboardWindow.xaml
	/// </summary>
	public partial class ClipboardWindow : Window, IDisposable
	{
		private readonly object _clipboardManagerLock = new object();
		private readonly object _clipboardInUseFlagLock = new object();

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
				// TODO: Add error handling here.
				LogManager.Error("Unable to aquire the handle to the clipboard message window.  This means we cannot intercept the message loop and perform clipboard actions.");
			}

			InitializeComponent();
			this.LastMessageProcessed = null;
			LogManager.Debug("Clipboard message window initialized!");
		}

		public void Dispose()
		{
			Win32API.ChangeClipboardChain(this.Handle, this.NextClipboardViewerHandle);
			LogManager.Debug("Clipboard Manager has been disposed and the clipboard message window is closing.");
		}

		private bool HasProcessedFirstMessage
		{
			get;
			set;
		}

		private bool IsProcessingClipboardOperation
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

		private HotKeyMessage CurrentMessage
		{
			get;
			set;
		}

		private HotKeyMessage LastMessageProcessed
		{
			get;
			set;
		}

		private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			handled = true;

			switch (msg)
			{
				case Win32API.WM_HOTKEY:
					lock (this._clipboardInUseFlagLock)
					{
						this.IsProcessingClipboardOperation = true;
					}

					// Figure out what key was pressed and send it along to the clipboard manager.
					this.CurrentMessage = new HotKeyMessage
					{
						Hwnd = hwnd,
						Msg = msg,
						WParam = wParam,
						LParam = lParam
					};
					LogManager.DebugFormat("New HotKey message recieved in message loop:\r\n{0}", this.CurrentMessage);
					HotKey hotKey = HotKey.FromHotKeyMessage(this.CurrentMessage);

					// Make sure this thread is the only one using the clipboard manager right now.
					lock (this._clipboardManagerLock)
					{
						if (this.CurrentMessage != this.LastMessageProcessed)
						{
							// Wait while there are any modifier keys held down.
							// This causes unpredictable results when the user has setup a combination of different hotkeys.
							while (ModifierKeysPressed())
							{
								Thread.Sleep(10);
							}

							try
							{
								AppController.ClipboardManager.ProcessHotKey(hotKey);
								this.LastMessageProcessed = this.CurrentMessage;
							}
							catch (Exception e)
							{
								LogManager.ErrorFormat("Error processing the hotkey: {0}", e, hotKey);
							}
						}
					}

					lock (this._clipboardInUseFlagLock)
					{
						this.IsProcessingClipboardOperation = false;
					}
					break;

				case Win32API.WM_DRAWCLIPBOARD:
					bool isProcessingClipboardAction;
					lock (this._clipboardInUseFlagLock)
					{
						isProcessingClipboardAction = this.IsProcessingClipboardOperation;
					}

					if (!isProcessingClipboardAction && this.HasProcessedFirstMessage)
					{
						// The data on the clipboard has changed.
						// This means the user used the regular windows clipboard.
						// Track the data on the clipboard for the history viewer.
						// Data coppied using any additional clipboards will be tracked internally.
						try
						{
							LogManager.Debug("System clipboard has changed.  About to store the contents of the clipboard.");
							lock (this._clipboardManagerLock)
							{
								AppController.ClipboardManager.StoreClipboardContents();
							}
						}
						catch (Exception e)
						{
							LogManager.Error("Error storing clipboard contents", e);
						}
					}
					else
					{
						LogManager.Debug("System clipboard has changed, but we are currently processing another clipboard action.  Skipping message.");
					}

					// Send the message to the next app in the clipboard chain.
					Win32API.SendMessage(this.NextClipboardViewerHandle, msg, wParam, lParam);
					this.HasProcessedFirstMessage = true;
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
					break;

				default:
					handled = false;
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
