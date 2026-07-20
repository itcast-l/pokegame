using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

public enum AuthorityActionKind
{
	None,
	BaseCard,
	ItemCard,
	QuestionReward,
	CardTransfer,
}

public enum AuthorityActionPhase
{
	Prepared,
	Applying,
	WaitingDiscard,
	WaitingAttachment,
	Finalizing,
}

public enum AuthorityActionContinuationStep
{
	PrimaryMutation,
	QuestionRewardDelivery,
	CardTransfer,
	AttachmentQueue,
	TurnAdvancement,
	Complete,
}

public enum ClientActionAcceptance
{
	Accepted,
	DuplicateOrOlder,
	Invalid,
}

public enum AuthorityActionMutationKind
{
	Primary,
	QuestionReward,
	CardTransfer,
	Discard,
	Attachment,
}

public sealed record AuthorityActionState
{
	[JsonPropertyName("action_id")]
	[JsonPropertyOrder(0)]
	public long ActionId { get; init; }

	[JsonPropertyName("client_action_id")]
	[JsonPropertyOrder(1)]
	public long ClientActionId { get; init; }

	[JsonPropertyName("kind")]
	[JsonPropertyOrder(2)]
	public AuthorityActionKind Kind { get; init; }

	[JsonPropertyName("phase")]
	[JsonPropertyOrder(3)]
	public AuthorityActionPhase Phase { get; init; }

	[JsonPropertyName("actor_seat")]
	[JsonPropertyOrder(4)]
	public int ActorSeat { get; init; }

	[JsonPropertyName("turn_generation")]
	[JsonPropertyOrder(5)]
	public int TurnGeneration { get; init; }

	[JsonPropertyName("base_hand_index")]
	[JsonPropertyOrder(6)]
	public int BaseHandIndex { get; init; } = -1;

	[JsonPropertyName("item_hand_index")]
	[JsonPropertyOrder(7)]
	public int ItemHandIndex { get; init; } = -1;

	[JsonPropertyName("item_or_reward_id")]
	[JsonPropertyOrder(8)]
	public string ItemOrRewardId { get; init; } = string.Empty;

	[JsonPropertyName("target_seat")]
	[JsonPropertyOrder(9)]
	public int TargetSeat { get; init; } = -1;

	[JsonPropertyName("target_hand_index")]
	[JsonPropertyOrder(10)]
	public int TargetHandIndex { get; init; } = -1;

	[JsonPropertyName("reserve_replacement")]
	[JsonPropertyOrder(11)]
	public bool ReserveReplacement { get; init; }

	[JsonPropertyName("played")]
	[JsonPropertyOrder(12)]
	public bool Played { get; init; }

	[JsonPropertyName("captured")]
	[JsonPropertyOrder(13)]
	public bool Captured { get; init; }

	[JsonPropertyName("completes_turn")]
	[JsonPropertyOrder(14)]
	public bool CompletesTurn { get; init; }

	[JsonPropertyName("deck_collected")]
	[JsonPropertyOrder(15)]
	public bool DeckCollected { get; init; }

	[JsonPropertyName("remaining_attachment_reward_queue")]
	[JsonPropertyOrder(16)]
	public string[] RemainingAttachmentRewardQueue { get; init; } = Array.Empty<string>();

	[JsonPropertyName("remaining_attachment_reward_queue_seats")]
	[JsonPropertyOrder(17)]
	public int[] RemainingAttachmentRewardQueueSeats { get; init; } = Array.Empty<int>();

	[JsonPropertyName("continuation_step")]
	[JsonPropertyOrder(18)]
	public AuthorityActionContinuationStep ContinuationStep { get; init; }

	[JsonPropertyName("primary_mutation_applied")]
	[JsonPropertyOrder(19)]
	public bool PrimaryMutationApplied { get; init; }

	[JsonPropertyName("turn_advance_applied")]
	[JsonPropertyOrder(20)]
	public bool TurnAdvanceApplied { get; init; }

	[JsonPropertyName("pending_reward_item_id")]
	[JsonPropertyOrder(21)]
	public string PendingRewardItemId { get; init; } = string.Empty;

	[JsonPropertyName("pending_reward_seat")]
	[JsonPropertyOrder(22)]
	public int PendingRewardSeat { get; init; } = -1;

	[JsonPropertyName("pending_reward_hand_index")]
	[JsonPropertyOrder(23)]
	public int PendingRewardHandIndex { get; init; } = -1;

	[JsonPropertyName("pending_reward_requires_attachment")]
	[JsonPropertyOrder(24)]
	public bool PendingRewardRequiresAttachment { get; init; }

