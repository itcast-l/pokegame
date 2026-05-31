using System.Collections.Generic;
using Godot;

public static class Localization
{
	public const string DefaultLanguagePreference = "auto";
	private const string CsvPath = "res://localization/ui.csv";
	private const string DefaultLocale = "en";
	private static bool _loaded;
	private static readonly Dictionary<string, Translation> _runtimeTranslations = new();

	public static readonly (string Preference, string LabelKey, string SteamApiCode, string SteamWebApiCode)[] SupportedLanguages =
	{
		(DefaultLanguagePreference, "LANG_AUTO", "", ""),
		("ar", "LANG_AR", "arabic", "ar"),
		("bg", "LANG_BG", "bulgarian", "bg"),
		("zh_CN", "LANG_ZH_CN", "schinese", "zh-CN"),
		("zh_TW", "LANG_ZH_TW", "tchinese", "zh-TW"),
		("cs", "LANG_CS", "czech", "cs"),
		("da", "LANG_DA", "danish", "da"),
		("nl", "LANG_NL", "dutch", "nl"),
		("en", "LANG_EN", "english", "en"),
		("fi", "LANG_FI", "finnish", "fi"),
		("fr", "LANG_FR", "french", "fr"),
		("de", "LANG_DE", "german", "de"),
		("el", "LANG_EL", "greek", "el"),
		("hu", "LANG_HU", "hungarian", "hu"),
		("id", "LANG_ID", "indonesian", "id"),
		("it", "LANG_IT", "italian", "it"),
		("ja", "LANG_JA", "japanese", "ja"),
		("ko", "LANG_KO", "koreana", "ko"),
		("no", "LANG_NO", "norwegian", "no"),
		("pl", "LANG_PL", "polish", "pl"),
		("pt", "LANG_PT", "portuguese", "pt"),
		("pt_BR", "LANG_PT_BR", "brazilian", "pt-BR"),
		("ro", "LANG_RO", "romanian", "ro"),
		("ru", "LANG_RU", "russian", "ru"),
		("es", "LANG_ES", "spanish", "es"),
		("es_419", "LANG_ES_419", "latam", "es-419"),
		("sv", "LANG_SV", "swedish", "sv"),
		("th", "LANG_TH", "thai", "th"),
		("tr", "LANG_TR", "turkish", "tr"),
		("uk", "LANG_UK", "ukrainian", "uk"),
		("vi", "LANG_VI", "vietnamese", "vi"),
	};

