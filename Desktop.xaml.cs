using Gma.System.MouseKeyHook;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;


namespace PCLauncher
{
	public partial class Desktop : Window
	{
		private static Desktop instance;
		private static readonly IKeyboardMouseEvents mouseKeyHook = Hook.GlobalEvents();
		/// <summary>
		/// Chế độ Administrator (nhấn vô icon Windows để kích hoạt)
		/// </summary>
		private static bool administrator;
		private readonly ulong bluetoothMAC;


		public Desktop()
		{
			instance = this;
			InitializeComponent();

			#region Gắn hình ảnh cho Icon
			iconYoutube.GetChild<Image>().Source = new BitmapImage(new Uri("pack://application:,,,/Resources/youtube.png"));
			iconSearch.GetChild<Image>().Source = new BitmapImage(new Uri("pack://application:,,,/Resources/search.png"));
			iconTV.GetChild<Image>().Source = new BitmapImage(new Uri("pack://application:,,,/Resources/TV.png"));
			iconVideo.GetChild<Image>().Source = new BitmapImage(new Uri("pack://application:,,,/Resources/video.png"));
			iconPhoto.GetChild<Image>().Source = new BitmapImage(new Uri("pack://application:,,,/Resources/photo.png"));
			iconGame.GetChild<Image>().Source = new BitmapImage(new Uri("pack://application:,,,/Resources/game.png"));
			iconWindows.GetChild<Image>().Source = new BitmapImage(new Uri("pack://application:,,,/Resources/windows.ico"));
			#endregion

			#region Giới hạn bàn phím: cấm những phím nâng cao
			var soundPlayer = new SoundPlayer($"{App.PATH}Resources/error.wav");
			Closed += (_, __) => soundPlayer.Dispose();
			soundPlayer.LoadAsync();
			var task = Task.CompletedTask;
			Keyboard.allowKey = key =>
			{
				if (administrator || key_allow[key]) return true;
				if (task.IsCompleted) task = Task.Run(soundPlayer.Play);
				return false;
			};
			Keyboard.StartListening();
			#endregion

			#region Chuyển đổi loa
			App.SystemSpeaker = string.IsNullOrEmpty(App.SystemSpeaker) ? "" : App.SystemSpeaker.Trim();
			App.Headphone = string.IsNullOrEmpty(App.Headphone) ? "" : App.Headphone.Trim();
			if (App.SystemSpeaker.Length == 0 || App.Headphone.Length == 0) switchSpeaker.Visibility = Visibility.Hidden;
			else
			{
				currentSpeaker = App.Headphone;
				Click_SwitchSpeaker(null, null); // Chuyển audio về loa chính (System)
			}
			#endregion

			Util.taskBarState = Util.AppBarState.AutoHide;
			Task.Delay(300).ContinueWith(_ => Util.isTaskBarVisible = false);
			Wallpaper.Init(this);
			SystemEvents.PowerModeChanged += PowerModeChanged;
			calendar.MouseEnter += (_, __) => CalendarGotoNow();
			calendar.MouseLeave += (_, __) => CalendarGotoNow();
			calendar.MouseWheel += (_, e) => Keyboard.Press(e.Delta > 0 ? Key.Up : Key.Down);
			Alarm.Init(mouseKeyHook);
			try { bluetoothMAC = Util.ConvertMACAddress(App.AutoConnectBluetoothMAC); } catch { }
			if (bluetoothMAC > 0) AutoConnectBluetooth();
		}


		private void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
		{
			if (e.Mode == PowerModes.Suspend) return;
			if (Process.GetProcessesByName("msedge").Length != 0 || Process.GetProcessesByName("chrome").Length != 0)
				Keyboard.Press(Key.MediaPlayPause);
			if (IsActive) instance.CalendarGotoNow();
			if (switchSpeaker.Visibility == Visibility.Visible)
			{
				currentSpeaker = App.Headphone;
				Click_SwitchSpeaker(null, null); // Chuyển audio về loa chính (System)
			}
			if (bluetoothMAC > 0) AutoConnectBluetooth();
		}


