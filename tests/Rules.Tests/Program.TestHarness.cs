using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private static CaptureScoringRules.Context DefaultScoringContext => new(
		SuppressSlimeCoatedScore: false,
		ApplyScoreReductionPerCard: false,
		ScoreReductionPerCard: 0);

	private static PlayingCard CreatePlayingCard()
	{
		return new PlayingCard("hearts", "A", new Godot.Texture2D());
	}

	private static GameBoardState CreateCaptureState(PlayingCard card)
	{
		var state = new GameBoardState();
		state.PlayerDecks.Add(new List<PlayingCard>());
		state.TableCards.Add(new TableCard(card, isFaceUp: true));
		return state;
	}

	private static bool InvokeCanAcceptSceneLoadAck(
		IReadOnlyCollection<NetworkPlayerPublicState> players,
		int senderPeerId,
		int reportedSeatIndex,
		out int senderSeatIndex)
	{
		MethodInfo? method = typeof(MultiplayerBoardSyncRules).GetMethod(
			"CanAcceptSceneLoadAck",
			BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		True(method != null);
		object?[] args = { players, senderPeerId, reportedSeatIndex, -1 };
		bool accepted = (bool)(method!.Invoke(null, args) ?? false);
		senderSeatIndex = (int)args[3]!;
		return accepted;
	}

	private static void ResetGameSaveStoreTestState()
	{
		Godot.Engine.MainLoopOverride = null;
		Godot.GD.ClearWarnings();
		Godot.FileAccess.ClearTestState();
		Godot.DirAccess.ClearTestState();
		SetStaticField(typeof(GameSaveStore), "_attemptedLegacyMigration", false);
		SetStaticField(typeof(GameSaveStore), "_attemptedProgressProfileMigration", false);
		SetStaticField(typeof(GameSaveStore), "_warnedMissingSaveManager", false);
	}

	private static Godot.TestSaveManagerNode InstallSaveManagerForTests(string activeSlotId)
	{
		var root = new Godot.Node();
		var saveManager = new Godot.TestSaveManagerNode();
		root.AddNamedChild("SaveManager", saveManager);
		Godot.Engine.MainLoopOverride = new Godot.SceneTree { Root = root };
		saveManager.SeedValue("migration.settings_cfg.v1", true);
		saveManager.SeedValue("migration.progress_profile.v1", true);
		saveManager.SeedValue(GameSaveStore.ActiveProfileSlotKey, activeSlotId);
		return saveManager;
	}

	private static void SeedProfile(
		Godot.TestSaveManagerNode saveManager,
		string slotId,
		GameSaveStore.GameProgressData progressData)
	{
		saveManager.SeedSlot(slotId, progressData.ToDictionary());
	}

	private static GameSaveStore.GameProgressData LoadStoredProfile(Godot.TestSaveManagerNode saveManager, string slotId)
	{
		return GameSaveStore.GameProgressData.FromDictionary(
			saveManager.GetSlotData(slotId),
			GameSaveStore.GameProgressData.CreateDefault());
	}

	private static GameSaveStore.GameProgressData CreateProgressData(
		string playerName,
		string pendingAdventureRunJson = "")
	{
		return GameSaveStore.GameProgressData.CreateDefault() with
		{
			PlayerName = playerName,
			PendingAdventureRunJson = pendingAdventureRunJson,
		};
	}

	private static void SetStaticField(Type type, string fieldName, object value)
	{
		FieldInfo? field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		True(field != null);
		field!.SetValue(null, value);
	}

	private static NetworkPlayerPublicState Player(int peerId, int seatIndex, bool isHost, bool isReady)
	{
		return new NetworkPlayerPublicState(
			peerId,
			$"stable-{peerId}",
			$"Player{peerId}",
			"cat",
			seatIndex,
			isHost,
			IsLocal: peerId == 1,
			isReady,
			IsConnected: true);
	}

	private static string ReadTresString(string tresText, string propertyName)
	{
		Match match = Regex.Match(tresText, $"^{Regex.Escape(propertyName)} = \"([^\"]*)\"$", RegexOptions.Multiline);
		return match.Success ? match.Groups[1].Value : string.Empty;
	}

	private static Dictionary<string, List<string>> ReadLocalizationRowsByKey(string localizationPath, out List<string> header)
	{
		True(File.Exists(localizationPath));
		string[] localizationLines = File.ReadAllLines(localizationPath);
		True(localizationLines.Length > 1);

		header = ParseCsvLine(localizationLines[0]);
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

		return rowsByKey;
	}

	private static List<string> ParseCsvLine(string line)
	{
		var values = new List<string>();
		var current = new System.Text.StringBuilder();
		bool inQuotes = false;

		for (int index = 0; index < line.Length; index++)
		{
			char value = line[index];
			if (value == '"')
			{
				if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
				{
					current.Append('"');
					index++;
				}
				else
				{
					inQuotes = !inQuotes;
				}
				continue;
			}

			if (value == ',' && !inQuotes)
			{
				values.Add(current.ToString());
				current.Clear();
				continue;
			}

			current.Append(value);
		}

		values.Add(current.ToString());
		return values;
	}

	private static bool ContainsHanCharacter(string value)
	{
		foreach (char character in value)
		{
			if (character is >= '\u3400' and <= '\u9fff')
			{
				return true;
			}
		}

		return false;
	}

	private static List<string> ReadSupportedLocalePreferences(string repoRoot)
	{
		string localizationSourcePath = Path.Combine(repoRoot, "scripts", "Localization.cs");
		True(File.Exists(localizationSourcePath));

		string source = File.ReadAllText(localizationSourcePath);
		var locales = new List<string>();
		foreach (Match match in Regex.Matches(
			source,
			"^\\s*\\(\"(?<preference>[^\"]+)\",\\s*\"[^\"]+\",\\s*\"[^\"]*\",\\s*\"[^\"]*\"\\),?\\s*$",
			RegexOptions.Multiline))
		{
			string preference = match.Groups["preference"].Value.Trim();
			if (!string.IsNullOrWhiteSpace(preference) && preference != "auto")
			{
				locales.Add(preference);
			}
		}

		True(locales.Count > 0);
		return locales;
	}

	private static IEnumerable<string> EnumerateRuntimeLocalizationReferenceFiles(string repoRoot)
	{
		foreach (string absolutePath in Directory.GetFiles(repoRoot, "*.*", SearchOption.AllDirectories))
		{
			string extension = Path.GetExtension(absolutePath);
			if (extension != ".cs" && extension != ".tscn" && extension != ".tres")
			{
				continue;
			}

			string relativePath = Path.GetRelativePath(repoRoot, absolutePath).Replace('\\', '/');
			if (IsExcludedRuntimeLocalizationPath(relativePath))
			{
				continue;
			}

			yield return relativePath;
		}
	}

	private static bool IsExcludedRuntimeLocalizationPath(string relativePath)
	{
		if (relativePath.StartsWith("tests/", StringComparison.Ordinal)
			|| relativePath.StartsWith("addons/", StringComparison.Ordinal)
			|| relativePath.StartsWith(".godot/", StringComparison.Ordinal)
			|| relativePath.StartsWith("outputs/", StringComparison.Ordinal))
		{
			return true;
		}

		if (relativePath.EndsWith(".uid", StringComparison.Ordinal)
			|| relativePath.EndsWith(".import", StringComparison.Ordinal))
		{
			return true;
		}

		if (relativePath.Contains("gallery", StringComparison.OrdinalIgnoreCase)
			|| relativePath.Contains("preview", StringComparison.OrdinalIgnoreCase)
			|| relativePath.Contains("_editor_", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (relativePath == "scenes/adventure_suit_upgrade_editor_scene.tscn"
			|| relativePath == "scenes/upgrade_choice_popup_asset_scene.tscn")
		{
			return true;
		}

		return relativePath.EndsWith(".cs", StringComparison.Ordinal)
			&& (relativePath.Contains(".Debug", StringComparison.Ordinal)
				|| relativePath.Contains("DebugRecording", StringComparison.Ordinal));
	}

	private static IEnumerable<LocalizationReference> FindLocalizationReferences(string relativePath, string[] lines)
	{
		for (int index = 0; index < lines.Length; index++)
		{
			string line = lines[index];
			if (relativePath.EndsWith(".cs", StringComparison.Ordinal))
			{
				foreach (Match match in Regex.Matches(line, "\\b(?:Localize|localize|Tr)\\(\\s*\"(?<key>[A-Z0-9_]+)\"\\s*\\)"))
				{
					string key = match.Groups["key"].Value;
					if (IsLikelyLocalizationKey(key))
					{
						yield return new LocalizationReference(relativePath, index + 1, key);
					}
				}

				continue;
			}

			if (relativePath.EndsWith(".tscn", StringComparison.Ordinal))
			{
				foreach (Match match in Regex.Matches(line, "^\\s*(?:text|placeholder_text|tooltip_text|popup/item_\\d+/text)\\s*=\\s*\"(?<key>[^\"]*)\"$"))
				{
					string key = match.Groups["key"].Value.Trim();
					if (IsLikelyLocalizationKey(key))
					{
						yield return new LocalizationReference(relativePath, index + 1, key);
					}
				}

				continue;
			}

			foreach (Match match in Regex.Matches(line, "^\\s*[A-Za-z0-9_]*Key\\s*=\\s*\"(?<key>[^\"]*)\"$"))
			{
				string key = match.Groups["key"].Value.Trim();
				if (IsLikelyLocalizationKey(key))
				{
					yield return new LocalizationReference(relativePath, index + 1, key);
				}
			}
		}
	}

	private static bool IsLikelyLocalizationKey(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || !value.Contains('_', StringComparison.Ordinal))
		{
			return false;
		}

		foreach (char character in value)
		{
			if ((character < 'A' || character > 'Z') && (character < '0' || character > '9') && character != '_')
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsAllowedRuntimeSceneText(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || IsLikelyLocalizationKey(value))
		{
			return true;
		}

		if (Regex.IsMatch(value, "^v\\d+(?:\\.\\d+)*$", RegexOptions.CultureInvariant)
			|| Regex.IsMatch(value, "^\\d+$", RegexOptions.CultureInvariant)
			|| Regex.IsMatch(value, "^\\d+%$", RegexOptions.CultureInvariant)
			|| Regex.IsMatch(value, "^\\d+\\s*x\\s*\\d+$", RegexOptions.CultureInvariant))
		{
			return true;
		}

		if (value is "A" or "B" or "J" or "Q" or "K" or "P" or "Enter" or "Escape" or "Start"
			or "Up" or "Down" or "Left" or "Right"
			or "D-Pad Up" or "D-Pad Down" or "D-Pad Left" or "D-Pad Right"
			or "Deckland" or "Steam" or "LAN")
		{
			return true;
		}

		return !ContainsHanCharacter(value) && !Regex.IsMatch(value, "[A-Za-z]", RegexOptions.CultureInvariant);
	}

	private static int ReadTresInt(string tresText, string propertyName, int fallback)
	{
		Match match = Regex.Match(tresText, $"^{Regex.Escape(propertyName)} = (-?\\d+)$", RegexOptions.Multiline);
		return match.Success && int.TryParse(match.Groups[1].Value, out int value) ? value : fallback;
	}

	private static readonly string[] RuntimeHardcodedSceneRelativePaths =
	{
		"scenes/main_menu_scene.tscn",
		"scenes/main_menu_player_count_dialog.tscn",
		"scenes/main_menu_settings_dialog.tscn",
		"scenes/main_menu_controls_dialog.tscn",
		"scenes/main_menu_exit_dialog.tscn",
		"scenes/main_menu_avatar_entry.tscn",
		"scenes/character_avatar_edit_scene.tscn",
		"scenes/avatar_unlock_prompt.tscn",
		"scenes/multiplayer_lobby_scene.tscn",
		"scenes/multiplayer_lobby_room_row.tscn",
		"scenes/multiplayer_lobby_player_row.tscn",
		"scenes/game_board_result_banner.tscn",
		"scenes/pause_menu.tscn",
		"scenes/item_selection_overlay.tscn",
		"scenes/adventure_avatar_menu_scene.tscn",
		"scenes/adventure_level_entry.tscn",
		"scenes/adventure_mode_entry.tscn",
		"scenes/adventure_upgrade_selection_overlay.tscn",
	};

	private readonly record struct LocalizationReference(string RelativePath, int LineNumber, string Key);

	private static void AssertFramePatternExists(string repoRoot, string framePattern, int frameCount)
	{
		for (int frameIndex = 1; frameIndex <= frameCount; frameIndex++)
		{
			AssertResPathExists(repoRoot, string.Format(framePattern, frameIndex));
		}
	}

	private static void AssertResPathExists(string repoRoot, string resPath)
	{
		True(resPath.StartsWith("res://", StringComparison.Ordinal));
		string absolutePath = Path.Combine(repoRoot, resPath["res://".Length..].Replace('/', Path.DirectorySeparatorChar));
		True(File.Exists(absolutePath));
		True(new FileInfo(absolutePath).Length > 0);
	}

	private static int CountContiguousFrames(string repoRoot, string resDirectory, string filePattern)
	{
		True(resDirectory.StartsWith("res://", StringComparison.Ordinal));
		string absoluteDirectory = Path.Combine(repoRoot, resDirectory["res://".Length..].Replace('/', Path.DirectorySeparatorChar));
		True(Directory.Exists(absoluteDirectory));

		int frameCount = 0;
		for (int frameIndex = 1; ; frameIndex++)
		{
			string absolutePath = Path.Combine(absoluteDirectory, string.Format(filePattern, frameIndex));
			if (!File.Exists(absolutePath))
			{
				break;
			}

			True(new FileInfo(absolutePath).Length > 0);
			frameCount++;
		}

		return frameCount;
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "project.godot")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate project.godot.");
	}

	private static CaptureScoringRules.TableCard TableCard(
		string rank,
		int scoreValue,
		bool isBuriedInSand = false,
		bool isSlimeCoated = false,
		bool hasDoubleCollectItemEffect = false,
		bool hasPairDestroyItemEffect = false,
		int itemOwnerPlayerIndex = -1,
		string suit = "")
	{
		return new CaptureScoringRules.TableCard(
			new CaptureScoringRules.Card(rank, scoreValue, HasDoubleCollectEffect: false, Suit: suit),
			isBuriedInSand,
			isSlimeCoated,
			hasDoubleCollectItemEffect,
			hasPairDestroyItemEffect,
			itemOwnerPlayerIndex);
	}

	private static AdventureUpgradeDefinitionModel Upgrade(string id, string rarity, string suit = "hearts")
	{
		return new AdventureUpgradeDefinitionModel(
			id,
			DisplayName: id,
			suit,
			rarity,
			TriggerText: string.Empty,
			EffectText: string.Empty,
			Enabled: true);
	}

	private static bool ContainsUpgradeId(IReadOnlyList<AdventureUpgradeDefinitionModel> offer, string upgradeId)
	{
		foreach (AdventureUpgradeDefinitionModel definition in offer)
		{
			if (definition.Id == upgradeId)
			{
				return true;
			}
		}

		return false;
	}

	private static bool HasUniqueUpgradeIds(IReadOnlyList<AdventureUpgradeDefinitionModel> offer)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (AdventureUpgradeDefinitionModel definition in offer)
		{
			if (!seen.Add(definition.Id))
			{
				return false;
			}
		}

		return true;
	}


	private sealed class FixedCardTriggerProvider : ICardTriggerProvider
	{
		private readonly int _requestCount;

		internal FixedCardTriggerProvider(int requestCount)
		{
			_requestCount = requestCount;
		}

		public IEnumerable<CardTriggerRequest> CollectCardTriggers(
			CardGameEvent gameEvent,
			int sequenceStart)
		{
			var definition = new ItemCardDefinition(
				"dispatch-test-card",
				string.Empty);
			var rule = new ItemCardEffectRule(
				CardGameEvents.OnPlay,
				CardEffectTypes.DrawCard,
				CardEffectValues.Empty,
				0);
			for (int requestIndex = 0;
				requestIndex < _requestCount;
				requestIndex++)
			{
				yield return new CardTriggerRequest(
					definition,
					rule,
					gameEvent,
					sequenceStart + requestIndex);
			}
		}
	}

	private static void Run(string name, Action test)
	{
		try
		{
			test();
			_passed++;
			Console.WriteLine($"PASS {name}");
		}
		catch (Exception ex)
		{
			_failed++;
			Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
		}
	}

	private static void Equal<T>(T expected, T actual)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
		{
			throw new InvalidOperationException($"expected {expected}, got {actual}");
		}
	}

	private static void True(bool value)
	{
		if (!value)
		{
			throw new InvalidOperationException("expected true, got false");
		}
	}

	private static void False(bool value)
	{
		if (value)
		{
			throw new InvalidOperationException("expected false, got true");
		}
	}
}