	[JsonPropertyName("transfer_mutation_applied")]
	[JsonPropertyOrder(25)]
	public bool TransferMutationApplied { get; init; }

	[JsonPropertyName("question_reward_mutation_applied")]
	[JsonPropertyOrder(26)]
	public bool QuestionRewardMutationApplied { get; init; }

	[JsonPropertyName("discard_mutation_applied")]
	[JsonPropertyOrder(27)]
	public bool DiscardMutationApplied { get; init; }

	[JsonPropertyName("attachment_mutation_applied")]
	[JsonPropertyOrder(28)]
	public bool AttachmentMutationApplied { get; init; }

	[JsonPropertyName("selection_index")]
	[JsonPropertyOrder(29)]
	public int SelectionIndex { get; init; } = -1;

	[JsonPropertyName("base_card_code")]
	[JsonPropertyOrder(30)]
	public string BaseCardCode { get; init; } = string.Empty;

	[JsonPropertyName("rng_state_before_action")]
	[JsonPropertyOrder(31)]
	public string RngStateBeforeAction { get; init; } = string.Empty;

	[JsonIgnore]
	public bool IsActive => Kind != AuthorityActionKind.None && ActionId > 0;

	public static AuthorityActionState Empty => new();

	public AuthorityActionState WithPhase(AuthorityActionPhase phase)
	{
		return this with { Phase = phase };
	}

	public AuthorityActionState WithSelection(
		AuthorityActionPhase phase,
		int selectionIndex = -1)
	{
		return this with
		{
			Phase = phase,
			SelectionIndex = selectionIndex,
		};
	}

	public AuthorityActionState WithMutationResult(
		bool played,
		bool captured,
		bool completesTurn,
		bool deckCollected)
	{
		return this with
		{
			Played = played,
			Captured = captured,
			CompletesTurn = completesTurn,
			DeckCollected = deckCollected,
			Phase = AuthorityActionPhase.Finalizing,
		};
	}

	public AuthorityActionState WithRemainingAttachmentRewardQueue(IEnumerable<string> itemCardIds)
	{
		return this with
		{
			RemainingAttachmentRewardQueue = itemCardIds
				.Where(itemCardId => !string.IsNullOrWhiteSpace(itemCardId))
				.Select(itemCardId => itemCardId.Trim())
				.ToArray(),
		};
	}
}

public static class AuthorityActionStateRules
{
	public static bool IsKnownKind(AuthorityActionKind kind)
	{
		return Enum.IsDefined(kind) && kind != AuthorityActionKind.None;
	}

	public static bool IsKnownPhase(AuthorityActionPhase phase)
	{
		return Enum.IsDefined(phase);
	}