		#region Cho phép/Cấm phím
		private static readonly Dictionary<Key, bool> key_allow = new()
		{
#if !DEBUG
			{ Key.LMenu, false },
			{ Key.RMenu,false },
			{ Key.F4,false },
#endif
			{ Key.F1,false },
			{ Key.F2,false },
			{ Key.F3,false },
			{ Key.F5,false },
			{ Key.F6,false },
			{ Key.F7,false },
			{ Key.F8,false },
			{ Key.F9,false },
			{ Key.F10,false },
			{ Key.F11,false },
			{ Key.F12,false },
			{ Key.SelectMedia,false },
			{ Key.LaunchMail,false },
			{ Key.VolumeMute,false },
			{ Key.BrowserSearch,false },
			{ Key.BrowserHome,false },
			{ Key.LControlKey,false },
			{ Key.RControlKey,false },
			{ Key.LWin,false },
			{ Key.RWin,false }
		};


		static Desktop()
		{
			foreach (Key key in Enum.GetValues(typeof(Key)))
				if (!key_allow.ContainsKey(key)) key_allow[key] = true;
		}
		#endregion


		private CancellationTokenSource cancelMaximizing;
		private void Window_Activated(object sender, EventArgs e)
		{
			AutoHideIcon(true);
			administrator = false;
			Util.isTaskBarVisible = false;

			#region Maximize các ứng dụng đang chạy (nếu có)
			var token = (cancelMaximizing = new CancellationTokenSource()).Token;
			Task.Run(async () =>
		   {
			   await Task.Delay(DELAY_TO_MAXIMIZE);
			   if (token.IsCancellationRequested) return;

			   foreach (var p in Process.GetProcessesByName("msedge")) p.MaximizeMainWindow();
			   foreach (var p in Process.GetProcessesByName("chrome")) p.MaximizeMainWindow();
			   foreach (var p in Process.GetProcessesByName("mpc-be64")) p.MaximizeMainWindow();
			   string videoName = Path.GetFileName(App.VideoPath);
			   string photoName = Path.GetFileName(App.PhotoPath);
			   foreach (var p in Process.GetProcessesByName("explorer"))
				   if (p.MainWindowTitle == videoName || p.MainWindowTitle == photoName) p.MaximizeMainWindow();
		   });
			#endregion

			calendar.Visibility = clock.Visibility = Visibility.Visible;
			CalendarGotoNow();
			clockTimer.Start();
		}


		private void Window_Deactivated(object sender, EventArgs e)
		{
			AutoHideIcon(false);
			cancelMaximizing.Cancel();
			cancelMaximizing.Dispose();
			clockTimer.Stop();
			calendar.Visibility = clock.Visibility = Visibility.Collapsed;
		}


		private void Window_Closing(object sender, CancelEventArgs e) => e.Cancel =
#if DEBUG
				false
#else
				true
#endif
				;


		private void Window_Closed(object sender, EventArgs e)
		{
			mouseKeyHook.Dispose();
			Keyboard.StopListening();
			SystemEvents.PowerModeChanged -= PowerModeChanged;
#if DEBUG
			Util.taskBarState = Util.AppBarState.AlwaysOnTop;
			Util.isTaskBarVisible = true;
#endif
		}


		#region Icons
		private const int DELAY_TO_MAXIMIZE = 4000;
		private readonly ProcessStartInfo youtubeInfo = new()
		{
			FileName = App.YoutubeExePath,
			Arguments = App.YoutubeArgument
		};
		private async void Click_Youtube_Icon(object sender, RoutedEventArgs e)
		{
			IsEnabled = false;
			Process.Start(youtubeInfo);
			await Task.Delay(DELAY_TO_MAXIMIZE);

			foreach (var p in Process.GetProcessesByName("msedge")) p.MaximizeMainWindow();
			foreach (var p in Process.GetProcessesByName("chrome")) p.MaximizeMainWindow();
			clickX += CloseWebBrowsers;
			IsEnabled = true;
		}


		private readonly ProcessStartInfo searchInfo = new()
		{
			FileName = App.SearchExePath,
			Arguments = App.SearchArgument
		};
		private async void Click_Search_Icon(object sender, RoutedEventArgs e)
		{
			IsEnabled = false;
			Process.Start(searchInfo);
			await Task.Delay(DELAY_TO_MAXIMIZE);

			foreach (var p in Process.GetProcessesByName("msedge")) p.MaximizeMainWindow();
			foreach (var p in Process.GetProcessesByName("chrome")) p.MaximizeMainWindow();
			clickX += CloseWebBrowsers;
			IsEnabled = true;
		}