	public static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}

		_loaded = true;
		bool loadedImportedTranslations = LoadImportedTranslations();
		LoadCsvTranslations(warnIfMissing: !loadedImportedTranslations);
	}

	private static void LoadCsvTranslations(bool warnIfMissing)
	{
		using Godot.FileAccess? file = Godot.FileAccess.Open(CsvPath, Godot.FileAccess.ModeFlags.Read);
		if (file == null)
		{
			if (warnIfMissing)
			{
				GD.PushWarning($"Missing localization table: {CsvPath}");
			}

			return;
		}

		string[] header = file.GetCsvLine();
		if (header.Length < 2 || header[0] != "key")
		{
			GD.PushWarning($"Invalid localization table header: {CsvPath}");
			return;
		}

		var localeColumns = new Dictionary<string, int>();
		for (int index = 1; index < header.Length; index++)
		{
			string locale = NormalizeLocale(header[index]);
			if (string.IsNullOrWhiteSpace(locale))
			{
				continue;
			}

			localeColumns[locale] = index;
		}

		foreach ((string preference, _, _, _) in SupportedLanguages)
		{
			if (preference == DefaultLanguagePreference)
			{
				continue;
			}

			_runtimeTranslations[preference] = GetOrCreateRuntimeTranslation(preference);
		}

		while (!file.EofReached())
		{
			string[] row = file.GetCsvLine();
			if (row.Length == 0 || string.IsNullOrWhiteSpace(row[0]))
			{
				continue;
			}

			string key = row[0];
			string defaultText = GetRowText(row, localeColumns, DefaultLocale);
			if (string.IsNullOrEmpty(defaultText))
			{
				defaultText = GetFirstTranslatedText(row);
			}

			foreach ((string preference, _, _, _) in SupportedLanguages)
			{
				if (preference == DefaultLanguagePreference || !_runtimeTranslations.TryGetValue(preference, out Translation? translation))
				{
					continue;
				}

				string translatedText = GetRowText(row, localeColumns, preference);
				if (string.IsNullOrEmpty(translatedText))
				{
					translatedText = defaultText;
				}

				if (string.IsNullOrEmpty(translatedText))
				{
					continue;
				}

				translation.AddMessage(key, translatedText, string.Empty);
			}
		}
	}

	private static string GetRowText(string[] row, Dictionary<string, int> localeColumns, string locale)
	{
		return localeColumns.TryGetValue(locale, out int index) && index < row.Length ? row[index] : string.Empty;
	}

	private static string GetFirstTranslatedText(string[] row)
	{
		for (int index = 1; index < row.Length; index++)
		{
			if (!string.IsNullOrEmpty(row[index]))
			{
				return row[index];
			}
		}

		return string.Empty;
	}

	private static Translation GetOrCreateRuntimeTranslation(string locale)
	{
		if (_runtimeTranslations.TryGetValue(locale, out Translation? translation))
		{
			return translation;
		}

		translation = new Translation
		{
			Locale = locale,
		};
		TranslationServer.AddTranslation(translation);
		return translation;
	}

	private static bool LoadImportedTranslations()
	{
		bool loadedAny = false;
		foreach ((string preference, _, _, _) in SupportedLanguages)
		{
			if (preference == DefaultLanguagePreference)
			{
				continue;
			}

			if (TranslationServer.HasTranslationForLocale(preference, exact: true))
			{
				loadedAny = true;
				continue;
			}

			string translationPath = $"res://localization/ui.{preference}.translation";
			if (!ResourceLoader.Exists(translationPath))
			{
				continue;
			}

			Translation? translation = GD.Load<Translation>(translationPath);
			if (translation == null)
			{
				continue;
			}

			TranslationServer.AddTranslation(translation);
			loadedAny = true;
		}

		return loadedAny;
	}

	public static string ResolveLocale(string preference)
	{
		return ResolveLocale(preference, string.Empty);
	}

	public static string ResolveLocale(Node context, string preference)
	{
		return ResolveLocale(preference, GetSteamAutomaticLocale(context));
	}

	public static string ResolveLocale(string preference, string automaticLocale)
	{
		preference = NormalizePreference(preference);
		if (preference != DefaultLanguagePreference)
		{
			return preference;
		}

		string normalizedAutomaticLocale = NormalizeLocale(automaticLocale);
		if (!string.IsNullOrEmpty(normalizedAutomaticLocale))
		{
			return normalizedAutomaticLocale;
		}

		return ResolveSystemLocale();
	}

	public static string NormalizeLocale(string localeOrSteamLanguage)
	{
		if (string.IsNullOrWhiteSpace(localeOrSteamLanguage))
		{
			return string.Empty;
		}

		string normalized = localeOrSteamLanguage.Trim().Replace("-", "_");
		if (normalized.Equals("zh_Hans", System.StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("zh_CN", System.StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("schinese", System.StringComparison.OrdinalIgnoreCase))
		{
			return "zh_CN";
		}

		if (normalized.Equals("zh_Hant", System.StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("zh_TW", System.StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("tchinese", System.StringComparison.OrdinalIgnoreCase))
		{
			return "zh_TW";
		}

		if (normalized.Equals("pt_BR", System.StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("brazilian", System.StringComparison.OrdinalIgnoreCase))
		{
			return "pt_BR";
		}

		if (normalized.Equals("es_419", System.StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("latam", System.StringComparison.OrdinalIgnoreCase))
		{
			return "es_419";
		}

		if (normalized.Equals("nb", System.StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("nn", System.StringComparison.OrdinalIgnoreCase))
		{
			return "no";
		}

		for (int index = 0; index < SupportedLanguages.Length; index++)
		{
			(string preference, _, string steamApiCode, string steamWebApiCode) = SupportedLanguages[index];
			if (preference == DefaultLanguagePreference)
			{
				continue;
			}

			if (normalized.Equals(preference, System.StringComparison.OrdinalIgnoreCase)
				|| normalized.Equals(steamApiCode, System.StringComparison.OrdinalIgnoreCase)
				|| normalized.Equals(steamWebApiCode.Replace("-", "_"), System.StringComparison.OrdinalIgnoreCase))
			{
				return preference;
			}
		}

		return string.Empty;
	}

	private static string GetSteamAutomaticLocale(Node context)
	{
		Node? steamManager = context.GetNodeOrNull<Node>("/root/SteamManager");
		if (steamManager == null || !GodotObject.IsInstanceValid(steamManager) || !steamManager.HasMethod("get_preferred_godot_locale"))
		{
			return string.Empty;
		}

		return NormalizeLocale(steamManager.Call("get_preferred_godot_locale").AsString());
	}

	private static string ResolveSystemLocale()
	{
		string normalizedLocale = OS.GetLocale().Replace("-", "_");
		if (normalizedLocale.StartsWith("zh_TW", System.StringComparison.OrdinalIgnoreCase)
			|| normalizedLocale.StartsWith("zh_HK", System.StringComparison.OrdinalIgnoreCase)
			|| normalizedLocale.StartsWith("zh_MO", System.StringComparison.OrdinalIgnoreCase))
		{
			return "zh_TW";
		}

		if (normalizedLocale.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase))
		{
			return "zh_CN";
		}

		if (normalizedLocale.StartsWith("pt_BR", System.StringComparison.OrdinalIgnoreCase))
		{
			return "pt_BR";
		}

		if (normalizedLocale.StartsWith("es_419", System.StringComparison.OrdinalIgnoreCase))
		{
			return "es_419";
		}

		string osLanguage = OS.GetLocaleLanguage();
		if (osLanguage is "nb" or "nn")
		{
			return "no";
		}

		string normalizedLanguage = NormalizeLocale(osLanguage);
		if (!string.IsNullOrEmpty(normalizedLanguage))
		{
			return normalizedLanguage;
		}

		return DefaultLocale;
	}

	public static int GetPreferenceIndex(string preference)
	{
		for (int index = 0; index < SupportedLanguages.Length; index++)
		{
			if (SupportedLanguages[index].Preference == preference)
			{
				return index;
			}
		}

		return 0;
	}

	public static string NormalizePreference(string preference)
	{
		return SupportedLanguages[GetPreferenceIndex(preference)].Preference;
	}
}
