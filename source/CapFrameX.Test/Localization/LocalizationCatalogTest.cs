using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CapFrameX.Test.Localization
{
	[TestClass]
	public class LocalizationCatalogTest
	{
		private static readonly Regex XamlKey = new Regex(@"\{loc:Tr\s+([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);
		private static readonly Regex CxLangCall = new Regex(@"CxLang\.T\s*\(([^;]+?)\)", RegexOptions.Compiled);
		private static readonly Regex StringLiteral = new Regex(@"""([A-Za-z_][A-Za-z0-9_]*)""", RegexOptions.Compiled);
		private static readonly Regex ModeConverterParam = new Regex(@"Converter=\{StaticResource\s+ModeDescriptionConverter\},\s*ConverterParameter=([^\}]+)\}", RegexOptions.Compiled);
		private static readonly Regex CatalogPrefixParam = new Regex(@"Converter=\{StaticResource\s+CatalogPrefixConverter\},\s*ConverterParameter=([A-Za-z0-9_]+)", RegexOptions.Compiled);

		[TestMethod]
		public void CatalogsUseTheSameKeysAndEveryCallSiteExists()
		{
			var root = FindRepositoryRoot();
			var localization = Path.Combine(root, "source", "CapFrameX.Contracts", "Localization");
			var catalogs = Directory.GetFiles(localization, "*.json").ToList();
			Assert.IsTrue(catalogs.Any(path => Path.GetFileName(path).Equals("en.json", StringComparison.OrdinalIgnoreCase)),
				"en.json is missing.");
			foreach (var path in catalogs)
			{
				var catalog = JObject.Parse(File.ReadAllText(path));
				Assert.IsFalse(string.IsNullOrWhiteSpace((string)catalog["name"]),
					Path.GetFileName(path) + " is missing the native language name.");
			}

			var parsed = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
			foreach (var path in catalogs)
			{
				var text = File.ReadAllText(path);
				parsed[Path.GetFileNameWithoutExtension(path)] = JObject.Parse(text, new JsonLoadSettings
				{
					DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
					CommentHandling = CommentHandling.Ignore
				});
			}

			var english = parsed["en"];
			var englishStrings = PropertyNames(english, "strings");
			var used = KeysUsedInSource(Path.Combine(root, "source"));
			var missing = used.Except(englishStrings).OrderBy(key => key).ToList();
			Assert.AreEqual(0, missing.Count, "Keys used in XAML or C# are missing from en.json: " + string.Join(", ", missing.Take(20)));

			var unused = englishStrings.Except(used).OrderBy(key => key).ToList();
			Assert.AreEqual(0, unused.Count, "Keys in en.json are not used anywhere: " + string.Join(", ", unused.Take(20)));

			foreach (var pair in parsed)
			{
				if (pair.Key.Equals("en", StringComparison.OrdinalIgnoreCase))
					continue;
				CollectionAssert.AreEquivalent(englishStrings.ToList(), PropertyNames(pair.Value, "strings").ToList(),
					pair.Key + ".json strings do not match en.json.");
				CollectionAssert.AreEquivalent(PropertyNames(english, "overlay").ToList(), PropertyNames(pair.Value, "overlay").ToList(),
					pair.Key + ".json overlay keys do not match en.json.");

				foreach (var pattern in PhrasePatterns(pair.Value))
				{
					Assert.IsFalse(string.IsNullOrWhiteSpace(pattern), $"{pair.Key}.json has an empty phrase pattern.");
					_ = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
				}
			}
		}

		private static HashSet<string> KeysUsedInSource(string source)
		{
			var keys = new HashSet<string>(StringComparer.Ordinal);
			foreach (var path in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
			{
				if (!path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
					&& !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
					continue;
				if (path.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0
					|| path.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
					continue;
				var text = File.ReadAllText(path);
				if (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
				{
					foreach (Match match in XamlKey.Matches(text))
						keys.Add(match.Groups[1].Value);

					foreach (Match match in ModeConverterParam.Matches(text))
					{
						var param = match.Groups[1].Value;
						foreach (var part in param.Split('|'))
						{
							var trimmed = part.Trim().Trim('"', '\'');
							if (!string.IsNullOrEmpty(trimmed))
								keys.Add(trimmed);
						}
					}

					foreach (Match match in CatalogPrefixParam.Matches(text))
					{
						var prefix = match.Groups[1].Value;
						if (prefix == "SoundMode_")
						{
							keys.Add("SoundMode_None");
							keys.Add("SoundMode_Simple");
							keys.Add("SoundMode_Voice");
						}
						else if (prefix == "RelatedMetric_")
						{
							keys.Add("RelatedMetric_Average");
							keys.Add("RelatedMetric_Second");
							keys.Add("RelatedMetric_Third");
						}
					}
				}
				else if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
				{
					foreach (Match match in CxLangCall.Matches(text))
					{
						var callContent = match.Groups[1].Value;
						foreach (Match sm in StringLiteral.Matches(callContent))
						{
							keys.Add(sm.Groups[1].Value);
						}
					}

					if (text.Contains("enum ") && text.Contains("Description("))
					{
						ExtractEnumKeys(text, keys);
					}
				}
			}
			return keys;
		}

		private static void ExtractEnumKeys(string text, HashSet<string> keys)
		{
			var enumMatches = Regex.Matches(text, @"(?:public\s+)?enum\s+(\w+)\s*\{([^}]+)\}", RegexOptions.Singleline);
			foreach (Match m in enumMatches)
			{
				var enumName = m.Groups[1].Value;
				var body = m.Groups[2].Value;
				var lines = body.Split('\n');
				var hasDesc = false;
				var hasShortDesc = false;
				foreach (var rawLine in lines)
				{
					var line = rawLine.Trim();
					if (line.Contains("[Description("))
						hasDesc = true;
					if (line.Contains("[ShortDescription("))
						hasShortDesc = true;

					var fieldMatch = Regex.Match(line, @"^([A-Za-z0-9_]+)\s*(?:=\s*[^,]+)?(?:,)?$");
					if (fieldMatch.Success && fieldMatch.Groups[1].Value != "public" && fieldMatch.Groups[1].Value != "enum")
					{
						var fieldName = fieldMatch.Groups[1].Value;
						if (hasDesc)
						{
							keys.Add(enumName + "_" + fieldName);
							if (hasShortDesc)
								keys.Add(enumName + "_" + fieldName + "_Short");
						}
						hasDesc = false;
						hasShortDesc = false;
					}
				}
			}
		}

		private static HashSet<string> PropertyNames(JObject catalog, string section)
		{
			var names = new HashSet<string>(StringComparer.Ordinal);
			if (catalog[section] is JObject map)
			{
				foreach (var property in map.Properties())
					names.Add(property.Name);
			}
			return names;
		}

		private static HashSet<string> PhrasePatterns(JObject catalog)
		{
			var patterns = new HashSet<string>(StringComparer.Ordinal);
			if (catalog["phrases"] is JArray phrases)
			{
				foreach (var phrase in phrases)
					patterns.Add((string)phrase["pattern"]);
			}
			return patterns;
		}

		private static string FindRepositoryRoot()
		{
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null)
			{
				if (File.Exists(Path.Combine(dir.FullName, "CapFrameX.sln")))
					return dir.FullName;
				dir = dir.Parent;
			}
			Assert.Fail("Could not find CapFrameX.sln above the test output.");
			return null;
		}
	}
}
