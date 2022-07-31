using Gma.System.MouseKeyHook;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;


namespace PCLauncher
{
	public partial class AlarmNotification : Window
	{
		private readonly IKeyboardMouseEvents mouseKeyHook;
		private readonly CancellationTokenSource cts = new();
		private const int DURATION_SECONDS = 60;
		public AlarmNotification(IKeyboardMouseEvents mouseKeyHook)
		{
			InitializeComponent();
			this.mouseKeyHook = mouseKeyHook;
			mouseKeyHook.KeyDown += MouseKeyEvent;
			mouseKeyHook.MouseDown += MouseKeyEvent;
			mouseKeyHook.MouseMove += MouseKeyEvent;
			DelayClosing();

			#region Phát video
			for (int i = 0; i < 100; ++i) Keyboard.Press(Key.VolumeDown);
			player.Source = new(App.AlarmPath, UriKind.Absolute);
			player.MediaEnded += (_, __) => player.Position = new TimeSpan(0, 0, 0);
			#endregion

			#region Đếm ngược
			int seconds = DURATION_SECONDS;
			var timer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, (_, __) =>
			{
				time.Text = $"{seconds--}";
			}, App.Current.Dispatcher)
			{ IsEnabled = false };
			timer.Start();
			#endregion

			IncreaseVolume();
			async void IncreaseVolume()
			{
				var token = cts.Token;
				for (int i = 0; i < App.AlarmVolume; i += 2)
				{
					Keyboard.Press(Key.VolumeUp);
					await Task.Delay(500);
					if (token.IsCancellationRequested) return;
				}
			}
		}


		private void Window_Closed(object sender, EventArgs e)
		{
			mouseKeyHook.KeyDown -= MouseKeyEvent;
			mouseKeyHook.MouseDown -= MouseKeyEvent;
			mouseKeyHook.MouseMove -= MouseKeyEvent;
			cts.Dispose();
		}


		private void MouseKeyEvent(object? sender, EventArgs e)
		{
			if (e is not KeyEventArgs k ||
				((k.KeyCode & Keys.VolumeUp) != Keys.VolumeUp && (k.KeyCode & Keys.VolumeDown) != Keys.VolumeDown)) cts.Cancel();
		}


		private async void DelayClosing()
		{
			try { await Task.Delay(DURATION_SECONDS * 1000, cts.Token); } catch (OperationCanceledException) { Close(); return; }
			Close();
			Util.SleepPC();
		}
	}
}
