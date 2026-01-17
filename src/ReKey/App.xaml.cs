using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace ReKey;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		if (!IsAdministrator())
		{
			try
			{
				var exePath = Environment.ProcessPath;
				if (!string.IsNullOrWhiteSpace(exePath))
				{
					var startInfo = new ProcessStartInfo(exePath)
					{
						UseShellExecute = true,
						Verb = "runas",
						Arguments = string.Join(" ", e.Args)
					};
					Process.Start(startInfo);
				}
			}
			catch
			{
				MessageBox.Show("관리자 권한이 필요합니다.", "ReKey", MessageBoxButton.OK, MessageBoxImage.Warning);
			}

			Shutdown();
			return;
		}

		base.OnStartup(e);
	}

	private static bool IsAdministrator()
	{
		var identity = WindowsIdentity.GetCurrent();
		var principal = new WindowsPrincipal(identity);
		return principal.IsInRole(WindowsBuiltInRole.Administrator);
	}
}