	public static bool IsValid(AuthorityActionState? state, int playerCount = 4)
	{
		if (playerCount < 2 || playerCount > NetworkProtocol.MaxAuthorityCheckpointPlayers)
		{
			return false;
		}

		if (state is null)
		{
			return true;
		}

		if (state.RemainingAttachmentRewardQueue is null
			|| state.RemainingAttachmentRewardQueueSeats is null
			|| state.ItemOrRewardId is null
			|| state.PendingRewardItemId is null
			|| state.BaseCardCode is null
			|| state.RngStateBeforeAction is null)
		{
			return false;
		}

		if (state.Kind == AuthorityActionKind.None)
		{
			return state.ActionId == 0
				&& state.ClientActionId == 0
				&& state.ActorSeat == 0
				&& state.TurnGeneration == 0
				&& state.BaseHandIndex == -1
				&& state.ItemHandIndex == -1
					&& state.TargetSeat == -1
					&& state.TargetHandIndex == -1
					&& string.IsNullOrEmpty(state.ItemOrRewardId)
					&& state.Phase == AuthorityActionPhase.Prepared
					&& !state.ReserveReplacement
					&& !state.Played
					&& !state.Captured
					&& !state.CompletesTurn
					&& !state.DeckCollected
					&& state.RemainingAttachmentRewardQueue.Length == 0
					&& state.RemainingAttachmentRewardQueueSeats.Length == 0
					&& state.ContinuationStep == AuthorityActionContinuationStep.PrimaryMutation
					&& !state.PrimaryMutationApplied
					&& !state.TurnAdvanceApplied
					&& string.IsNullOrEmpty(state.PendingRewardItemId)
					&& state.PendingRewardSeat == -1
					&& state.PendingRewardHandIndex == -1
					&& !state.PendingRewardRequiresAttachment
					&& !state.TransferMutationApplied
					&& !state.QuestionRewardMutationApplied
					&& !state.DiscardMutationApplied
					&& !state.AttachmentMutationApplied
					&& state.SelectionIndex == -1
					&& string.IsNullOrEmpty(state.BaseCardCode)
					&& string.IsNullOrEmpty(state.RngStateBeforeAction);
		}

		if (!IsKnownKind(state.Kind)
			|| !IsKnownPhase(state.Phase)
			|| state.ActionId <= 0
			|| state.ClientActionId < 0
			|| state.ActorSeat < 0
			|| state.ActorSeat >= playerCount
			|| state.TurnGeneration < 0
			|| state.BaseHandIndex < -1
			|| state.ItemHandIndex < -1
			|| state.TargetSeat < -1
			|| state.TargetHandIndex < -1
			|| state.SelectionIndex < -1
			|| state.ItemOrRewardId.Length > 128
			|| state.BaseCardCode.Length > 32
			|| state.RngStateBeforeAction.Length > 32
			|| state.RemainingAttachmentRewardQueue.Length > 64
			|| state.RemainingAttachmentRewardQueueSeats.Length > 64
			|| !Enum.IsDefined(state.ContinuationStep)
			|| state.PendingRewardItemId.Length > 128
			|| state.PendingRewardSeat < -1
			|| state.PendingRewardSeat >= playerCount
			|| state.PendingRewardHandIndex < -1
			|| (state.Captured && !state.Played)
			|| (state.DeckCollected && !state.Captured)
			|| (state.TransferMutationApplied && !state.PrimaryMutationApplied)
			|| (state.QuestionRewardMutationApplied && state.Kind != AuthorityActionKind.QuestionReward)
			|| (state.TurnAdvanceApplied && state.Phase != AuthorityActionPhase.Finalizing))
		{
			return false;
		}

		if (state.TargetSeat >= playerCount)
		{
			return false;
		}

		if (state.RemainingAttachmentRewardQueueSeats.Length != 0
			&& state.RemainingAttachmentRewardQueue.Length != state.RemainingAttachmentRewardQueueSeats.Length)
		{
			return false;
		}

		if (!state.RemainingAttachmentRewardQueue.All(
			itemCardId => !string.IsNullOrWhiteSpace(itemCardId) && itemCardId.Length <= 128)
			|| !state.RemainingAttachmentRewardQueueSeats.All(seat => seat >= 0 && seat < playerCount))
		{
			return false;
		}

		if (string.IsNullOrEmpty(state.PendingRewardItemId))
		{
			if (state.PendingRewardSeat != -1
				|| state.PendingRewardHandIndex != -1
				|| state.PendingRewardRequiresAttachment)
			{
				return false;
			}
		}
		else if (state.PendingRewardSeat < 0
			|| (state.PendingRewardRequiresAttachment && state.PendingRewardHandIndex != -1))
		{
			return false;
		}

		if (state.Phase == AuthorityActionPhase.WaitingDiscard
			&& (state.Kind is not (AuthorityActionKind.QuestionReward or AuthorityActionKind.CardTransfer)
				|| state.ContinuationStep is not (AuthorityActionContinuationStep.QuestionRewardDelivery or AuthorityActionContinuationStep.CardTransfer)))
		{
			return false;
		}

		if (state.Phase == AuthorityActionPhase.Prepared
			&& (state.Played
				|| state.Captured
				|| state.CompletesTurn
				|| state.DeckCollected
				|| state.PrimaryMutationApplied
				|| state.TransferMutationApplied
				|| state.QuestionRewardMutationApplied
				|| state.DiscardMutationApplied
				|| state.AttachmentMutationApplied
				|| state.TurnAdvanceApplied
				|| state.ContinuationStep != AuthorityActionContinuationStep.PrimaryMutation
				|| string.IsNullOrWhiteSpace(state.RngStateBeforeAction)
				|| !ulong.TryParse(state.RngStateBeforeAction, out _)))
		{
			return false;
		}

		if (state.Phase == AuthorityActionPhase.WaitingAttachment
			&& (state.Kind is not (AuthorityActionKind.QuestionReward or AuthorityActionKind.BaseCard or AuthorityActionKind.ItemCard)
				|| string.IsNullOrWhiteSpace(state.PendingRewardItemId)
				|| state.ContinuationStep != AuthorityActionContinuationStep.AttachmentQueue))
		{
			return false;
		}

		if (state.Phase is AuthorityActionPhase.Applying or AuthorityActionPhase.Finalizing)
		{
			if (state.Kind != AuthorityActionKind.CardTransfer
				&& (state.TargetSeat != -1 || state.TargetHandIndex != -1))
			{
				return false;
			}
		}
		if (state.Phase is not (AuthorityActionPhase.WaitingDiscard or AuthorityActionPhase.WaitingAttachment)
			&& state.SelectionIndex != -1)
		{
			return false;
		}

		if (state.Kind == AuthorityActionKind.BaseCard
			&& (state.BaseHandIndex < 0
				|| state.ItemHandIndex != -1
				|| !string.IsNullOrEmpty(state.ItemOrRewardId)
				|| (state.Phase == AuthorityActionPhase.Prepared
					&& string.IsNullOrWhiteSpace(state.BaseCardCode))))
		{
			return false;
		}

		if (state.Kind is AuthorityActionKind.ItemCard or AuthorityActionKind.CardTransfer
			&& (state.BaseHandIndex != -1
				|| state.ItemHandIndex < 0
				|| string.IsNullOrWhiteSpace(state.ItemOrRewardId)))
		{
			return false;
		}
		if (state.Kind == AuthorityActionKind.CardTransfer
			&& (state.TargetSeat < 0 || state.TargetSeat == state.ActorSeat))
		{
			return false;
		}

		if (state.Kind == AuthorityActionKind.QuestionReward
			&& (state.BaseHandIndex != -1
				|| state.ItemHandIndex != -1
				|| string.IsNullOrWhiteSpace(state.ItemOrRewardId)))
		{
			return false;
		}

		if (state.TransferMutationApplied && state.Kind != AuthorityActionKind.CardTransfer)
		{
			return false;
		}

		return true;
	}

