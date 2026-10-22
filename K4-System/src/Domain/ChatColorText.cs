namespace K4System.Domain;

using System.Reflection;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Modules.Utils;

/// <summary>Turns chat color names such as "lightred" into CounterStrikeSharp color codes.</summary>
internal static class ChatColorText
{
	private static readonly Lazy<(Regex Pattern, Dictionary<string, string> Codes)> Colors = new(() =>
	{
		Dictionary<string, string> codes = new(StringComparer.OrdinalIgnoreCase);

		foreach (FieldInfo field in typeof(ChatColors).GetFields(BindingFlags.Public | BindingFlags.Static))
		{
			string? value = field.GetValue(null)?.ToString();
			if (value is not null)
				codes.TryAdd(field.Name, value);
		}

		// Longest names first, so "LightRed" wins over "Red".
		string alternatives = string.Join("|", codes.Keys.OrderByDescending(name => name.Length).Select(Regex.Escape));
		Regex pattern = new($@"\b(?:{alternatives})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		return (pattern, codes);
	});

	/// <summary>True when the name is one of CounterStrikeSharp's chat colors, such as "LightRed".</summary>
	public static bool IsKnown(string? name) => name is not null && Colors.Value.Codes.ContainsKey(name.Trim());

	public static string Apply(string text)
	{
		if (string.IsNullOrEmpty(text))
			return text;

		(Regex pattern, Dictionary<string, string> codes) = Colors.Value;
		return pattern.Replace(text, match => codes[match.Value]);
	}
}
