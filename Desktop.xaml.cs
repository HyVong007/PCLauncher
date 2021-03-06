using Gma.System.MouseKeyHook;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;


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
			var bannedKeys = new List<Key>
			{
#if !DEBUG
				Key.LMenu,
				Key.RMenu,
				Key.F4,
#endif
				Key.F1,
				Key.F2,
				Key.F3,
				Key.F5,
				Key.F6,
				Key.F7,
				Key.F8,
				Key.F9,
				Key.F10,
				Key.F11,
				Key.F12,
				Key.SelectMedia,
				Key.LaunchMail,
				Key.VolumeMute,
				Key.BrowserSearch,
				Key.BrowserHome,
				Key.LControlKey,
				Key.RControlKey,
				Key.LWin,
				Key.RWin
};
			using var soundPlayer = new SoundPlayer($"{App.PATH}Resources/error.wav");
			soundPlayer.LoadAsync();
			var task = Task.CompletedTask;
			Keyboard.allowKey = key =>
			{
				if (administrator || !bannedKeys.Contains(key)) return true;
				if (task.IsCompleted) task = Task.Run(soundPlayer.Play);
				return false;
			};
			Keyboard.StartListening();
			#endregion

			Util.taskBarState = Util.AppBarState.AutoHide;
			Task.Delay(300).ContinueWith(_ => Util.isTaskBarVisible = false);
			new Wallpaper(this);
			SystemEvents.PowerModeChanged += PowerModeChanged;
			calendar.MouseEnter += (_, __) => CalendarGotoNow();
			calendar.MouseLeave += (_, __) => CalendarGotoNow();
			calendar.MouseWheel += (_, e) => Keyboard.Press(e.Delta > 0 ? Key.Up : Key.Down);
		}


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

			dateTimePanel.Visibility = Visibility.Visible;
			CalendarGotoNow();
			clockTimer.Start();
		}


		private void Window_Deactivated(object sender, EventArgs e)
		{
			AutoHideIcon(false);
			cancelMaximizing.Cancel();
			cancelMaximizing.Dispose();
			clockTimer.Stop();
			dateTimePanel.Visibility = Visibility.Collapsed;
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
#if DEBUG
			Util.taskBarState = Util.AppBarState.AlwaysOnTop;
			Util.isTaskBarVisible = true;
#endif
			SystemEvents.PowerModeChanged -= PowerModeChanged;
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


		private void Click_Video_Icon(object sender, RoutedEventArgs e) => OpenFolder(App.VideoPath);


		private void Click_Photo_Icon(object sender, RoutedEventArgs e) => OpenFolder(App.PhotoPath);


		private async void OpenFolder(string path)
		{
			IsEnabled = false;
			string name = Path.GetFileName(path);
			bool found = false;
			foreach (var p in Process.GetProcessesByName("explorer"))
				if (p.MainWindowTitle == name)
					if (found) p.CloseMainWindow();
					else
					{
						found = true;
						p.MaximizeMainWindow();
					}

			if (!found)
			{
				Process.Start("explorer", path);
				await Task.Delay(DELAY_TO_MAXIMIZE);
				foreach (var p in Process.GetProcessesByName("explorer"))
					if (p.MainWindowTitle == name)
					{
						p.MaximizeMainWindow();
						break;
					}
			}

			IsEnabled = true;
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
			ShowIcon(false);
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
			ShowIcon(active);

			if (active)
			{
				mouseKeyHook.KeyDown += OnMouseKeyEvents;
				mouseKeyHook.MouseMove += OnMouseKeyEvents;
				mouseKeyHook.MouseDown += OnMouseKeyEvents;
			}
			else
			{
				mouseKeyHook.KeyDown -= OnMouseKeyEvents;
				mouseKeyHook.MouseMove -= OnMouseKeyEvents;
				mouseKeyHook.MouseDown -= OnMouseKeyEvents;
			}


			static void OnMouseKeyEvents(object? sender, EventArgs arg)
			{
				iconTime = 0;
				ShowIcon(true);
				if (!iconTimer.IsEnabled) iconTimer.IsEnabled = true;
			}
		}


		private static void ShowIcon(bool show)
		{
			if (show)
			{
				instance.iconGrid.Visibility = Visibility.Visible;
				instance.dateTimePanel.HorizontalAlignment = HorizontalAlignment.Right;
				instance.dateTimePanel.VerticalAlignment = VerticalAlignment.Top;
			}
			else
			{
				instance.iconGrid.Visibility = Visibility.Collapsed;
				instance.dateTimePanel.HorizontalAlignment = HorizontalAlignment.Center;
				instance.dateTimePanel.VerticalAlignment = VerticalAlignment.Center;
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


		private sealed class Wallpaper
		{
			private readonly Desktop desktop;
			public Wallpaper(Desktop desktop)
			{
				this.desktop = desktop;
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
			private CancellationTokenSource cts;
			private static readonly List<string> IMAGE_EXTENSIONS = new()
			{
				".JPG",
				".PNG",
				".GIF"
			};
			private List<Uri> uris = new();


			private async Task Show(CancellationToken token)
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


		#region Resume từ sleep
		private static void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
		{
			if (e.Mode != PowerModes.Resume) return;
			if (Process.GetProcessesByName("msedge").Length != 0 || Process.GetProcessesByName("chrome").Length != 0)
				Keyboard.Press(Key.MediaPlayPause);
			if (instance.IsActive) instance.CalendarGotoNow();
		}
		#endregion


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
			instance.clock.Text = $"{time.Hour:00} : {time.Minute:00} : {time.Second:00}";
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
			calendar.DisplayDate = cacheDate = DateTime.Now;
		}
		#endregion
	}
}