using System;
using Godot;

public static class NetworkAuthorityActionVariantCodec
{
	public static Godot.Collections.Dictionary ToVariant(AuthorityActionState state)
	{
		ArgumentNullException.ThrowIfNull(state);
		return new Godot.Collections.Dictionary
		{
			["action_id"] = state.ActionId,
			["client_action_id"] = state.ClientActionId,
			["kind"] = state.Kind.ToString(),
			["phase"] = state.Phase.ToString(),
			["actor_seat"] = state.ActorSeat,
			["turn_generation"] = state.TurnGeneration,
			["base_hand_index"] = state.BaseHandIndex,
			["item_hand_index"] = state.ItemHandIndex,
			["item_or_reward_id"] = state.ItemOrRewardId,
			["base_card_code"] = state.BaseCardCode,
			["rng_state_before_action"] = state.RngStateBeforeAction,
			["target_seat"] = state.TargetSeat,
			["target_hand_index"] = state.TargetHandIndex,
			["reserve_replacement"] = state.ReserveReplacement,
			["played"] = state.Played,
			["captured"] = state.Captured,
			["completes_turn"] = state.CompletesTurn,
			["deck_collected"] = state.DeckCollected,
			["remaining_attachment_reward_queue"] = new Godot.Collections.Array<string>(state.RemainingAttachmentRewardQueue),
			["remaining_attachment_reward_queue_seats"] = new Godot.Collections.Array<int>(state.RemainingAttachmentRewardQueueSeats),
			["continuation_step"] = state.ContinuationStep.ToString(),
			["primary_mutation_applied"] = state.PrimaryMutationApplied,
			["turn_advance_applied"] = state.TurnAdvanceApplied,
			["pending_reward_item_id"] = state.PendingRewardItemId,
			["pending_reward_seat"] = state.PendingRewardSeat,
			["pending_reward_hand_index"] = state.PendingRewardHandIndex,
			["pending_reward_requires_attachment"] = state.PendingRewardRequiresAttachment,
			["transfer_mutation_applied"] = state.TransferMutationApplied,
			["question_reward_mutation_applied"] = state.QuestionRewardMutationApplied,
			["discard_mutation_applied"] = state.DiscardMutationApplied,
			["attachment_mutation_applied"] = state.AttachmentMutationApplied,
			["selection_index"] = state.SelectionIndex,
		};
	}

