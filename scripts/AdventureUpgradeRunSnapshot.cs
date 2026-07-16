using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

internal enum AdventureUpgradeRestoreResult
{
	NoSnapshot,
	Restored,
	Migrated,
	DeferredManifestUnavailable,
	InvalidSnapshotPreserved,
	InvalidJsonPreserved,
}

internal enum AdventureUpgradeAcquireResult
{
	Acquired,
	AlreadyAcquired,
	DeferredManifestUnavailable,
	RejectedUnknownUpgrade,
}

[JsonConverter(typeof(AdventureUpgradeRunSnapshotJsonConverter))]
internal readonly record struct AdventureUpgradeRunSnapshot
{
	internal const int CurrentSchemaVersion = 7;

	public int SchemaVersion { get; init; }
	public string CampaignId { get; init; }
	public int MainStageIndex { get; init; }
	public string EnemyAvatarId { get; init; }
	public bool ShouldGrantRunStartUpgrade { get; init; }
	public bool ShouldGrantVictoryUpgrade { get; init; }
	public IReadOnlyList<string> AcquiredUpgradeIds { get; init; }
	public int CurrentHealth { get; init; }
	public int MaxHealth { get; init; }
	public int PermanentCaptureDamageBonus { get; init; }
	public int QuestionRewardCount { get; init; }
	public int HealingOverflowRemainder { get; init; }
	public bool HasUsedRevive { get; init; }

	[JsonIgnore]
	public int CurrentAvatarStageNumber => LegacyCurrentAvatarStageNumber != 0
		? LegacyCurrentAvatarStageNumber
		: AdventureCampaignCatalog.LegacyAvatarStageForMainStage(MainStageIndex);

	[JsonIgnore]
	public int CurrentRunStageIndex { get; init; }

	[JsonIgnore]
	public bool FinalDoubleCaptureDamageActive { get; init; }

	[JsonIgnore]
	public int NextCaptureDamageBonus { get; init; }

	[JsonIgnore]
	public int NextTurnHealAmount { get; init; }

	[JsonIgnore]
	public int NextTurnDrawCount { get; init; }

	[JsonIgnore]
	public bool ClubShortBranchReturnUsedThisTurn { get; init; }

	[JsonIgnore]
	public int ClubMissClimbGrowthStacks { get; init; }

	[JsonIgnore]
	public bool ClubBorrowSuitSetArmed { get; init; }

	[JsonIgnore]
	public bool ClubGapRootingUsedThisTurn { get; init; }

	[JsonIgnore]
	public int FinalDoubleQuestionRewardCountThisBattle { get; init; }

	[JsonIgnore]
	public int ClubFlourishStacks { get; init; }

	[JsonIgnore]
	internal int LegacyCurrentAvatarStageNumber { get; init; }

	public AdventureUpgradeRunSnapshot(
		int SchemaVersion,
		string CampaignId,
		int MainStageIndex,
		string EnemyAvatarId,
		bool ShouldGrantRunStartUpgrade,
		bool ShouldGrantVictoryUpgrade,
		IReadOnlyList<string> AcquiredUpgradeIds,
		int CurrentHealth = 100,
		int MaxHealth = 100,
		int PermanentCaptureDamageBonus = 0,
		int QuestionRewardCount = 0,
		int HealingOverflowRemainder = 0,
		bool HasUsedRevive = false)
	{
		this.SchemaVersion = SchemaVersion;
		this.CampaignId = CampaignId ?? string.Empty;
		this.MainStageIndex = MainStageIndex;
		this.EnemyAvatarId = EnemyAvatarId ?? string.Empty;
		this.ShouldGrantRunStartUpgrade = ShouldGrantRunStartUpgrade;
		this.ShouldGrantVictoryUpgrade = ShouldGrantVictoryUpgrade;
		this.AcquiredUpgradeIds = AcquiredUpgradeIds ?? Array.Empty<string>();
		this.CurrentHealth = CurrentHealth;
		this.MaxHealth = MaxHealth;
		this.PermanentCaptureDamageBonus = PermanentCaptureDamageBonus;
		this.QuestionRewardCount = QuestionRewardCount;
		this.HealingOverflowRemainder = HealingOverflowRemainder;
		this.HasUsedRevive = HasUsedRevive;
		this.CurrentRunStageIndex = 0;
		this.LegacyCurrentAvatarStageNumber = 0;
	}

	// Source compatibility for v1-v5 callers and fixtures. New JSON never uses this shape.
	public AdventureUpgradeRunSnapshot(
		int SchemaVersion,
		int CurrentAvatarStageNumber,
		int CurrentRunStageIndex,
		bool ShouldGrantRunStartUpgrade,
		bool ShouldGrantVictoryUpgrade,
		IReadOnlyList<string> AcquiredUpgradeIds,
		int CurrentHealth = 100,
		int MaxHealth = 100,
		int PermanentCaptureDamageBonus = 0,
		int QuestionRewardCount = 0,
		bool FinalDoubleCaptureDamageActive = false,
		int HealingOverflowRemainder = 0,
		int NextCaptureDamageBonus = 0,
		int NextTurnHealAmount = 0,
		int NextTurnDrawCount = 0,
		bool ClubShortBranchReturnUsedThisTurn = false,
		int ClubMissClimbGrowthStacks = 0,
		bool ClubBorrowSuitSetArmed = false,
		bool ClubGapRootingUsedThisTurn = false,
		int FinalDoubleQuestionRewardCountThisBattle = 0,
		int ClubFlourishStacks = 0)
	{
		this.SchemaVersion = SchemaVersion;
		this.LegacyCurrentAvatarStageNumber = CurrentAvatarStageNumber;
		this.CampaignId = AdventureCampaignCatalog.TryMapLegacyRunStage(
			CurrentAvatarStageNumber,
			out int mainStageIndex)
			? AdventureCampaignCatalog.MainCampaignId
			: string.Empty;
		this.MainStageIndex = mainStageIndex;
		this.EnemyAvatarId = AdventureCampaignCatalog.GetMainAvatarId(mainStageIndex);
		this.ShouldGrantRunStartUpgrade = ShouldGrantRunStartUpgrade;
		this.ShouldGrantVictoryUpgrade = ShouldGrantVictoryUpgrade;
		this.AcquiredUpgradeIds = AcquiredUpgradeIds ?? Array.Empty<string>();
		this.CurrentHealth = CurrentHealth;
		this.MaxHealth = MaxHealth;
		this.PermanentCaptureDamageBonus = PermanentCaptureDamageBonus;
		this.QuestionRewardCount = QuestionRewardCount;
		this.HealingOverflowRemainder = HealingOverflowRemainder;
		this.HasUsedRevive = false;
		this.CurrentRunStageIndex = CurrentRunStageIndex;
		this.FinalDoubleCaptureDamageActive = FinalDoubleCaptureDamageActive;
		this.NextCaptureDamageBonus = NextCaptureDamageBonus;
		this.NextTurnHealAmount = NextTurnHealAmount;
		this.NextTurnDrawCount = NextTurnDrawCount;
		this.ClubShortBranchReturnUsedThisTurn = ClubShortBranchReturnUsedThisTurn;
		this.ClubMissClimbGrowthStacks = ClubMissClimbGrowthStacks;
		this.ClubBorrowSuitSetArmed = ClubBorrowSuitSetArmed;
		this.ClubGapRootingUsedThisTurn = ClubGapRootingUsedThisTurn;
		this.FinalDoubleQuestionRewardCountThisBattle = FinalDoubleQuestionRewardCountThisBattle;
		this.ClubFlourishStacks = ClubFlourishStacks;
	}

	internal static bool TryDeserialize(
		string json,
		out AdventureUpgradeRunSnapshot snapshot,
		out bool migrated,
		out string failure)
	{
		snapshot = default;
		migrated = false;
		failure = string.Empty;
		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(json);
		}
		catch (JsonException error)
		{
			failure = error.Message;
			return false;
		}

		using (document)
		{
			JsonElement root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object
				|| !TryGetInt(root, nameof(SchemaVersion), out int schemaVersion)
				|| schemaVersion < 1
				|| schemaVersion > CurrentSchemaVersion)
			{
				failure = "Unsupported or missing snapshot schema version.";
				return false;
			}

			bool shouldGrantRunStart = GetBool(root, nameof(ShouldGrantRunStartUpgrade), false);
			bool shouldGrantVictory = GetBool(root, nameof(ShouldGrantVictoryUpgrade), true);
			if (!root.TryGetProperty(nameof(AcquiredUpgradeIds), out JsonElement acquiredIdsElement)
				|| acquiredIdsElement.ValueKind != JsonValueKind.Array)
			{
				failure = "Snapshot is missing an acquired upgrade id array.";
				return false;
			}

			IReadOnlyList<string> acquiredIds = GetStringList(root, nameof(AcquiredUpgradeIds));
			int currentHealth = GetInt(root, nameof(CurrentHealth), 100);
			int maxHealth = GetInt(root, nameof(MaxHealth), 100);
			int permanentBonus = GetInt(root, nameof(PermanentCaptureDamageBonus), 0);
			int questionRewardCount = GetInt(root, nameof(QuestionRewardCount), 0);
			int healingOverflow = GetInt(root, nameof(HealingOverflowRemainder), 0);
			bool hasUsedRevive = schemaVersion >= 7
				&& GetBool(root, nameof(HasUsedRevive), false);

			if (schemaVersion < 6)
			{
				if (!TryGetInt(root, nameof(CurrentAvatarStageNumber), out int legacyStage))
				{
					failure = "Legacy snapshot is missing CurrentAvatarStageNumber.";
					return false;
				}

				snapshot = new AdventureUpgradeRunSnapshot(
					schemaVersion,
					legacyStage,
					GetInt(root, nameof(CurrentRunStageIndex), 0),
					shouldGrantRunStart,
					shouldGrantVictory,
					acquiredIds,
					currentHealth,
					maxHealth,
					permanentBonus,
					questionRewardCount,
					GetBool(root, nameof(FinalDoubleCaptureDamageActive), false),
					healingOverflow,
					GetInt(root, nameof(NextCaptureDamageBonus), 0),
					GetInt(root, nameof(NextTurnHealAmount), 0),
					GetInt(root, nameof(NextTurnDrawCount), 0),
					GetBool(root, nameof(ClubShortBranchReturnUsedThisTurn), false),
					GetInt(root, nameof(ClubMissClimbGrowthStacks), 0),
					GetBool(root, nameof(ClubBorrowSuitSetArmed), false),
					GetBool(root, nameof(ClubGapRootingUsedThisTurn), false),
					GetInt(root, nameof(FinalDoubleQuestionRewardCountThisBattle), 0),
					GetInt(root, nameof(ClubFlourishStacks), 0));
				migrated = true;
				return true;
			}

			if (!TryGetString(root, nameof(CampaignId), out string campaignId)
				|| !TryGetInt(root, nameof(MainStageIndex), out int mainStageIndex)
				|| !TryGetString(root, nameof(EnemyAvatarId), out string enemyAvatarId))
			{
				failure = "Canonical snapshot is missing campaign identity fields.";
				return false;
			}

			snapshot = new AdventureUpgradeRunSnapshot(
				schemaVersion,
				campaignId,
				mainStageIndex,
				enemyAvatarId,
				shouldGrantRunStart,
				shouldGrantVictory,
				acquiredIds,
				currentHealth,
				maxHealth,
				permanentBonus,
				questionRewardCount,
				healingOverflow,
				hasUsedRevive);
			return true;
		}
	}

	private static bool TryGetInt(JsonElement root, string name, out int value)
	{
		if (root.TryGetProperty(name, out JsonElement element)
			&& element.ValueKind == JsonValueKind.Number
			&& element.TryGetInt32(out value))
		{
			return true;
		}

		value = 0;
		return false;
	}

	private static int GetInt(JsonElement root, string name, int fallback)
	{
		return TryGetInt(root, name, out int value) ? value : fallback;
	}

	private static bool GetBool(JsonElement root, string name, bool fallback)
	{
		return root.TryGetProperty(name, out JsonElement element)
			&& (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
			? element.GetBoolean()
			: fallback;
	}

	private static bool TryGetString(JsonElement root, string name, out string value)
	{
		if (root.TryGetProperty(name, out JsonElement element)
			&& element.ValueKind == JsonValueKind.String)
		{
			value = element.GetString() ?? string.Empty;
			return true;
		}

		value = string.Empty;
		return false;
	}

	private static List<string> GetStringList(JsonElement root, string name)
	{
		var result = new List<string>();
		if (!root.TryGetProperty(name, out JsonElement element)
			|| element.ValueKind != JsonValueKind.Array)
		{
			return result;
		}

		foreach (JsonElement item in element.EnumerateArray())
		{
			if (item.ValueKind == JsonValueKind.String)
			{
				result.Add(item.GetString() ?? string.Empty);
			}
		}

		return result;
	}
}