		private readonly ProcessStartInfo tvInfo = new()
		{
			FileName = App.TVExePath,
			Arguments = App.TVArgument
		};
		private async void Click_TV_Icon(object sender, RoutedEventArgs e)
		{
			IsEnabled = false;
			Process.Start(tvInfo);
			await Task.Delay(DELAY_TO_MAXIMIZE);

			foreach (var p in Process.GetProcessesByName("msedge")) p.MaximizeMainWindow();
			foreach (var p in Process.GetProcessesByName("chrome")) p.MaximizeMainWindow();
			clickX += CloseWebBrowsers;
			IsEnabled = true;
		}


		private static void CloseWebBrowsers()
		{
			foreach (var p in Process.GetProcessesByName("msedge")) p.CloseMainWindow();
			foreach (var p in Process.GetProcessesByName("chrome")) p.CloseMainWindow();
			clickX -= CloseWebBrowsers;
		}


		private async void Click_Video_Icon(object sender, RoutedEventArgs e)
		{
			iconVideo.IsEnabled = false;
			var videoExplorer = await OpenFolder(App.VideoPath);
			clickX += ClickX;
			iconVideo.IsEnabled = true;


			void ClickX()
			{
				var mpc = Process.GetProcessesByName("mpc-be64");
				if (mpc.Length != 0)
				{
					foreach (var p in mpc) p.CloseMainWindow();
					return;
				}

				if (videoExplorer?.HasExited == false) videoExplorer.CloseMainWindow();
				clickX -= ClickX;
			}
		}


		private void Click_Photo_Icon(object sender, RoutedEventArgs e) => OpenFolder(App.PhotoPath);


		private async Task<Process> OpenFolder(string path)
		{
			IsEnabled = false;
			string name = Path.GetFileName(path);
			Process result = null;
			foreach (var p in Process.GetProcessesByName("explorer"))
				if (p.MainWindowTitle == name)
					if (result != null) p.CloseMainWindow();
					else (result = p).MaximizeMainWindow();

			if (result == null)
			{
				Process.Start("explorer", path);
				await Task.Delay(DELAY_TO_MAXIMIZE);
				foreach (var p in Process.GetProcessesByName("explorer"))
					if (p.MainWindowTitle == name)
					{
						(result = p).MaximizeMainWindow();
						break;
					}
			}

			IsEnabled = true;
			return result;
		}


		private void Click_Game_Icon(object sender, RoutedEventArgs e)
		{
			IsEnabled = false;
			new Games.Game().Show();
			IsEnabled = true;
		}


		private void Click_Windows_Icon(object sender, RoutedEventArgs e)
		{
			IsEnabled = false;
			administrator = true;
			Util.isTaskBarVisible = true;
			Keyboard.Press(Key.LWin);
			IsEnabled = true;
		}


		#region Auto hide Icon
		private static int iconTime;
		private static readonly DispatcherTimer iconTimer = new(new TimeSpan(0, 0, 1), DispatcherPriority.Background, (_, __) =>
		{
			if (++iconTime < 5) return;
			instance.iconGrid.Visibility = Visibility.Collapsed;
			iconTimer.Stop();
		}, App.Current.Dispatcher)
		{
			// Phải tắt do bug của System.Windows.Threading.DispatcherTimer: https://github.com/dotnet/wpf/issues/2135
			IsEnabled = false
		};


		private static void AutoHideIcon(bool active)
		{
			if (iconTimer.IsEnabled == active) return;
			iconTimer.IsEnabled = active;
			iconTime = 0;

			if (active)
			{
				instance.iconGrid.Visibility = Visibility.Visible;
				mouseKeyHook.KeyDown += OnMouseKeyEvents;
				mouseKeyHook.MouseMove += OnMouseKeyEvents;
				mouseKeyHook.MouseDown += OnMouseKeyEvents;
			}
			else
			{
				instance.iconGrid.Visibility = Visibility.Collapsed;
				mouseKeyHook.KeyDown -= OnMouseKeyEvents;
				mouseKeyHook.MouseMove -= OnMouseKeyEvents;
				mouseKeyHook.MouseDown -= OnMouseKeyEvents;
			}


			static void OnMouseKeyEvents(object? sender, EventArgs arg)
			{
				iconTime = 0;
				instance.iconGrid.Visibility = Visibility.Visible;
				if (!iconTimer.IsEnabled) iconTimer.IsEnabled = true;
			}
		}
		#endregion
		#endregion


