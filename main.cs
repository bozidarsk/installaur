using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;

public static class Program 
{
	public static string Shell(string command, bool getStdOut = false) => Shell(command, out int code, getStdOut);
	public static string Shell(string command, out int code, bool getStdOut = false) 
	{
		ProcessStartInfo info = new ProcessStartInfo();
		info.FileName = Environment.GetEnvironmentVariable("SHELL");
		info.Arguments = $"-c \"{command}\"";
		info.UseShellExecute = false;
		info.RedirectStandardOutput = getStdOut;
		info.RedirectStandardError = false;

		Process proc = new Process();
		proc.StartInfo = info;
		proc.Start();
		proc.WaitForExit();
		code = proc.ExitCode;

		return getStdOut ? proc.StandardOutput.ReadToEnd() : null;
	}

	private static void InstallAUR(params string[] packages) 
	{
		foreach (string package in packages) 
		{
			string path = $"{Config.DownloadDir}/{package}";

			Shell($"git clone 'https://aur.archlinux.org/{package}.git' '{path}'");
			if (!File.Exists($"{path}/PKGBUILD")) { Console.WriteLine($"Pacakge '{package}' is empty."); continue; }

			Shell($"cat '{path}/.SRCINFO' | grep -e '\\sdepends\\s' | sed -E 's/\\s+depends = (.+)/\\1/' > '/tmp/installaur-{package}-depends'");
			Shell($"cat '{path}/.SRCINFO' | grep -e '\\smakedepends\\s' | sed -E 's/\\s+makedepends = (.+)/\\1/' > '/tmp/installaur-{package}-makedepends'");

			List<string> deps = new List<string>();
			if (!Config.NoRuntime) { deps.AddRange(File.ReadAllText($"/tmp/installaur-{package}-depends").Split('\r', '\n').Where(x => !string.IsNullOrEmpty(x))); }
			if (!Config.NoBuildtime) { deps.AddRange(File.ReadAllText($"/tmp/installaur-{package}-makedepends").Split('\r', '\n').Where(x => !string.IsNullOrEmpty(x))); }

			foreach (string dependency in deps)
			{
				if (string.IsNullOrEmpty(dependency)) { continue; }
				int code;

				Shell($"pacman -Q '{dependency}'", out code);
				if (code == 1) 
				{
					Shell($"sudo pacman -Sy --noconfirm '{dependency}'", out code);
					if (code == 1) 
					{
						InstallAUR(dependency);
						Directory.SetCurrentDirectory(path);
					}
				}
			}

			if (Config.DependenciesOnly) { continue; }
			Directory.SetCurrentDirectory(path);

			string makepkg = $"makepkg{!Config.PackageOnly ? " -i" : ""}";
			Shell($"script -qe -c '{makepkg}' '/tmp/installaur-{package}-makepkg'");

			IEnumerable<string> keys = File.ReadAllText($"/tmp/installaur-{package}-makepkg")
				.Split('\n')
				.Where(x => x.Contains("unknown public key"))
				.Select(x => x.Remove(0, x.IndexOf("unknown public key") + "unknown public key".Length + 1).Substring(0, 16))
			;

			foreach (string key in keys) { Shell($"gpg --recv-keys {key}"); }
			if (keys.Any()) { Shell(makepkg); }
		}
	}

	private static int Main(string[] args) 
	{
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