internal sealed class AdventureUpgradeRunSnapshotJsonConverter : JsonConverter<AdventureUpgradeRunSnapshot>
{
	public override AdventureUpgradeRunSnapshot Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		string json = document.RootElement.GetRawText();
		if (!AdventureUpgradeRunSnapshot.TryDeserialize(
			json,
			out AdventureUpgradeRunSnapshot snapshot,
			out _,
			out string failure))
		{
			throw new JsonException(failure);
		}

		return snapshot;
	}

	public override void Write(
		Utf8JsonWriter writer,
		AdventureUpgradeRunSnapshot value,
		JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.SchemaVersion), value.SchemaVersion);
		if (value.SchemaVersion < 6)
		{
			// The compatibility constructor is still used by old fixtures and
			// migration tooling. Preserve its historical wire shape so those
			// payloads remain valid legacy inputs; all runtime snapshots use the current schema.
			writer.WriteNumber(
				nameof(AdventureUpgradeRunSnapshot.CurrentAvatarStageNumber),
				value.LegacyCurrentAvatarStageNumber != 0
					? value.LegacyCurrentAvatarStageNumber
					: value.CurrentAvatarStageNumber);
			writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.CurrentRunStageIndex), value.CurrentRunStageIndex);
			writer.WriteBoolean(nameof(AdventureUpgradeRunSnapshot.ShouldGrantRunStartUpgrade), value.ShouldGrantRunStartUpgrade);
			writer.WriteBoolean(nameof(AdventureUpgradeRunSnapshot.ShouldGrantVictoryUpgrade), value.ShouldGrantVictoryUpgrade);
			writer.WritePropertyName(nameof(AdventureUpgradeRunSnapshot.AcquiredUpgradeIds));
			writer.WriteStartArray();
			if (value.AcquiredUpgradeIds != null)
			{
				foreach (string upgradeId in value.AcquiredUpgradeIds)
				{
					writer.WriteStringValue(upgradeId);
				}
			}
			writer.WriteEndArray();
			writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.CurrentHealth), value.CurrentHealth);
			writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.MaxHealth), value.MaxHealth);
			writer.WriteNumber(
				nameof(AdventureUpgradeRunSnapshot.PermanentCaptureDamageBonus),
				value.PermanentCaptureDamageBonus);
			writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.QuestionRewardCount), value.QuestionRewardCount);
			writer.WriteBoolean(
				nameof(AdventureUpgradeRunSnapshot.FinalDoubleCaptureDamageActive),
				value.FinalDoubleCaptureDamageActive);
			writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.HealingOverflowRemainder), value.HealingOverflowRemainder);
			writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.NextCaptureDamageBonus), value.NextCaptureDamageBonus);
			writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.NextTurnHealAmount), value.NextTurnHealAmount);
			writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.NextTurnDrawCount), value.NextTurnDrawCount);
			writer.WriteBoolean(
				nameof(AdventureUpgradeRunSnapshot.ClubShortBranchReturnUsedThisTurn),
				value.ClubShortBranchReturnUsedThisTurn);
			writer.WriteNumber(
				nameof(AdventureUpgradeRunSnapshot.ClubMissClimbGrowthStacks),
				value.ClubMissClimbGrowthStacks);
			writer.WriteBoolean(
				nameof(AdventureUpgradeRunSnapshot.ClubBorrowSuitSetArmed),
				value.ClubBorrowSuitSetArmed);
			writer.WriteBoolean(
				nameof(AdventureUpgradeRunSnapshot.ClubGapRootingUsedThisTurn),
				value.ClubGapRootingUsedThisTurn);
			writer.WriteNumber(
				nameof(AdventureUpgradeRunSnapshot.FinalDoubleQuestionRewardCountThisBattle),
				value.FinalDoubleQuestionRewardCountThisBattle);
			writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.ClubFlourishStacks), value.ClubFlourishStacks);
			writer.WriteEndObject();
			return;
		}

		writer.WriteString(nameof(AdventureUpgradeRunSnapshot.CampaignId), value.CampaignId);
		writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.MainStageIndex), value.MainStageIndex);
		writer.WriteString(nameof(AdventureUpgradeRunSnapshot.EnemyAvatarId), value.EnemyAvatarId);
		writer.WriteBoolean(nameof(AdventureUpgradeRunSnapshot.ShouldGrantRunStartUpgrade), value.ShouldGrantRunStartUpgrade);
		writer.WriteBoolean(nameof(AdventureUpgradeRunSnapshot.ShouldGrantVictoryUpgrade), value.ShouldGrantVictoryUpgrade);
		writer.WritePropertyName(nameof(AdventureUpgradeRunSnapshot.AcquiredUpgradeIds));
		writer.WriteStartArray();
		if (value.AcquiredUpgradeIds != null)
		{
			foreach (string upgradeId in value.AcquiredUpgradeIds)
			{
				writer.WriteStringValue(upgradeId);
			}
		}
		writer.WriteEndArray();
		writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.CurrentHealth), value.CurrentHealth);
		writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.MaxHealth), value.MaxHealth);
		writer.WriteNumber(
			nameof(AdventureUpgradeRunSnapshot.PermanentCaptureDamageBonus),
			value.PermanentCaptureDamageBonus);
		writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.QuestionRewardCount), value.QuestionRewardCount);
		writer.WriteNumber(nameof(AdventureUpgradeRunSnapshot.HealingOverflowRemainder), value.HealingOverflowRemainder);
		if (value.SchemaVersion >= 7)
		{
			writer.WriteBoolean(nameof(AdventureUpgradeRunSnapshot.HasUsedRevive), value.HasUsedRevive);
		}
		writer.WriteEndObject();
	}
}
