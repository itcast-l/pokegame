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

			while (pendingUnlocks.Count > 0)
			{
				string candidateAvatarId = NormalizeAvatarId(pendingUnlocks[0]);
				pendingUnlocks.RemoveAt(0);
				if (AdventureCampaignCatalog.TryGetMainStageIndex(
						candidateAvatarId,
						out int mainStageIndex)
					&& IsPlayerObtainableAdventureStage(mainStageIndex)
					&& mainStageIndex <= profile.HighestCompletedMainAdventureStage)
				{
					unlockedAvatarId = candidateAvatarId;
					break;
				}
			}

			return profile with
			{
				PendingAvatarUnlockQueue = FormatPendingAdventureAvatarUnlockQueue(pendingUnlocks),
			};
		});
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to consume pending adventure avatar unlock: {saveResult}");
			return string.Empty;
		}

		return unlockedAvatarId;
	}
}
