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
		public Game()
		{
			InitializeComponent();
			Desktop.clickX += ClickX;

			// Có 2 tay cầm: Generic Gamepad (luôn kết nối USB) và Xbox (có thể kết nối hoặc không)
			// Khi tay cầm Xbox kết nối/ngắt kết nối thì thay đổi cài đặt và khởi động lại Nestopia
			
			var xbox = new SharpDX.XInput.Controller(SharpDX.XInput.UserIndex.One);
			ChangeNestopiaSetting();
			bool xboxConnected = xbox.IsConnected;
			DispatcherTimer timer = null;
			Closed += (_, __) => timer.Stop();
			timer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Background, (_, __) =>
			{
				if (xbox.IsConnected == xboxConnected) return;

				xboxConnected = xbox.IsConnected;
				bool runningNestopia = CloseNestopia();
				ChangeNestopiaSetting();

				// Hiện tại chỉ có game Tank nên khởi động game Tank
				if (runningNestopia) Click_Tank(null, null);
			}, App.Current.Dispatcher);


			void ChangeNestopiaSetting()
			{
				try
				{
					File.Delete($"{path}\\nestopia.xml");
					File.Copy($"{path}\\{(xbox.IsConnected ? "TWO GAMEPAD SETTING.xml" : "ONE GAMEPAD SETTING.xml")}", $"{path}\\nestopia.xml");
				}
				catch { }
			}
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


		private static readonly string path = $"{App.PATH}Games\\Nestopia";
		public async void Click_Tank(object _, RoutedEventArgs __)
		{
			Desktop.clickX -= ClickX;
			CloseNestopia();
			Process.Start($"{path}\\nestopia.exe", $"{path}\\games\\tank.nes");
			await Task.Delay(1000);
			(nestopia = Process.GetProcessesByName("nestopia")[0]).MaximizeMainWindow();
			Desktop.clickX += ClickX;
		}


		private bool CloseNestopia()
		{
			if (nestopia?.HasExited == false)
			{
				nestopia.CloseMainWindow();
				if (!nestopia.WaitForExit(1000)) nestopia.Kill();
				nestopia = null;
				return true;
			}

			return false;
		}


		private void ClickX()
		{
			if (CloseNestopia()) return;
			Desktop.clickX -= ClickX;
			Close();
		}
	}
}
