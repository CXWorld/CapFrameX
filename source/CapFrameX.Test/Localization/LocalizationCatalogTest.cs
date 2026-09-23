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
		private static readonly Regex CodeKey = new Regex(@"CxLang\.T\(\s*""([A-Za-z_][A-Za-z0-9_]*)""\s*\)", RegexOptions.Compiled);

		[TestMethod]
		public void CatalogsUseTheSameKeysAndEveryCallSiteExists()
		{
			var root = FindRepositoryRoot();
			var localization = Path.Combine(root, "source", "CapFrameX.Contracts", "Localization");
			var catalogs = Directory.GetFiles(localization, "*.json")
				.Where(path => !string.Equals(Path.GetFileName(path), "Russian.json", StringComparison.OrdinalIgnoreCase))
				.ToList();
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
					DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
				});
			}

			var english = parsed["en"];
			var englishStrings = PropertyNames(english, "strings");
			var used = KeysUsedInSource(Path.Combine(root, "source"));
			var missing = used.Except(englishStrings).OrderBy(key => key).ToList();
			Assert.AreEqual(0, missing.Count, "Keys used in XAML or C# are missing from en.json: " + string.Join(", ", missing.Take(20)));

			foreach (var pair in parsed)
			{
				if (pair.Key.Equals("en", StringComparison.OrdinalIgnoreCase))
					continue;
				CollectionAssert.AreEquivalent(englishStrings.ToList(), PropertyNames(pair.Value, "strings").ToList(),
					pair.Key + ".json strings do not match en.json.");
				CollectionAssert.AreEquivalent(PropertyNames(english, "overlay").ToList(), PropertyNames(pair.Value, "overlay").ToList(),
					pair.Key + ".json overlay keys do not match en.json.");
				CollectionAssert.AreEquivalent(PhrasePatterns(english).ToList(), PhrasePatterns(pair.Value).ToList(),
					pair.Key + ".json phrase patterns do not match en.json.");
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
				foreach (Match match in XamlKey.Matches(text))
					keys.Add(match.Groups[1].Value);
				foreach (Match match in CodeKey.Matches(text))
					keys.Add(match.Groups[1].Value);
			}
			return keys;
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
