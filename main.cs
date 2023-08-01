using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;

public static class Program 
{
	private static void zsh(string command) => zsh(command, out int code);
	private static void zsh(string command, out int code) 
	{
		ProcessStartInfo info = new ProcessStartInfo();
		info.FileName = "/bin/zsh";
		info.Arguments = "-c \"" + command + "\"";
		info.UseShellExecute = false;

		Process proc = new Process();
		proc.StartInfo = info;
		proc.Start();
		proc.WaitForExit();
		code = proc.ExitCode;
	}

	private static void InstallAUR(params string[] packages) 
	{
		foreach (string package in packages) 
		{
			zsh($"git clone 'https://aur.archlinux.org/{package}.git' '{Config.DownloadDir}/{package}'");
			Directory.SetCurrentDirectory($"{Config.DownloadDir}/{package}");

			string dependencies = (!Config.NoBuildtime ? "$makedepends " : "") + (!Config.NoRuntime ? "$depends" : "");
			zsh($"source '{Config.DownloadDir}/{package}/PKGBUILD'; echo {dependencies} | tr -d '\\n' > '/tmp/installaur-{package}'");

			foreach (string dependency in File.ReadAllText($"/tmp/installaur-{package}").Split(' ')) 
			{
				if (string.IsNullOrEmpty(dependency)) { continue; }
				int code;

				zsh($"pacman -Q '{dependency}'", out code);
				if (code == 1) 
				{
					zsh($"sudo pacman -Sy --noconfirm '{dependency}'", out code);
					if (code == 1) { InstallAUR(dependency); }
				}
			}

			if (Config.DependenciesOnly) { continue; }

			string makepkg = $"makepkg{!Config.PackageOnly ? " -i" : ""}";
			zsh($"script -qe -c '{makepkg}' '/tmp/installaur-{package}'");

			string[] keys = 
				File.ReadAllText($"/tmp/installaur-{package}")
				.Split('\n')
				.Where(x => x.Contains("unknown public key"))
				.Select(x => x.Remove(0, x.IndexOf("unknown public key") + "unknown public key".Length + 1).Substring(0, 16))
				.ToArray()
			;

			if (keys.Length > 0) 
			{
				string allkeys = "";
				for (int i = 0; i < keys.Length; i++) { allkeys += keys[i] + " "; }
				zsh($"gpg --recv-keys {allkeys}");
				zsh(makepkg);
			}

			if (!Config.PackageOnly) { zsh($"rm -rf '{Config.DownloadDir}/{package}'"); }
			zsh($"rm '/tmp/installaur-{package}'");
		}
	}

	private static int Main(string[] args) 
	{
		if (!File.Exists("/bin/zsh")) { Console.WriteLine("zsh is required."); return 1; }

		if (args.Length == 0 || (args.Length == 1 && (args[0] == "-h" || args[0] == "--help" || args[0] == "help"))) 
		{
			Console.WriteLine("Usage:\n\tinstallaur <package0> [package1] [package...] [options]");
			Console.WriteLine("\nOptions:");
			Console.WriteLine("\t-b, --no-buildtime        Does not check for missing buildtime dependencies.");
			Console.WriteLine("\t-r, --no-runtime          Does not check for missing runtime dependencies.");
			Console.WriteLine("\t-d, --dependencies-only   Does not install the package, only its dependencies.");
			Console.WriteLine("\t-p, --package-only        Only makes the package and does not install it.");
			Console.WriteLine("\t-D, --download-dir <dir>  Where does it download (git clone) the aur repository.");
			Console.WriteLine("\t                          After installing it removes these files. (if --package-only is not specified)");
			return 0;
		}

		Config.Initialize(ref args);
		if (args.Length == 0) { Console.WriteLine("No packages provided."); return 1; }

		InstallAUR(args);

		return 0;
	}
}