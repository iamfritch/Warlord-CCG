using System;
using System.IO;

namespace WarlordEngine;

/// <summary>
/// Central, machine-independent resolution of data file locations.
/// Data files are copied next to the built assemblies (see the CopyToOutputDirectory items in
/// WarlordEngine.csproj) and are therefore resolved relative to <see cref="AppContext.BaseDirectory"/>,
/// never relative to the current working directory or an absolute path.
/// </summary>
public static class DataPaths
{
	/// <summary>Sub-folder (relative to the output directory) that contains the data files.</summary>
	public const string DataFolderName = "Data";

	public const string CardsFileName = "cards.json";

	public const string StarterDecksFileName = "starter_decks.json";

	public static string CardsJsonPath => Resolve(CardsFileName);

	public static string StarterDecksJsonPath => Resolve(StarterDecksFileName);

	/// <summary>Resolves a data file relative to <see cref="AppContext.BaseDirectory"/>.</summary>
	public static string Resolve(string fileName)
	{
		string basePath = AppContext.BaseDirectory;
		return Path.Combine(basePath, DataFolderName, fileName);
	}
}
