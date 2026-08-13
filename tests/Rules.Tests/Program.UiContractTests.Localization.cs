using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private static void CharacterAvatarDisplayNamesAreLocalizedForEveryLocale()
	{
		string repoRoot = FindRepositoryRoot();
		string avatarDirectory = Path.Combine(repoRoot, "resources", "character_avatars");
		string localizationPath = Path.Combine(repoRoot, "localization", "ui.csv");
		True(Directory.Exists(avatarDirectory));
		True(File.Exists(localizationPath));

		string[] localizationLines = File.ReadAllLines(localizationPath);
		True(localizationLines.Length > 1);
		List<string> header = ParseCsvLine(localizationLines[0]);
		True(header.Count > 1);

		var rowsByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		foreach (string line in localizationLines[1..])
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			List<string> row = ParseCsvLine(line);
			if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
			{
				continue;
			}

			rowsByKey[row[0].Trim()] = row;
		}

		string[] avatarPaths = Directory.GetFiles(avatarDirectory, "*.tres");
		True(avatarPaths.Length > 0);

		foreach (string avatarPath in avatarPaths)
		{
			string avatarText = File.ReadAllText(avatarPath);
			string avatarId = ReadTresString(avatarText, "Id");
			string fallbackName = ReadTresString(avatarText, "DisplayName");
			string displayNameKey = ReadTresString(avatarText, "DisplayNameKey");
			True(!string.IsNullOrWhiteSpace(avatarId));
			True(!string.IsNullOrWhiteSpace(fallbackName));
			True(!string.IsNullOrWhiteSpace(displayNameKey));
			True(displayNameKey.StartsWith("AVATAR_", StringComparison.Ordinal));
			True(displayNameKey.EndsWith("_NAME", StringComparison.Ordinal));
			if (!rowsByKey.TryGetValue(displayNameKey, out List<string>? row))
			{
				throw new InvalidOperationException($"missing localization row for {displayNameKey}");
			}

			Equal(header.Count, row.Count);

			for (int index = 1; index < header.Count; index++)
			{
				string locale = header[index];
				string localizedName = row[index].Trim();
				True(!string.IsNullOrWhiteSpace(localizedName));
				if (locale != "zh_CN" && locale != "zh_TW")
				{
					False(ContainsHanCharacter(localizedName));
				}
			}
		}
	}

	private static void PlayerCountLocalizationDifficultyRowsExistForEveryLocale()
	{
		string localizationPath = Path.Combine(FindRepositoryRoot(), "localization", "ui.csv");
		True(File.Exists(localizationPath));

		string[] localizationLines = File.ReadAllLines(localizationPath);
		True(localizationLines.Length > 1);
		List<string> header = ParseCsvLine(localizationLines[0]);
		True(header.Count > 1);

		var rowsByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		foreach (string line in localizationLines[1..])
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			List<string> row = ParseCsvLine(line);
			if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
			{
				continue;
			}

			rowsByKey[row[0].Trim()] = row;
		}

		foreach (string key in new[]
		{
			"PLAYER_COUNT_DIFFICULTY",
			"PLAYER_COUNT_DIFFICULTY_LOW",
			"PLAYER_COUNT_DIFFICULTY_MEDIUM",
			"PLAYER_COUNT_DIFFICULTY_HIGH",
		})
			{
				True(rowsByKey.TryGetValue(key, out List<string>? row));
				row ??= new List<string>();
				Equal(header.Count, row.Count);

			for (int index = 1; index < row.Count; index++)
			{
				True(!string.IsNullOrWhiteSpace(row[index]));
			}
		}
	}

	private static void RuntimeLocalizationKeysExistForEveryReference()
	{
		string repoRoot = FindRepositoryRoot();
		var rowsByKey = ReadLocalizationRowsByKey(Path.Combine(repoRoot, "localization", "ui.csv"), out _);
		var missing = new List<string>();

		foreach (string relativePath in EnumerateRuntimeLocalizationReferenceFiles(repoRoot))
		{
			string absolutePath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
			foreach (LocalizationReference reference in FindLocalizationReferences(relativePath, File.ReadAllLines(absolutePath)))
			{
				if (!rowsByKey.ContainsKey(reference.Key))
				{
					missing.Add($"{reference.Key} ({reference.RelativePath}:{reference.LineNumber})");
				}
			}
		}

		if (missing.Count > 0)
		{
			throw new InvalidOperationException("missing runtime localization keys: " + string.Join(", ", missing));
		}
	}

	private static void RuntimeLocalizationRowsCoverEverySupportedLanguage()
	{
		string repoRoot = FindRepositoryRoot();
		string localizationPath = Path.Combine(repoRoot, "localization", "ui.csv");
		var rowsByKey = ReadLocalizationRowsByKey(localizationPath, out List<string> header);
		List<string> expectedLocales = ReadSupportedLocalePreferences(repoRoot);
		List<string> actualLocales = header.Skip(1).Select(locale => locale.Trim()).ToList();

		if (expectedLocales.Count != actualLocales.Count)
		{
			throw new InvalidOperationException($"expected {expectedLocales.Count} localization locales, got {actualLocales.Count}");
		}

		for (int index = 0; index < expectedLocales.Count; index++)
		{
			if (!string.Equals(expectedLocales[index], actualLocales[index], StringComparison.Ordinal))
			{
				throw new InvalidOperationException($"localization header locale mismatch at column {index + 1}: expected {expectedLocales[index]}, got {actualLocales[index]}");
			}
		}

		foreach (KeyValuePair<string, List<string>> entry in rowsByKey)
		{
			List<string> row = entry.Value;
			if (row.Count != header.Count)
			{
				throw new InvalidOperationException($"localization row {entry.Key} has {row.Count} columns; expected {header.Count}");
			}

			for (int index = 1; index < row.Count; index++)
			{
				if (string.IsNullOrWhiteSpace(row[index]))
				{
					throw new InvalidOperationException($"localization row {entry.Key} is missing a value for locale {header[index]}");
				}
			}
		}
	}

	private static void RuntimeScenesDoNotExposeHardcodedNaturalLanguageText()
	{
		string repoRoot = FindRepositoryRoot();
		foreach (string absolutePath in Directory.GetFiles(
			Path.Combine(repoRoot, "scenes"),
			"*.tscn",
			SearchOption.AllDirectories))
		{
			string relativePath = Path.GetRelativePath(repoRoot, absolutePath).Replace('\\', '/');
			if (IsExcludedRuntimeLocalizationPath(relativePath))
			{
				continue;
			}

			string[] lines = File.ReadAllLines(absolutePath);
			for (int index = 0; index < lines.Length; index++)
			{
				Match match = Regex.Match(lines[index], "^\\s*(?<property>(?:text|placeholder_text|tooltip_text|popup/item_\\d+/text))\\s*=\\s*\"(?<value>[^\"]*)\"$");
				if (!match.Success)
				{
					continue;
				}

				string value = match.Groups["value"].Value.Trim();
				if (IsAllowedRuntimeSceneText(value))
				{
					continue;
				}

				throw new InvalidOperationException($"hardcoded runtime scene text in {relativePath}:{index + 1} -> {value}");
			}
		}
	}

	private static void RuntimeButtonsNeverDeclareHoverTooltipText()
	{
		string repoRoot = FindRepositoryRoot();
		var buttonTypes = new HashSet<string>(StringComparer.Ordinal)
		{
			"Button",
			"CheckBox",
			"CheckButton",
			"LinkButton",
			"MenuButton",
			"OptionButton",
			"TextureButton",
		};

		foreach (string absolutePath in Directory.GetFiles(
			Path.Combine(repoRoot, "scenes"),
			"*.tscn",
			SearchOption.AllDirectories))
		{
			string relativePath = Path.GetRelativePath(repoRoot, absolutePath).Replace('\\', '/');
			string currentNodeType = string.Empty;
			string[] lines = File.ReadAllLines(absolutePath);
			for (int index = 0; index < lines.Length; index++)
			{
				Match nodeMatch = Regex.Match(lines[index], "^\\[node[^]]*\\btype=\"(?<type>[^\"]+)\"");
				if (nodeMatch.Success)
				{
					currentNodeType = nodeMatch.Groups["type"].Value;
					continue;
				}

				if (lines[index].StartsWith("[", StringComparison.Ordinal))
				{
					currentNodeType = string.Empty;
					continue;
				}

				Match tooltipMatch = Regex.Match(lines[index], "^\\s*tooltip_text\\s*=\\s*\"(?<value>[^\"]*)\"$");
				if (tooltipMatch.Success
					&& buttonTypes.Contains(currentNodeType)
					&& !string.IsNullOrEmpty(tooltipMatch.Groups["value"].Value))
				{
					throw new InvalidOperationException(
						$"button hover tooltip declared in {relativePath}:{index + 1}");
				}
			}
		}

		foreach (string absolutePath in Directory.GetFiles(
			Path.Combine(repoRoot, "scripts"),
			"*.cs",
			SearchOption.AllDirectories))
		{
			string relativePath = Path.GetRelativePath(repoRoot, absolutePath).Replace('\\', '/');
			string[] lines = File.ReadAllLines(absolutePath);
			for (int index = 0; index < lines.Length; index++)
			{
				Match assignment = Regex.Match(
					lines[index],
					"(?<receiver>[A-Za-z_][A-Za-z0-9_\\.]*)\\.TooltipText\\s*=\\s*(?<value>.+);$");
				if (!assignment.Success
					|| !assignment.Groups["receiver"].Value.Contains("button", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				string value = assignment.Groups["value"].Value.Trim();
				if (relativePath == "scripts/GameAudioManager.UiBinding.cs" && value == "string.Empty")
				{
					continue;
				}

				throw new InvalidOperationException(
					$"button tooltip assignment bypasses the global suppression policy in {relativePath}:{index + 1}");
			}
		}

		string policySource = File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameAudioManager.UiBinding.cs"));
		True(policySource.Contains("SuppressButtonTooltip(button);", StringComparison.Ordinal));
		True(policySource.Contains("button.ShortcutInTooltip = false;", StringComparison.Ordinal));
		True(policySource.Contains("button.MouseEntered +=", StringComparison.Ordinal));
	}

	private static void LocalizationPlaceholdersMatchEnglishContract()
	{
		string localizationPath = Path.Combine(FindRepositoryRoot(), "localization", "ui.csv");
		Dictionary<string, List<string>> rowsByKey = ReadLocalizationRowsByKey(localizationPath, out List<string> header);
		int englishIndex = header.IndexOf("en");
		True(englishIndex > 0);

		foreach ((string key, List<string> row) in rowsByKey)
		{
			var expected = new HashSet<string>(
				Regex.Matches(row[englishIndex], "\\{[A-Za-z0-9_]+\\}").Select(match => match.Value),
				StringComparer.Ordinal);
			for (int localeIndex = 1; localeIndex < header.Count; localeIndex++)
			{
				var actual = new HashSet<string>(
					Regex.Matches(row[localeIndex], "\\{[A-Za-z0-9_]+\\}").Select(match => match.Value),
					StringComparer.Ordinal);
				if (!expected.SetEquals(actual))
				{
					throw new InvalidOperationException(
						$"localization placeholder mismatch key={key} locale={header[localeIndex]} expected={string.Join(',', expected)} actual={string.Join(',', actual)}");
				}
			}
		}
	}

	private static void LocalizationTableHasNoBroadEnglishFallback()
	{
		string localizationPath = Path.Combine(FindRepositoryRoot(), "localization", "ui.csv");
		Dictionary<string, List<string>> rowsByKey = ReadLocalizationRowsByKey(localizationPath, out List<string> header);
		int englishIndex = header.IndexOf("en");
		True(englishIndex > 0);

		foreach ((string key, List<string> row) in rowsByKey)
		{
			if (key == "APP_NAME"
				|| key.StartsWith("LANG_", StringComparison.Ordinal)
				|| key.StartsWith("AVATAR_", StringComparison.Ordinal) && key.EndsWith("_NAME", StringComparison.Ordinal))
			{
				continue;
			}

			string english = row[englishIndex];
			if (!Regex.IsMatch(english, "[A-Za-z]{3}", RegexOptions.CultureInvariant))
			{
				continue;
			}

			var matchingLocales = new List<string>();
			for (int localeIndex = 1; localeIndex < header.Count; localeIndex++)
			{
				if (localeIndex != englishIndex && string.Equals(row[localeIndex], english, StringComparison.Ordinal))
				{
					matchingLocales.Add(header[localeIndex]);
				}
			}

			if (matchingLocales.Count >= 10)
			{
				throw new InvalidOperationException(
					$"broad English localization fallback key={key} locales={string.Join(',', matchingLocales)}");
			}
		}
	}

	private static void LocalizationScriptsMatchHighRiskLocales()
	{
		string localizationPath = Path.Combine(FindRepositoryRoot(), "localization", "ui.csv");
		Dictionary<string, List<string>> rowsByKey = ReadLocalizationRowsByKey(localizationPath, out List<string> header);
		var requiredScripts = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["ar"] = "[\\u0600-\\u06FF]",
			["bg"] = "[\\u0400-\\u04FF]",
			["el"] = "[\\u0370-\\u03FF]",
			["ja"] = "[\\u3040-\\u30FF\\u3400-\\u9FFF]",
			["ko"] = "[\\uAC00-\\uD7AF]",
			["ru"] = "[\\u0400-\\u04FF]",
			["th"] = "[\\u0E00-\\u0E7F]",
			["uk"] = "[\\u0400-\\u04FF]",
			["zh_CN"] = "[\\u3400-\\u9FFF]",
			["zh_TW"] = "[\\u3400-\\u9FFF]",
		};

		foreach ((string locale, string scriptPattern) in requiredScripts)
		{
			int localeIndex = header.IndexOf(locale);
			True(localeIndex > 0);
			foreach ((string key, List<string> row) in rowsByKey)
			{
				string english = row[header.IndexOf("en")];
				if (key == "APP_NAME"
					|| key.StartsWith("LANG_", StringComparison.Ordinal)
					|| !Regex.IsMatch(english, "[A-Za-z]{4}", RegexOptions.CultureInvariant))
				{
					continue;
				}

				if (!Regex.IsMatch(row[localeIndex], scriptPattern, RegexOptions.CultureInvariant))
				{
					throw new InvalidOperationException($"wrong localization script key={key} locale={locale}");
				}
			}
		}
	}

	private static void RuntimeCSharpDoesNotAssignHardcodedNaturalLanguageText()
	{
		string repoRoot = FindRepositoryRoot();
		foreach (string relativePath in EnumerateRuntimeLocalizationReferenceFiles(repoRoot))
		{
			if (!relativePath.EndsWith(".cs", StringComparison.Ordinal))
			{
				continue;
			}

			string[] lines = File.ReadAllLines(Path.Combine(repoRoot, relativePath));
			for (int index = 0; index < lines.Length; index++)
			{
				Match match = Regex.Match(
					lines[index],
					"\\b(?:Text|TooltipText|PlaceholderText)\\s*=\\s*\"(?<value>[^\"]*)\"");
				if (match.Success && !IsAllowedRuntimeSceneText(match.Groups["value"].Value.Trim()))
				{
					throw new InvalidOperationException(
						$"hardcoded runtime C# text in {relativePath}:{index + 1} -> {match.Groups["value"].Value}");
				}
			}
		}

		var forbiddenFallbacks = new Dictionary<string, string[]>(StringComparer.Ordinal)
		{
			["scripts/MainMenuScene.AvatarUnlockPrompt.cs"] = ["GetMainMenuLocalizationFallback"],
			["scripts/AdventureAvatarMenuScene.AvatarUnlockPrompt.cs"] = ["GetAdventureMenuLocalizationFallback"],
			["scripts/GameBoardScene.PlayerStatusTooltip.cs"] = ["GetStatusMetricFallback"],
			["scripts/GameBoardScene.QuestionRejudgeOverlay.cs"] = ["FallbackText", "return definition.Description;"],
			["scripts/MainMenuSkillCodexOverlay.cs"] = ["? definition.EffectText"],
			["scripts/NetworkManager.cs"] = ["未检测到局域网 IPv4 地址"],
			["scripts/LocalizationTypography.cs"] = ["ThemeDB.FallbackFont"],
			["scripts/AdventureUpgradeChoiceArt.cs"] = ["ThemeDB.FallbackFont"],
		};
		foreach ((string relativePath, string[] fragments) in forbiddenFallbacks)
		{
			string source = File.ReadAllText(Path.Combine(repoRoot, relativePath));
			foreach (string fragment in fragments)
			{
				False(source.Contains(fragment, StringComparison.Ordinal));
			}
		}
	}

	private static void ReleasePresetsExcludeLocalizationDevelopmentScenes()
	{
		string repoRoot = FindRepositoryRoot();
		string presets = File.ReadAllText(Path.Combine(repoRoot, "export_presets.cfg"));
		MatchCollection excludeFilters = Regex.Matches(presets, "^exclude_filter=\"(?<filter>[^\"]*)\"$", RegexOptions.Multiline);
		Equal(3, excludeFilters.Count);
		string[] excludedScenes =
		{
			"scenes/adventure_suit_upgrade_editor_scene.tscn",
			"scenes/card_gallery.tscn",
			"scenes/character_avatar_gallery_scene.tscn",
			"scenes/character_avatar_preview.tscn",
			"scenes/upgrade_choice_popup_asset_scene.tscn",
		};
		foreach (Match filterMatch in excludeFilters)
		{
			foreach (string excludedScene in excludedScenes)
			{
				True(filterMatch.Groups["filter"].Value.Split(',').Contains(excludedScene, StringComparer.Ordinal));
			}
		}

		foreach (string scenePath in Directory.GetFiles(Path.Combine(repoRoot, "scenes"), "*.tscn"))
		{
			string relativePath = Path.GetRelativePath(repoRoot, scenePath).Replace('\\', '/');
			if (excludedScenes.Contains(relativePath, StringComparer.Ordinal))
			{
				continue;
			}

			string sceneText = File.ReadAllText(scenePath);
			foreach (string excludedScene in excludedScenes)
			{
				False(sceneText.Contains($"res://{excludedScene}", StringComparison.Ordinal));
			}
		}
	}

	private static void LocalizationFontsAreProjectOwnedAndLocaleSelected()
	{
		string repoRoot = FindRepositoryRoot();
		foreach (string relativePath in new[]
		{
			"assets/fonts/noto/NotoSans-Variable.ttf",
			"assets/fonts/noto/NotoSansArabic-Variable.ttf",
			"assets/fonts/noto/NotoSansThai-Variable.ttf",
			"assets/fonts/noto/NotoSansSC-Variable.ttf",
			"assets/fonts/noto/NotoSansTC-Variable.ttf",
			"assets/fonts/noto/NotoSansJP-Variable.ttf",
			"assets/fonts/noto/NotoSansKR-Variable.ttf",
			"assets/fonts/noto/OFL.txt",
			"resources/fonts/noto_latin.tres",
			"resources/fonts/noto_arabic.tres",
			"resources/fonts/noto_thai.tres",
			"resources/fonts/noto_sc.tres",
			"resources/fonts/noto_tc.tres",
			"resources/fonts/noto_jp.tres",
			"resources/fonts/noto_kr.tres",
			"resources/fonts/nunito_black_localized.tres",
			"resources/fonts/nunito_regular_localized.tres",
		})
		{
			True(File.Exists(Path.Combine(repoRoot, relativePath)));
		}

		string typographySource = File.ReadAllText(Path.Combine(repoRoot, "scripts", "LocalizationTypography.cs"));
		foreach (string locale in new[] { "ar", "th", "zh_CN", "zh_TW", "ja", "ko" })
		{
			True(typographySource.Contains($"\"{locale}\" =>", StringComparison.Ordinal));
		}
		True(typographySource.Contains("TextDirection.Auto", StringComparison.Ordinal));
		True(typographySource.Contains("LayoutDirectionEnum.ApplicationLocale", StringComparison.Ordinal));
	}

}