	public static bool IsStableCheckpointState(AuthorityActionState state)
	{
		ArgumentNullException.ThrowIfNull(state);
		return !state.IsActive
			|| state.Phase is AuthorityActionPhase.Prepared
				or AuthorityActionPhase.WaitingDiscard
				or AuthorityActionPhase.WaitingAttachment
				or AuthorityActionPhase.Finalizing;
	}

	public static bool IsMutationApplied(
		AuthorityActionState state,
		AuthorityActionMutationKind mutation)
	{
		ArgumentNullException.ThrowIfNull(state);
		return mutation switch
		{
			AuthorityActionMutationKind.Primary => state.PrimaryMutationApplied,
			AuthorityActionMutationKind.QuestionReward => state.QuestionRewardMutationApplied,
			AuthorityActionMutationKind.CardTransfer => state.TransferMutationApplied,
			AuthorityActionMutationKind.Discard => state.DiscardMutationApplied,
			AuthorityActionMutationKind.Attachment => state.AttachmentMutationApplied,
			_ => false,
		};
	}

	public static bool ShouldApplyMutation(
		AuthorityActionState state,
		AuthorityActionMutationKind mutation)
	{
		return !IsMutationApplied(state, mutation);
	}

	public static AuthorityActionState MarkMutationApplied(
		AuthorityActionState state,
		AuthorityActionMutationKind mutation)
	{
		ArgumentNullException.ThrowIfNull(state);
		return mutation switch
		{
			AuthorityActionMutationKind.Primary => state with { PrimaryMutationApplied = true, Played = true },
			AuthorityActionMutationKind.QuestionReward => state with { QuestionRewardMutationApplied = true },
			AuthorityActionMutationKind.CardTransfer => state with
			{
				PrimaryMutationApplied = true,
				TransferMutationApplied = true,
			},
			AuthorityActionMutationKind.Discard => state with { DiscardMutationApplied = true },
			AuthorityActionMutationKind.Attachment => state with { AttachmentMutationApplied = true },
			_ => state,
		};
	}

	public static AuthorityActionState AdvanceAfterDiscardMutation(AuthorityActionState state)
	{
		ArgumentNullException.ThrowIfNull(state);
		if (!state.IsActive)
		{
			return state;
		}

		AuthorityActionState marked = MarkMutationApplied(
			state,
			AuthorityActionMutationKind.Discard);
		return marked with
		{
			Phase = state.Kind is AuthorityActionKind.QuestionReward or AuthorityActionKind.CardTransfer
					? AuthorityActionPhase.Applying
					: AuthorityActionPhase.Finalizing,
			ContinuationStep = state.Kind == AuthorityActionKind.CardTransfer
				? AuthorityActionContinuationStep.CardTransfer
				: AuthorityActionContinuationStep.QuestionRewardDelivery,
			SelectionIndex = -1,
		};
	}

	public static bool CanResumeFinalizing(AuthorityActionState state)
	{
		ArgumentNullException.ThrowIfNull(state);
		return state.IsActive
			&& state.Phase == AuthorityActionPhase.Finalizing;
	}

