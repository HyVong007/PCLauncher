using Microsoft.Win32;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;


namespace PCLauncher.Games
{
	public partial class Game : Window
	{
		private Process nestopia;
		private readonly Controller xbox;
		public Game()
		{
			InitializeComponent();
			Desktop.clickX += ClickX;

			// Có 2 tay cầm: Generic Gamepad (luôn kết nối USB) và Xbox (có thể kết nối hoặc không)
			// Khi tay cầm Xbox kết nối/ngắt kết nối thì thay đổi cài đặt và khởi động lại Nestopia

			xbox = new Controller(UserIndex.One);
			bool xboxConnected = xbox.IsConnected;
			DispatcherTimer timer = null;
			Closed += (_, __) => timer.Stop();
			timer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Background, (_, __) =>
			{
				if (xbox.IsConnected == xboxConnected) return;
				xboxConnected = xbox.IsConnected;
				if (!string.IsNullOrEmpty(currentGame)) Click_Game(currentGame);
			}, App.Current.Dispatcher);
			SystemEvents.PowerModeChanged += PowerModeChanged;
		}


		private void Window_Activated(object _, EventArgs __)
		{
			if (nestopia?.HasExited == false) nestopia.MaximizeMainWindow();
		}


		public new async void Show()
		{
			base.Show();
			await Task.Yield();
			Focus();
		}


		private void Click_Tank(object sender, RoutedEventArgs e) => Click_Game("tank.nes");


		private void Click_Tetris(object sender, RoutedEventArgs e) => Click_Game("tetris.nes");


		private async void Click_CoTuong(object sender, RoutedEventArgs e)
		{
			IsEnabled = false;
			Activated += ShowX;
			Desktop.clickX -= ClickX;
			Process.Start($"{App.PATH}Games\\Co Tuong\\Co Tuong.exe");
			await Task.Delay(3000);
			IsEnabled = true;


			void ShowX(object sender, EventArgs e)
			{
				Activated -= ShowX;
				Desktop.clickX += ClickX;
			}
		}


		private static string currentGame;
		private static readonly string path = $"{App.PATH}Games\\Nestopia";
		private async void Click_Game(string game)
		{
			IsEnabled = false;
			Desktop.clickX -= ClickX;
			CloseNestopia();

			#region Change Nestopia Setting
			try { File.Delete($"{path}\\nestopia.xml"); } catch { }
			File.Copy($"{path}\\{(xbox.IsConnected ? "TWO GAMEPAD SETTING.xml" : "ONE GAMEPAD SETTING.xml")}", $"{path}\\nestopia.xml");

			// Cài đặt game Tetris: TV Aspect Ratio = yes
			if (game == "tetris.nes")
			{
				var lines = File.ReadAllLines($"{path}\\nestopia.xml");
				lines[App.NestopiaTVAspectRatio] = "<tv-aspect-ratio>yes</tv-aspect-ratio>";
				File.WriteAllLines($"{path}\\nestopia.xml", lines);
			}
			#endregion

			Process.Start($"{path}\\nestopia.exe", $"{path}\\games\\{currentGame = game}");
			await Task.Delay(1000);
			(nestopia = Process.GetProcessesByName("nestopia")[0]).MaximizeMainWindow();
			Desktop.clickX += ClickX;
			IsEnabled = true;
		}


		private bool CloseNestopia()
		{
			if (nestopia?.HasExited == false)
			{
				nestopia.CloseMainWindow();
				if (!nestopia.WaitForExit(1000)) nestopia.Kill();
				nestopia = null;
				currentGame = "";
				return true;
			}

			return false;
		}


		private void ClickX()
		{
			if (CloseNestopia()) return;
			Desktop.clickX -= ClickX;
			SystemEvents.PowerModeChanged -= PowerModeChanged;
			Close();
		}


		private void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
		{
			if (e.Mode == PowerModes.Resume) CloseNestopia();
		}
	}
}