	public static bool TryRead(
		Variant rawValue,
		out AuthorityActionState state,
		int playerCount = NetworkProtocol.MaxAuthorityCheckpointPlayers)
	{
		state = AuthorityActionState.Empty;
		if (rawValue.VariantType != Variant.Type.Dictionary)
		{
			return false;
		}

		Godot.Collections.Dictionary dictionary = rawValue.AsGodotDictionary();
		if (!TryReadInt64(dictionary, "action_id", out long actionId)
			|| !TryReadInt64(dictionary, "client_action_id", out long clientActionId)
			|| !TryReadEnum(dictionary, "kind", out AuthorityActionKind kind)
			|| !TryReadEnum(dictionary, "phase", out AuthorityActionPhase phase)
			|| !TryReadInt32(dictionary, "actor_seat", out int actorSeat)
			|| !TryReadInt32(dictionary, "turn_generation", out int turnGeneration)
			|| !TryReadInt32(dictionary, "base_hand_index", out int baseHandIndex)
			|| !TryReadInt32(dictionary, "item_hand_index", out int itemHandIndex)
			|| !TryReadString(dictionary, "item_or_reward_id", out string itemOrRewardId)
			|| !TryReadString(dictionary, "base_card_code", out string baseCardCode)
			|| !TryReadString(dictionary, "rng_state_before_action", out string rngStateBeforeAction)
			|| !TryReadInt32(dictionary, "target_seat", out int targetSeat)
			|| !TryReadInt32(dictionary, "target_hand_index", out int targetHandIndex)
			|| !TryReadBool(dictionary, "reserve_replacement", out bool reserveReplacement)
			|| !TryReadBool(dictionary, "played", out bool played)
			|| !TryReadBool(dictionary, "captured", out bool captured)
			|| !TryReadBool(dictionary, "completes_turn", out bool completesTurn)
			|| !TryReadBool(dictionary, "deck_collected", out bool deckCollected)
			|| !TryReadStringArray(dictionary, "remaining_attachment_reward_queue", out string[] queue))
		{
			return false;
		}

		string[] queueSeatValues = Array.Empty<string>();
		int[] queueSeats = Array.Empty<int>();
		if (dictionary.ContainsKey("remaining_attachment_reward_queue_seats")
			&& !TryReadInt32Array(dictionary, "remaining_attachment_reward_queue_seats", out queueSeats))
		{
			return false;
		}

		AuthorityActionContinuationStep continuationStep = AuthorityActionContinuationStep.PrimaryMutation;
		bool primaryMutationApplied = false;
		bool turnAdvanceApplied = false;
		string pendingRewardItemId = string.Empty;
		int pendingRewardSeat = -1;
		int pendingRewardHandIndex = -1;
		bool pendingRewardRequiresAttachment = false;
		bool transferMutationApplied = false;
		bool questionRewardMutationApplied = false;
		bool discardMutationApplied = false;
		bool attachmentMutationApplied = false;
		int selectionIndex = -1;
		if (dictionary.ContainsKey("continuation_step")
			&& (!TryReadEnum(dictionary, "continuation_step", out continuationStep)
				|| !TryReadBool(dictionary, "primary_mutation_applied", out primaryMutationApplied)
				|| !TryReadBool(dictionary, "turn_advance_applied", out turnAdvanceApplied)
				|| !TryReadString(dictionary, "pending_reward_item_id", out pendingRewardItemId)
				|| !TryReadInt32(dictionary, "pending_reward_seat", out pendingRewardSeat)
				|| !TryReadInt32(dictionary, "pending_reward_hand_index", out pendingRewardHandIndex)
				|| !TryReadBool(dictionary, "pending_reward_requires_attachment", out pendingRewardRequiresAttachment)))
		{
				return false;
		}

		if (dictionary.ContainsKey("transfer_mutation_applied")
			&& (!TryReadBool(dictionary, "transfer_mutation_applied", out transferMutationApplied)
				|| !TryReadBool(dictionary, "question_reward_mutation_applied", out questionRewardMutationApplied)
				|| !TryReadBool(dictionary, "discard_mutation_applied", out discardMutationApplied)
				|| !TryReadBool(dictionary, "attachment_mutation_applied", out attachmentMutationApplied)))
		{
			return false;
		}

		if (!TryReadInt32(dictionary, "selection_index", out selectionIndex))
		{
			return false;
		}

		if (queue.Length > 0 && queueSeats.Length == 0)
		{
			queueSeats = Array.ConvertAll(queue, _ => actorSeat);
		}

		var decoded = new AuthorityActionState
		{
			ActionId = actionId,
			ClientActionId = clientActionId,
			Kind = kind,
			Phase = phase,
			ActorSeat = actorSeat,
			TurnGeneration = turnGeneration,
			BaseHandIndex = baseHandIndex,
			ItemHandIndex = itemHandIndex,
			ItemOrRewardId = itemOrRewardId,
			BaseCardCode = baseCardCode,
			RngStateBeforeAction = rngStateBeforeAction,
			TargetSeat = targetSeat,
			TargetHandIndex = targetHandIndex,
			ReserveReplacement = reserveReplacement,
			Played = played,
			Captured = captured,
			CompletesTurn = completesTurn,
			DeckCollected = deckCollected,
			RemainingAttachmentRewardQueue = queue,
			RemainingAttachmentRewardQueueSeats = queueSeats,
			ContinuationStep = continuationStep,
			PrimaryMutationApplied = primaryMutationApplied,
			TurnAdvanceApplied = turnAdvanceApplied,
			PendingRewardItemId = pendingRewardItemId,
			PendingRewardSeat = pendingRewardSeat,
			PendingRewardHandIndex = pendingRewardHandIndex,
			PendingRewardRequiresAttachment = pendingRewardRequiresAttachment,
			TransferMutationApplied = transferMutationApplied,
			QuestionRewardMutationApplied = questionRewardMutationApplied,
			DiscardMutationApplied = discardMutationApplied,
			AttachmentMutationApplied = attachmentMutationApplied,
			SelectionIndex = selectionIndex,
		};
		if (!AuthorityActionStateRules.IsValid(decoded, playerCount))
		{
			return false;
		}

		state = decoded;
		return true;
	}

