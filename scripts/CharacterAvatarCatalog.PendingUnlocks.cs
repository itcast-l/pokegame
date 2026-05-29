using System;
using System.Collections.Generic;
using Godot;

public static partial class CharacterAvatarCatalog
{
	private static List<string> LoadPendingAdventureAvatarUnlockQueue()
	{
		string rawQueue = GameSaveStore.GetString(GameSaveStore.PendingAvatarUnlockQueueKey, string.Empty);
		List<string> queue = ParsePendingAdventureAvatarUnlockQueue(rawQueue);
		if (queue.Count > 0)
		{
			return queue;
		}

		string legacyAvatarId = GameSaveStore.GetString(GameSaveStore.PendingAvatarUnlockKey, string.Empty);
		if (!string.IsNullOrWhiteSpace(legacyAvatarId))
		{
			queue.Add(legacyAvatarId);
		}

		return queue;
	}

	private static List<string> ParsePendingAdventureAvatarUnlockQueue(string rawQueue)
	{
		if (string.IsNullOrWhiteSpace(rawQueue))
		{
			return new List<string>();
		}

		return new List<string>(rawQueue.Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
	}

	private static string FormatPendingAdventureAvatarUnlockQueue(IReadOnlyList<string> queue)
	{
		return queue.Count > 0 ? string.Join(PendingAvatarUnlockQueueSeparator, queue) : string.Empty;
	}

	private static Error SavePendingAdventureAvatarUnlockQueue(IReadOnlyList<string> queue)
	{
		return GameSaveStore.SetValues(
			new GameSaveStore.SaveValue(GameSaveStore.PendingAvatarUnlockQueueKey, FormatPendingAdventureAvatarUnlockQueue(queue)),
			new GameSaveStore.SaveValue(GameSaveStore.PendingAvatarUnlockKey, string.Empty),
			new GameSaveStore.SaveValue(GameSaveStore.PendingAvatarUnlockStageKey, 0));
	}
}
