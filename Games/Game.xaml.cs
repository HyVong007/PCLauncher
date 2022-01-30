using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;


namespace PCLauncher.Games
{
	public partial class Game : Window
	{
		private Process nestopia;
		public Game()
		{
			InitializeComponent();
			Desktop.clickX += ClickX;


			void ClickX()
			{
				if (nestopia?.HasExited == false)
				{
					nestopia.CloseMainWindow();
					return;
				}
				Desktop.clickX -= ClickX;
				Close();
			}
		}


		public new async void Show()
		{
			base.Show();
			await Task.Yield();
			Focus();
		}


		private static readonly string path = $"{AppDomain.CurrentDomain.BaseDirectory}\\Games\\Nestopia";
		public async void Click_Tank(object _, RoutedEventArgs __)
		{
			if (nestopia?.HasExited == false)
			{
				nestopia.CloseMainWindow();
				if (!nestopia.WaitForExit(5000)) nestopia.Kill();
			}
			nestopia = Process.Start($"{path}\\nestopia.exe", $"{path}\\games\\tank.nes");
			await Task.Delay(1000);
			if (nestopia?.HasExited == false) nestopia.MaximizeMainWindow();
		}


		private void Window_Activated(object _, EventArgs __)
		{
			if (nestopia?.HasExited == false) nestopia.MaximizeMainWindow();
		}
	}
}