	private static bool TryReadInt32(Godot.Collections.Dictionary dictionary, string key, out int value)
	{
		value = 0;
		return dictionary.TryGetValue(key, out Variant rawValue)
			&& rawValue.VariantType == Variant.Type.Int
			&& rawValue.AsInt64() >= int.MinValue
			&& rawValue.AsInt64() <= int.MaxValue
			&& (value = rawValue.AsInt32()) == rawValue.AsInt32();
	}

	private static bool TryReadInt64(Godot.Collections.Dictionary dictionary, string key, out long value)
	{
		value = 0;
		return dictionary.TryGetValue(key, out Variant rawValue)
			&& rawValue.VariantType == Variant.Type.Int
			&& (value = rawValue.AsInt64()) == rawValue.AsInt64();
	}

	private static bool TryReadBool(Godot.Collections.Dictionary dictionary, string key, out bool value)
	{
		value = false;
		return dictionary.TryGetValue(key, out Variant rawValue)
			&& rawValue.VariantType == Variant.Type.Bool
			&& (value = rawValue.AsBool()) == rawValue.AsBool();
	}

	private static bool TryReadString(Godot.Collections.Dictionary dictionary, string key, out string value)
	{
		value = string.Empty;
		if (!dictionary.TryGetValue(key, out Variant rawValue)
			|| rawValue.VariantType != Variant.Type.String)
		{
			return false;
		}

		value = rawValue.AsString();
		return true;
	}

	private static bool TryReadEnum<TEnum>(Godot.Collections.Dictionary dictionary, string key, out TEnum value)
		where TEnum : struct, Enum
	{
		value = default;
		return TryReadString(dictionary, key, out string rawValue)
			&& Enum.TryParse(rawValue, true, out value)
			&& Enum.IsDefined(value);
	}

	private static bool TryReadStringArray(Godot.Collections.Dictionary dictionary, string key, out string[] values)
	{
		values = Array.Empty<string>();
		if (!dictionary.TryGetValue(key, out Variant rawValue)
			|| rawValue.VariantType != Variant.Type.Array)
		{
			return false;
		}

		Godot.Collections.Array array = rawValue.AsGodotArray();
		var decoded = new string[array.Count];
		for (int index = 0; index < array.Count; index++)
		{
			if (array[index].VariantType != Variant.Type.String)
			{
				return false;
			}

			decoded[index] = array[index].AsString();
		}

		values = decoded;
		return true;
	}

	private static bool TryReadInt32Array(Godot.Collections.Dictionary dictionary, string key, out int[] values)
	{
		values = Array.Empty<int>();
		if (!dictionary.TryGetValue(key, out Variant rawValue)
			|| rawValue.VariantType != Variant.Type.Array)
		{
			return false;
		}

		Godot.Collections.Array array = rawValue.AsGodotArray();
		var decoded = new int[array.Count];
		for (int index = 0; index < array.Count; index++)
		{
			if (array[index].VariantType != Variant.Type.Int
				|| array[index].AsInt64() < int.MinValue
				|| array[index].AsInt64() > int.MaxValue)
			{
				return false;
			}

			decoded[index] = array[index].AsInt32();
		}

		values = decoded;
		return true;
	}
}
