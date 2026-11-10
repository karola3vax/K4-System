namespace K4System.Services;

using System.Globalization;
using System.Text.Json;
using CounterStrikeSharp.API.Core.Translations;
using Microsoft.Extensions.Localization;

/// <summary>
/// The shipped language files plus the server's own wording. A release replaces plugins/K4-System/lang,
/// so a server that edited those files lost its changes on every update. Lines in
/// configs/plugins/K4-System/lang/&lt;code&gt;.json win over the shipped text; updates never touch that folder.
/// An override file only needs the lines that change. Every other line keeps the shipped text.
/// </summary>
internal sealed class CustomLanguage : IStringLocalizer
{
	public const string ReadmeName = "README.txt";

	private const string Readme =
		"""
		Your own K4-System wording. Updates never change this folder.

		Put a file here named after the language, such as en.json, tr.json or pt-BR.json,
		with only the lines you want to change. For example, en.json:

		{
		  "prefix": "{gold}[MyServer]"
		}

		Every line you leave out keeps the text from plugins/K4-System/lang/en.json, so new
		lines from an update still show. Copy the line names from that file. Colors such as
		{lime} and placeholders such as {0} work the same as there.

		Changes are read when K4-System loads: restart the server, or reload the plugin.
		""";

	private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

	private static readonly JsonDocumentOptions FileOptions = new()
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	private readonly IStringLocalizer shipped;
	private readonly string shippedDirectory;

	// The shipped files by culture name ("" is the invariant culture), each read once.
	private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> shippedFiles = new();
	private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> custom;

	/// <param name="custom">The server's lines by language code, from <see cref="Load"/>.</param>
	public CustomLanguage(IStringLocalizer shipped, string shippedDirectory, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> custom)
	{
		this.shipped = shipped;
		this.shippedDirectory = shippedDirectory;
		this.custom = custom;
	}

	/// <summary>A language file in the folder, and why it can't be read (null when it is fine).</summary>
	public readonly record struct FileCheck(string Name, string? Problem);

	/// <summary>
	/// Creates the folder with its how-to file and reads every language file in it. A file that can't be
	/// read is reported and skipped, so its language keeps the shipped text.
	/// </summary>
	public static (IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Lines, IReadOnlyList<FileCheck> Files) Load(string directory)
	{
		Directory.CreateDirectory(directory);
		string readme = Path.Combine(directory, ReadmeName);
		if (!File.Exists(readme))
			File.WriteAllText(readme, Readme);

		Dictionary<string, IReadOnlyDictionary<string, string>> lines = new(StringComparer.OrdinalIgnoreCase);
		List<FileCheck> files = [];
		foreach (string path in Directory.GetFiles(directory, "*.json").Order(StringComparer.OrdinalIgnoreCase))
		{
			string name = Path.GetFileName(path);
			(IReadOnlyDictionary<string, string>? read, string? problem) = Read(path);
			files.Add(new FileCheck(name, problem));
			if (read is not null)
				lines[Path.GetFileNameWithoutExtension(path)] = read;
		}

		return (lines, files);
	}

	internal static (IReadOnlyDictionary<string, string>? Lines, string? Problem) Read(string path)
	{
		try
		{
			using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path), FileOptions);
			if (document.RootElement.ValueKind != JsonValueKind.Object)
				return (null, "it must be one { ... } block of \"line\": \"text\" pairs");

			Dictionary<string, string> lines = new(StringComparer.Ordinal);
			foreach (JsonProperty line in document.RootElement.EnumerateObject())
			{
				if (line.Value.ValueKind != JsonValueKind.String)
					return (null, $"\"{line.Name}\" must be text in quotes");
				lines[line.Name] = line.Value.GetString()!;
			}

