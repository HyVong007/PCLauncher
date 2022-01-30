using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
			using var soundPlayer = new SoundPlayer($"{App.PATH}/Resources/error.wav");
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
			new SlideShow(this);
		}


		private CancellationTokenSource cancelMaximizing;
		private async void Window_Activated(object sender, EventArgs e)
		{
			AutoHideIcon(true);
			administrator = false;
			Util.isTaskBarVisible = false;

			#region Maximize các ứng dụng đang chạy (nếu có)
			var token = (cancelMaximizing = new CancellationTokenSource()).Token;
			await Task.Run(async () =>
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
		}


		private void Window_Deactivated(object sender, EventArgs e)
		{
			AutoHideIcon(false);
			cancelMaximizing.Cancel();
			cancelMaximizing.Dispose();
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
		}


		#region Icons
		private const int DELAY_TO_MAXIMIZE = 4000;

		private readonly ProcessStartInfo youtubeInfo = new ProcessStartInfo
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


		private readonly ProcessStartInfo searchInfo = new ProcessStartInfo
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


		private readonly ProcessStartInfo tvInfo = new ProcessStartInfo
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


		private sealed class SlideShow
		{
			private readonly Desktop desktop;
			public SlideShow(Desktop desktop)
			{
				this.desktop = desktop;
				uris.Clear();
				foreach (string path in Directory.GetFiles(App.SlideshowPath))
					if (IMAGE_EXTENSIONS.Contains(Path.GetExtension(path).ToUpper())) uris.Add(new Uri(path));
				uris.Sort((a, b) => a.LocalPath.CompareTo(b.LocalPath));

				desktop.Activated += (s, e) =>
				{
					if (uris.Count != 0)
					{
						cts = new CancellationTokenSource();
						Show();
					}
				};

				desktop.Deactivated += (s, e) =>
				{
					cts?.Cancel();
					cts?.Dispose();
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
			private readonly List<Uri> uris = new();


			private async void Show()
			{
				var token = cts.Token;
				int index = 0;
				if (App.SlideshowRandom)
				{
					var rand = new Random(DateTime.Now.Millisecond);
					while (true)
					{
						UpdateWallpaper();
						try { await Task.Delay(App.SlideshowDelay, token); }
						catch (OperationCanceledException) { return; }
						index = rand.Next(uris.Count);
					}
				}

				#region Tuần tự hình nền
				while (true)
				{
					do
					{
						UpdateWallpaper();
						try { await Task.Delay(App.SlideshowDelay, token); }
						catch (OperationCanceledException) { return; }
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
						cts.Cancel();
						cts.Dispose();
						uris.Clear();
						foreach (string path in Directory.GetFiles(App.SlideshowPath))
							if (IMAGE_EXTENSIONS.Contains(Path.GetExtension(path).ToUpper())) uris.Add(new Uri(path));
						uris.Sort((a, b) => a.LocalPath.CompareTo(b.LocalPath));

						cts = new CancellationTokenSource();
						Show();
						return;
					}

					image.Freeze();
					desktop.wallPaper.Source = image;
				}
			}
			#endregion
		}
	}
}