	public static bool CanAdvancePhase(AuthorityActionPhase current, AuthorityActionPhase next)
	{
		if (!IsKnownPhase(current) || !IsKnownPhase(next))
		{
			return false;
		}

		if (current == next)
		{
			return true;
		}

		return (current, next) switch
		{
			(AuthorityActionPhase.Prepared, AuthorityActionPhase.Applying) => true,
			(AuthorityActionPhase.Applying, AuthorityActionPhase.WaitingDiscard) => true,
			(AuthorityActionPhase.Applying, AuthorityActionPhase.WaitingAttachment) => true,
			(AuthorityActionPhase.Applying, AuthorityActionPhase.Finalizing) => true,
			(AuthorityActionPhase.WaitingDiscard, AuthorityActionPhase.Applying) => true,
			(AuthorityActionPhase.WaitingDiscard, AuthorityActionPhase.WaitingAttachment) => true,
			(AuthorityActionPhase.WaitingDiscard, AuthorityActionPhase.Finalizing) => true,
			(AuthorityActionPhase.WaitingAttachment, AuthorityActionPhase.Applying) => true,
			(AuthorityActionPhase.WaitingAttachment, AuthorityActionPhase.Finalizing) => true,
			_ => false,
		};
	}

	public static bool CanCommitPendingSelection(
		AuthorityActionState? state,
		AuthorityActionPhase expectedPhase,
		int actorSeat,
		int turnGeneration)
	{
		return state is not null
			&& state.IsActive
			&& expectedPhase is AuthorityActionPhase.WaitingDiscard or AuthorityActionPhase.WaitingAttachment
			&& state.Phase == expectedPhase
			&& state.SelectionIndex == -1
			&& state.ActorSeat == actorSeat
			&& state.TurnGeneration == turnGeneration;
	}

	public static bool HasCommittedSelection(AuthorityActionState state)
	{
		ArgumentNullException.ThrowIfNull(state);
		return state.Phase switch
		{
			AuthorityActionPhase.WaitingDiscard => state.SelectionIndex >= 0,
			AuthorityActionPhase.WaitingAttachment => state.SelectionIndex >= 0,
			_ => false,
		};
	}

	public static ClientActionAcceptance ClassifyClientAction(long clientActionId, long lastAcceptedClientActionId)
	{
		if (clientActionId < 0 || lastAcceptedClientActionId < 0)
		{
			return ClientActionAcceptance.Invalid;
		}

		if (clientActionId == 0)
		{
			return lastAcceptedClientActionId == 0
				? ClientActionAcceptance.Accepted
				: ClientActionAcceptance.DuplicateOrOlder;
		}

		return clientActionId <= lastAcceptedClientActionId
			? ClientActionAcceptance.DuplicateOrOlder
			: ClientActionAcceptance.Accepted;
	}

	public static bool TryAcceptClientAction(
		long clientActionId,
		long lastAcceptedClientActionId,
		out long nextLastAcceptedClientActionId)
	{
		ClientActionAcceptance result = ClassifyClientAction(clientActionId, lastAcceptedClientActionId);
		if (result != ClientActionAcceptance.Accepted)
		{
			nextLastAcceptedClientActionId = lastAcceptedClientActionId;
			return false;
		}

		nextLastAcceptedClientActionId = Math.Max(lastAcceptedClientActionId, clientActionId);
		return true;
	}

	public static string Serialize(AuthorityActionState state)
	{
		ArgumentNullException.ThrowIfNull(state);
		return JsonSerializer.Serialize(state);
	}

	public static bool TryDeserialize(string json, out AuthorityActionState state, int playerCount = 4)
	{
		state = AuthorityActionState.Empty;
		if (string.IsNullOrWhiteSpace(json))
		{
			return false;
		}

		try
		{
			AuthorityActionState? decoded = JsonSerializer.Deserialize<AuthorityActionState>(json);
			if (!IsValid(decoded, playerCount))
			{
				return false;
			}

			state = NormalizeLegacyQueueSeats(decoded!);
			return true;
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static AuthorityActionState NormalizeLegacyQueueSeats(AuthorityActionState state)
	{
		if (state.RemainingAttachmentRewardQueue.Length == 0
			|| state.RemainingAttachmentRewardQueueSeats.Length > 0)
		{
			return state;
		}

		int fallbackSeat = Math.Max(0, state.ActorSeat);
		return state with
		{
			RemainingAttachmentRewardQueueSeats = state.RemainingAttachmentRewardQueue
				.Select(_ => fallbackSeat)
				.ToArray(),
		};
	}
}