			return (lines, null);
		}
		catch (JsonException ex)
		{
			return (null, ex.Message);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return (null, ex.Message);
		}
	}

	// CounterStrikeSharp copies a whole language file into a new dictionary on every lookup, then scans every
	// chat color name. A panel draw makes about a hundred lookups, which took up to 50 ms of a server frame, so
	// each line is looked up once per language. The files do not change while K4 runs; a reload builds a new instance.
	private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Culture, string Fallback, string Name), (string Text, bool Custom)> lines = new();

	/// <summary>The line's text before placeholders are filled in, and whether it is the server's own wording.</summary>
	private (string Text, bool Custom) Template(string name)
		=> lines.GetOrAdd((CultureInfo.CurrentUICulture.Name, CultureInfo.DefaultThreadCurrentUICulture?.Name ?? "", name),
			_ => Custom(name) is string text ? (text, true) : (ShippedText(name), false));

	/// <summary>
	/// The shipped line as CounterStrikeSharp's JsonStringLocalizer finds it (GetStringSafely), without its copy of
	/// the whole file per lookup: the reader's language and its parents, English for the invariant culture, then the
	/// server language; the key itself when no file has it. A test compares the two.
	/// </summary>
	private string ShippedText(string name)
	{
		CultureInfo culture = CultureInfo.CurrentUICulture;
		string? text = Shipped(name, culture);

		if (text is null && culture.Equals(CultureInfo.InvariantCulture))
			text = ShippedFile("en").GetValueOrDefault(name);

		if (text is null && CultureInfo.DefaultThreadCurrentUICulture is CultureInfo server && !culture.Equals(server))
			text = Shipped(name, server);

		return text?.ReplaceColorTags() ?? name;
	}

	/// <summary>The line in this culture's shipped file or a parent's, the most specific first.</summary>
	private string? Shipped(string name, CultureInfo culture)
	{
		do
		{
			if (ShippedFile(culture.Name).TryGetValue(name, out string? text))
				return text;
			culture = culture.Parent;
		}
		while (!culture.Equals(CultureInfo.InvariantCulture));

		return null;
	}

	private IReadOnlyDictionary<string, string> ShippedFile(string cultureName) => shippedFiles.GetOrAdd(cultureName, ReadShipped);

	// Read like JsonResourceManager: every value as its JSON text; a missing file has no lines.
	private IReadOnlyDictionary<string, string> ReadShipped(string cultureName)
	{
		string path = Path.Combine(shippedDirectory, cultureName + ".json");
		if (!File.Exists(path))
			return new Dictionary<string, string>();

		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path), FileOptions);
		return document.RootElement.EnumerateObject().ToDictionary(line => line.Name, line => line.Value.ToString());
	}

	public LocalizedString this[string name] => new(name, Template(name).Text);

	public LocalizedString this[string name, params object[] arguments]
	{
		get
		{
			(string text, bool own) = Template(name);
			if (!own)
				return new LocalizedString(name, string.Format(text, arguments));

			try
			{
				return new LocalizedString(name, string.Format(text, arguments));
			}
			catch (FormatException)
			{
				// A server line with a broken placeholder, such as {name} or {3} on a line with two values,
				// would throw in the middle of a command or event. The shipped text is shown instead.
				return shipped[name, arguments];
			}
		}
	}

	public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
		=> shipped.GetAllStrings(includeParentCultures).Select(line => this[line.Name]);

	/// <summary>
	/// The server's own text for the reader's language (pt-BR, then pt). When the shipped files have no
	/// translation in that language either, the shipped text falls back to the server language, and so does
	/// the server's own text.
	/// </summary>
	private string? Custom(string name)
	{
		if (custom.Count == 0)
			return null;

		CultureInfo culture = CultureInfo.CurrentUICulture;
		string? text = Lookup(name, culture);

		if (text is null && Shipped(name, culture) is null)
		{
			CultureInfo fallback = culture.Equals(CultureInfo.InvariantCulture)
				? English
				: CultureInfo.DefaultThreadCurrentUICulture ?? English;
			text = Lookup(name, fallback);
		}

		return text?.ReplaceColorTags();
	}

	private string? Lookup(string name, CultureInfo culture)
	{
		for (CultureInfo current = culture; !current.Equals(CultureInfo.InvariantCulture); current = current.Parent)
		{
			if (custom.TryGetValue(current.Name, out IReadOnlyDictionary<string, string>? lines) && lines.TryGetValue(name, out string? text))
				return text;
		}

		return null;
	}
}