		#region X Button Popup
		private static int xTime;
		private static readonly DispatcherTimer xTimer = new(new TimeSpan(0, 0, 1), DispatcherPriority.Background, (_, __) =>
		{
			if (++xTime < 5) return;
			instance.xButton.IsOpen = false;
			xTimer.Stop();
		}, App.Current.Dispatcher)
		{
			// Phải tắt do bug của System.Windows.Threading.DispatcherTimer: https://github.com/dotnet/wpf/issues/2135
			IsEnabled = false
		};


		public void Click_X(object _, RoutedEventArgs __) => ΔclickX();
		private static Action ΔclickX;
		/// <summary>
		/// Được gọi nếu người dùng click vào nút popup X<br/>
		/// Đăng ký handler sẽ kích hoạt popup X, hủy hết handler sẽ hủy popup X
		/// </summary>
		public static event Action clickX
		{
			add
			{
				bool empty = ΔclickX == null;
				ΔclickX += value;
				if (!empty) return;

				instance.xButton.IsOpen = xTimer.IsEnabled = true;
				xTime = 0;
				mouseKeyHook.KeyDown += OnMouseKeyEvents;
				mouseKeyHook.MouseMove += OnMouseKeyEvents;
				mouseKeyHook.MouseDown += OnMouseKeyEvents;
			}

			remove
			{
				ΔclickX -= value;
				if (ΔclickX != null) return;

				instance.xButton.IsOpen = xTimer.IsEnabled = false;
				xTime = 0;
				mouseKeyHook.KeyDown -= OnMouseKeyEvents;
				mouseKeyHook.MouseMove -= OnMouseKeyEvents;
				mouseKeyHook.MouseDown -= OnMouseKeyEvents;
			}
		}


		private static void OnMouseKeyEvents(object? sender, EventArgs arg)
		{
			xTime = 0;
			if (!instance.xButton.IsOpen) instance.xButton.IsOpen = true;
			if (!xTimer.IsEnabled) xTimer.IsEnabled = true;
		}
		#endregion


		private static class Wallpaper
		{
			private static Desktop desktop;
			public static void Init(Desktop desktop)
			{
				Wallpaper.desktop = desktop;
				uris.Clear();
				foreach (string path in Directory.GetFiles(App.WallpaperPath))
					if (IMAGE_EXTENSIONS.Contains(Path.GetExtension(path).ToUpper())) uris.Add(new Uri(path));
				uris.Sort((a, b) => a.LocalPath.CompareTo(b.LocalPath));

				desktop.Activated += (s, e) =>
				{
					lock (desktop)
					{
						if (uris.Count != 0)
						{
							var token = (cts = new CancellationTokenSource()).Token;
							Task.Run(async () => { try { await Show(token); } catch (OperationCanceledException) { } });
						}
					}
				};

				desktop.Deactivated += (s, e) =>
				{
					cts?.Cancel();
					cts?.Dispose();
					cts = null;
				};
			}


			#region Show
			private static CancellationTokenSource cts;
			private static readonly List<string> IMAGE_EXTENSIONS = new()
			{
				".JPG",
				".PNG",
				".GIF"
			};
			private static List<Uri> uris = new();


			private static async Task Show(CancellationToken token)
			{
				int index = 0;
				if (App.WallpaperRandom)
				{
					var rand = new Random(DateTime.Now.Millisecond);
					while (true)
					{
						UpdateWallpaper();
						await Task.Delay(App.WallpaperDelay, token);
						index = rand.Next(uris.Count);
					}
				}

				#region Tuần tự hình nền
				while (true)
				{
					do
					{
						UpdateWallpaper();
						await Task.Delay(App.WallpaperDelay, token);
					} while (++index < uris.Count);
					index = 0;
				}
				#endregion


				void UpdateWallpaper()
				{
					var image = new BitmapImage();

					try
					{
						image.BeginInit();
						image.CacheOption = BitmapCacheOption.None;
						image.UriSource = uris[index];
						image.EndInit();
					}
					catch
					{
						var tmp = new List<Uri>();
						foreach (string path in Directory.GetFiles(App.WallpaperPath))
							if (IMAGE_EXTENSIONS.Contains(Path.GetExtension(path).ToUpper())) tmp.Add(new Uri(path));
						tmp.Sort((a, b) => a.LocalPath.CompareTo(b.LocalPath));

						lock (desktop) uris = tmp;
						App.Current.Dispatcher.Invoke(() =>
						{
							if (!desktop.IsActive) return;
							cts?.Dispose();
							token = (cts = new CancellationTokenSource()).Token;
							Task.Run(async () => { try { await Show(token); } catch (OperationCanceledException) { } });
						});
						throw new OperationCanceledException();
					}

					image.Freeze();
					App.Current.Dispatcher.Invoke(() => { if (desktop.IsActive) desktop.wallPaper.Source = image; });
				}
			}
			#endregion
		}


