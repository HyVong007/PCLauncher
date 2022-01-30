﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Threading;


namespace PCLauncher
{
	public partial class App : Application
	{
		public static readonly string PATH = AppDomain.CurrentDomain.BaseDirectory;
		private void Application_Startup(object sender, StartupEventArgs e)
		{
			$"{PATH}\\CONFIG.txt".ReadConfig<App>();
			(MainWindow = new Desktop()).Show();
		}


		private void Application_Exit(object sender, ExitEventArgs e)
		{

		}


		private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			Application_Exit(null, null);
			File.WriteAllText($"{PATH}\\ERROR.txt", $"{DateTime.Now}:\n{e.Exception}");
			MessageBox.Show($"Log file at {PATH}\\ERROR.txt:\n\n{e.Exception}");
			e.Handled = true;
			Environment.Exit(1);
		}


		#region Config
		public static string YoutubeExePath, YoutubeArgument, SearchExePath, SearchArgument, TVExePath, TVArgument;
		public static string VideoPath, PhotoPath, SlideshowPath;
		public static int SlideshowDelay;
		public static bool SlideshowRandom;
		#endregion
	}
}