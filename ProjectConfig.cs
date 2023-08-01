public static partial class Config 
{
	public static bool NoBuildtime { private set; get; }
	public static bool NoRuntime { private set; get; }
	public static bool DependenciesOnly { private set; get; }
	public static bool PackageOnly { private set; get; }
	public static string DownloadDir { private set; get; } = "/tmp";

	private static readonly Option[] OptionsDefinition = 
	{
		new Option("--no-buildtime", 'b', false, null),
		new Option("--no-runtime", 'r', false, null),
		new Option("--dependencies-only", 'd', false, null),
		new Option("--package-only", 'p', false, null),
		new Option("--download-dir", 'D', true, null),
	};
}