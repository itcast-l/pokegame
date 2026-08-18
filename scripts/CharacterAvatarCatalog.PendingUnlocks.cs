using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public static partial class CharacterAvatarCatalog
{
	private static List<string> LoadPendingAdventureAvatarUnlockQueue()
	{
		string rawQueue = GameSaveStore.LoadActiveProfile().PendingAvatarUnlockQueue;
		return ParsePendingAdventureAvatarUnlockQueue(rawQueue);
	}

	private static List<string> ParsePendingAdventureAvatarUnlockQueue(string rawQueue)
	{
		string normalizedQueue = AdventureCampaignCatalog.NormalizePendingAvatarUnlockQueueCsv(rawQueue);
		return string.IsNullOrWhiteSpace(normalizedQueue)
			? new List<string>()
			: new List<string>(normalizedQueue.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
	}

	private static string FormatPendingAdventureAvatarUnlockQueue(IReadOnlyList<string> queue)
	{
		return queue.Count > 0 ? string.Join(PendingAvatarUnlockQueueSeparator, queue) : string.Empty;
	}

	private static Error SavePendingAdventureAvatarUnlockQueue(IReadOnlyList<string> queue)
	{
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		return GameSaveStore.SaveActiveProfile(profile with
		{
			PendingAvatarUnlockQueue = FormatPendingAdventureAvatarUnlockQueue(queue),
		});
	}

	public static async Task<string> ConsumePendingAdventureAvatarUnlockAsync()
	{
		string unlockedAvatarId = string.Empty;
		Error saveResult = await GameSaveStore.UpdateActiveProfileAsync(profile =>
		{
			List<string> pendingUnlocks = ParsePendingAdventureAvatarUnlockQueue(
				profile.PendingAvatarUnlockQueue);
			if (pendingUnlocks.Count == 0)
			{
				return profile;
			}
			if (!PendingAvatarUnlockQueueRules.TryPeek(
					pendingUnlocks,
					NormalizeAvatarId,
					avatarId => IsEligiblePendingAdventureAvatar(profile, avatarId),
					out string candidateAvatarId))
			{
				return profile with { PendingAvatarUnlockQueue = string.Empty };
			}
			if (!PendingAvatarUnlockQueueRules.TryConsumeExpected(
					pendingUnlocks,
					candidateAvatarId,
					NormalizeAvatarId,
					avatarId => IsEligiblePendingAdventureAvatar(profile, avatarId),
					out List<string> remainingUnlocks))
			{
				return profile;
			}

			unlockedAvatarId = candidateAvatarId;
			return profile with
			{
				PendingAvatarUnlockQueue = FormatPendingAdventureAvatarUnlockQueue(remainingUnlocks),
			};
		});
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to consume pending adventure avatar unlock: {saveResult}");
			return string.Empty;
		}

		return unlockedAvatarId;
	}

	public static async Task<string> PeekPendingAdventureAvatarUnlockAsync()
	{
		string unlockedAvatarId = string.Empty;
		Error readResult = await GameSaveStore.UpdateActiveProfileAsync(profile =>
		{
			List<string> pendingUnlocks = ParsePendingAdventureAvatarUnlockQueue(
				profile.PendingAvatarUnlockQueue);
			PendingAvatarUnlockQueueRules.TryPeek(
				pendingUnlocks,
				NormalizeAvatarId,
				avatarId => IsEligiblePendingAdventureAvatar(profile, avatarId),
				out unlockedAvatarId);
			return profile;
		});
		if (readResult != Error.Ok)
		{
			GD.PushWarning($"Failed to inspect pending adventure avatar unlock: {readResult}");
			return string.Empty;
		}

		return unlockedAvatarId;
	}

	public static async Task<Error> ConsumePendingAdventureAvatarUnlockAsync(string expectedAvatarId)
	{
		string normalizedExpectedAvatarId = NormalizeAvatarId(expectedAvatarId);
		bool consumed = false;
		Error saveResult = await GameSaveStore.UpdateActiveProfileAsync(profile =>
		{
			List<string> pendingUnlocks = ParsePendingAdventureAvatarUnlockQueue(
				profile.PendingAvatarUnlockQueue);
			if (!PendingAvatarUnlockQueueRules.TryConsumeExpected(
					pendingUnlocks,
					normalizedExpectedAvatarId,
					NormalizeAvatarId,
					avatarId => IsEligiblePendingAdventureAvatar(profile, avatarId),
					out List<string> remainingUnlocks))
			{
				return profile;
			}

			consumed = true;
			return profile with
			{
				PendingAvatarUnlockQueue = FormatPendingAdventureAvatarUnlockQueue(remainingUnlocks),
			};
		});
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to consume expected adventure avatar unlock: {saveResult}");
			return saveResult;
		}

		return consumed ? Error.Ok : Error.DoesNotExist;
	}

	private static bool IsEligiblePendingAdventureAvatar(
		GameSaveStore.GameProgressData profile,
		string avatarId)
	{
		return AdventureCampaignCatalog.TryGetMainStageIndex(avatarId, out int mainStageIndex)
			&& IsPlayerObtainableAdventureStage(mainStageIndex)
			&& mainStageIndex <= profile.HighestCompletedMainAdventureStage;
	}
}