		#region Clock and Calendar
		private static readonly SolidColorBrush[] rainBow =
			{ new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Orange), new SolidColorBrush(Colors.Yellow),
			new SolidColorBrush(Colors.Green), new SolidColorBrush(Colors.Blue), new SolidColorBrush(Colors.Indigo),
			new SolidColorBrush(Colors.Violet) };
		private static int rainBowIndex;
		private static readonly DispatcherTimer clockTimer = new(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, (_, __) =>
		{
			var time = DateTime.Now;
			if (time.Day != cacheDate.Day) instance.CalendarGotoNow();
			instance.clock.Text = $"{(time.Hour == 0 ? 12 : time.Hour < 13 ? time.Hour : time.Hour - 12)} : {time.Minute:00} : {time.Second:00}";
			instance.clock.Foreground = rainBow[rainBowIndex++];
			if (rainBowIndex >= rainBow.Length) rainBowIndex = 0;
		}, App.Current.Dispatcher)
		{
			IsEnabled = false
		};


		private static DateTime cacheDate;
		private void CalendarGotoNow()
		{
			calendar.DisplayMode = CalendarMode.Month;
			calendar.SelectedDate = calendar.DisplayDate = cacheDate = DateTime.Now;
		}
		#endregion


		#region Chuyển đổi âm thanh ra loa cố định và tai nghe
		private static string currentSpeaker;
		private static readonly ProcessStartInfo info = new()
		{
			FileName = $"{App.PATH}EndPointController.exe",
			UseShellExecute = false,
			CreateNoWindow = true
		};

		private async void Click_SwitchSpeaker(object _, RoutedEventArgs __)
		{
			switchSpeaker.Visibility = Visibility.Hidden;
			info.Arguments = "";
			info.RedirectStandardOutput = true;
			var p = Process.Start(info);
			string output = p.StandardOutput.ReadToEnd();
			await Task.Delay(1000);

			var lines = output.Split("\r\n");
			currentSpeaker = currentSpeaker == App.SystemSpeaker ? App.Headphone : App.SystemSpeaker;
			for (int i = lines.Length - 2; i >= 0; --i)
				if (lines[i].Split(':')[1].Trim() == currentSpeaker)
				{
					info.Arguments = $"{i + 1}";
					info.RedirectStandardOutput = false;
					Process.Start(info);
					speakerImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/" +
						$"{(currentSpeaker == App.SystemSpeaker ? "SystemSpeaker" : "Headphone")}.png"));
					await Task.Delay(1000);
					break;
				}
			switchSpeaker.Visibility = Visibility.Visible;
		}
		#endregion


		private CancellationTokenSource ctsBluetooth = new();
		private async void AutoConnectBluetooth()
		{
			ctsBluetooth.Cancel();
			ctsBluetooth.Dispose();
			var token = (ctsBluetooth = new()).Token;
			var b = await BluetoothDevice.FromBluetoothAddressAsync(bluetoothMAC);
			if (token.IsCancellationRequested || b == null) return;
			await b.DeviceInformation.Pairing.UnpairAsync();
			if (token.IsCancellationRequested) return;
			b.DeviceInformation.Pairing.Custom.PairingRequested += Custom_PairingRequested;
			await b.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly, DevicePairingProtectionLevel.None);


			void Custom_PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
			{
				b.DeviceInformation.Pairing.Custom.PairingRequested -= Custom_PairingRequested;
				args.Accept();
			}
		}
	}
}