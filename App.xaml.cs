﻿using System.IO;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;


namespace PCLauncher
{
	public partial class App : Application
	{
		public static readonly string PATH = AppDomain.CurrentDomain.BaseDirectory;
		private async void Application_Startup(object sender, StartupEventArgs e)
		{
			if (File.Exists($"{PATH}ERROR.txt")) File.Delete($"{PATH}ERROR.txt");
			$"{PATH}CONFIG.txt".ReadConfig<App>();
			(MainWindow = new Desktop()).Show();
		}


		private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			File.WriteAllText($"{PATH}ERROR.txt", $"{DateTime.Now}:\n{e.Exception}");
			MessageBox.Show($"Log file at {PATH}ERROR.txt:\n\n{e.Exception}");
			e.Handled = true;
			Environment.Exit(1);
		}


		#region Config
		public static string YoutubeExePath, YoutubeArgument, SearchExePath, SearchArgument, TVExePath, TVArgument;
		public static string VideoPath, PhotoPath, WallpaperPath;
		public static int WallpaperDelay;
		public static bool WallpaperRandom;
		public static int NestopiaTVAspectRatio;
		public static int BluetoothInitialVolume;
		public static string AlarmTime;
		public static string AlarmPath;
		public static int AlarmVolume;
		public static string SystemSpeaker, Headphone;
		public static string AutoConnectBluetoothMAC;
		#endregion
	}
}