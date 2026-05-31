using System;
using System.Collections.Generic;
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
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		return GameSaveStore.SaveActiveProfile(profile with
		{
			PendingAvatarUnlockQueue = FormatPendingAdventureAvatarUnlockQueue(queue),
		});
	}
}
