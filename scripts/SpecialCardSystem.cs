using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

public static class CardGameEvents
{
	public const string OnPlay = "on_play";
	public const string OnMatch = "on_match";
	public const string OnCollect = "on_collect";
	public const string OnMiss = "on_miss";
	public const string OnRoundStart = "on_round_start";
	public const string OnRoundEnd = "on_round_end";
	public const string OnDraw = "on_draw";
	public const string OnShuffle = "on_shuffle";
	public const string OnDiscard = "on_discard";
	public const string OnEmptyDeck = "on_empty_deck";
}

public static class CardEffectTypes
{
	public const string None = "";
	public const string PlaceTablePairDestroy = "place_table_pair_destroy";
	public const string DrawCard = "draw_card";
	public const string StealCard = "steal_card";
	public const string StealDeckCards = "steal_deck_cards";
	public const string CollectTable = "collect_table";
	public const string GrantReward = "grant_reward";
	public const string AttachModifier = "attach_modifier";
	public const string RevealCards = "reveal_cards";
	public const string MagnetExtraCapture = "magnet_extra_capture";
	public const string ShuffleTable = "shuffle_table";
	public const string DoubleCollect = "double_collect";
	public const string ReverseCollect = "reverse_collect";
	public const string DestroyCards = "destroy_cards";
	public const string ExtraTurn = "extra_turn";
	public const string EndpointCrown = "endpoint_crown";
	public const string BorrowTide = "borrow_tide";
	public const string ReverseAnchor = "reverse_anchor";
	public const string MidTideUpgrade = "mid_tide_upgrade";
	public const string ReverseFlowAnchor = "reverse_flow_anchor";
	public const string EmptyTideContinuation = "empty_tide_continuation";
	public const string SuitCollect = "suit_collect";
	public const string BlockMatch = "block_match";
}

public static class CardEffectValueKeys
{
	public const string Modifier = "modifier";
	public const string Target = "target";
	public const string Count = "count";
	public const string PairTag = "pair_tag";
	public const string Multiplier = "multiplier";
}

public static class CardEffectTargets
{
	public const string PlayerHand = "player_hand";
	public const string PlayerDeck = "player_deck";
	public const string Table = "table";
	public const string OpponentTableFaceDown = "opponent_table_face_down";
}

public static class CardPlayPresentationTypes
{
	public const string DirectTrigger = "direct_trigger";
	public const string TableOverlay = "table_overlay";
	public const string TransformAreaTrigger = "transform_area_trigger";
	public const string LensSplit = "lens_split";
}

public static class CardTags
{
	public const string Fixed = "fixed";
	public const string Bomb = "bomb";
	public const string Reward = "reward";
	public const string Attachment = "attachment";
}

public sealed class CardEffectValues
{
	public static readonly CardEffectValues Empty = new(new Dictionary<string, string>(StringComparer.Ordinal));

	private readonly Dictionary<string, string> _values;

	private CardEffectValues(Dictionary<string, string> values)
	{
		_values = values;
	}

	public bool IsEmpty => _values.Count == 0;

	public IReadOnlyDictionary<string, string> All => _values;

	public static CardEffectValues Parse(string rawValue)
	{
		return TryParse(rawValue, out CardEffectValues values)
			? values
			: Empty;
	}

	public static bool TryParse(string rawValue, out CardEffectValues values)
	{
		if (string.IsNullOrWhiteSpace(rawValue))
		{
			values = Empty;
			return true;
		}

		string raw = rawValue.Trim();
		if (raw.StartsWith("{", StringComparison.Ordinal))
		{
			return TryParseJsonObject(raw, out values);
		}

		var parsedValues = new Dictionary<string, string>(StringComparer.Ordinal);
		string[] pairs = raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		foreach (string pair in pairs)
		{
			int separatorIndex = pair.IndexOf('=');
			if (separatorIndex <= 0 || separatorIndex >= pair.Length - 1)
			{
				values = Empty;
				return false;
			}

			string key = pair[..separatorIndex].Trim();
			string value = pair[(separatorIndex + 1)..].Trim();
			if (key.Length == 0 || value.Length == 0)
			{
				values = Empty;
				return false;
			}

			parsedValues[key] = value;
		}

		values = parsedValues.Count == 0 ? Empty : new CardEffectValues(parsedValues);
		return true;
	}

	public string GetString(string key, string fallback = "")
	{
		return _values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
			? value
			: fallback;
	}

	public int GetInt(string key, int fallback = 0)
	{
		return _values.TryGetValue(key, out string? value) && int.TryParse(value, out int parsed)
			? parsed
			: fallback;
	}

	public bool GetBool(string key, bool fallback = false)
	{
		return _values.TryGetValue(key, out string? value) && bool.TryParse(value, out bool parsed)
			? parsed
			: fallback;
	}

	public CardEffectValues WithOverrides(CardEffectValues overrides)
	{
		if (overrides.IsEmpty)
		{
			return this;
		}

		var merged = new Dictionary<string, string>(_values, StringComparer.Ordinal);
		foreach (KeyValuePair<string, string> pair in overrides._values)
		{
			merged[pair.Key] = pair.Value;
		}

		return new CardEffectValues(merged);
	}

	private static bool TryParseJsonObject(string raw, out CardEffectValues values)
	{
		try
		{
			using JsonDocument document = JsonDocument.Parse(raw);
			if (document.RootElement.ValueKind != JsonValueKind.Object)
			{
				values = Empty;
				return false;
			}

			var parsedValues = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (JsonProperty property in document.RootElement.EnumerateObject())
			{
				if (property.Value.ValueKind == JsonValueKind.Array || property.Value.ValueKind == JsonValueKind.Object)
				{
					values = Empty;
					return false;
				}

				parsedValues[property.Name] = property.Value.ValueKind == JsonValueKind.String
					? property.Value.GetString() ?? string.Empty
					: property.Value.ToString();
				if (parsedValues[property.Name].Length == 0)
				{
					values = Empty;
					return false;
				}
			}

			values = parsedValues.Count == 0 ? Empty : new CardEffectValues(parsedValues);
			return true;
		}
		catch (JsonException)
		{
			values = Empty;
			return false;
		}
	}
}

public readonly record struct ItemCardEffectRule(
	string Trigger,
	string EffectType,
	CardEffectValues EffectValues,
	int Priority);

public sealed class CardGameEvent
{
	public CardGameEvent(
		string trigger,
		int actorPlayerIndex = -1,
		string sourceCardId = "",
		int sourceHandIndex = -1,
		int sourceTableCardIndex = -1,
		int targetPlayerIndex = -1,
		int targetHandIndex = -1,
		int captureStartIndex = -1,
		IReadOnlyCollection<int>? extraTableCardIndexes = null,
		Vector2? originGlobalCenter = null,
		int depth = 0)
	{
		Trigger = trigger;
		ActorPlayerIndex = actorPlayerIndex;
		SourceCardId = sourceCardId;
		SourceHandIndex = sourceHandIndex;
		SourceTableCardIndex = sourceTableCardIndex;
		TargetPlayerIndex = targetPlayerIndex;
		TargetHandIndex = targetHandIndex;
		CaptureStartIndex = captureStartIndex;
		ExtraTableCardIndexes = extraTableCardIndexes ?? Array.Empty<int>();
		OriginGlobalCenter = originGlobalCenter;
		Depth = depth;
	}

	public string Trigger { get; }
	public int ActorPlayerIndex { get; }
	public string SourceCardId { get; }
	public int SourceHandIndex { get; }
	public int SourceTableCardIndex { get; }
	public int TargetPlayerIndex { get; }
	public int TargetHandIndex { get; }
	public int CaptureStartIndex { get; }
	public IReadOnlyCollection<int> ExtraTableCardIndexes { get; }
	public Vector2? OriginGlobalCenter { get; }
	public int Depth { get; }

	public CardGameEvent CreateFollowUp(string trigger, string sourceCardId = "", int sourceTableCardIndex = -1)
	{
		return new CardGameEvent(
			trigger,
			ActorPlayerIndex,
			sourceCardId,
			SourceHandIndex,
			sourceTableCardIndex,
			TargetPlayerIndex,
			TargetHandIndex,
			CaptureStartIndex,
			ExtraTableCardIndexes,
			OriginGlobalCenter,
			Depth + 1);
	}
}

public readonly record struct CardTriggerRequest(
	ItemCardDefinition Definition,
	ItemCardEffectRule Rule,
	CardGameEvent Event,
	int Sequence);

public readonly record struct CardTriggerResolutionKey(
	string DefinitionId,
	string Trigger,
	string EffectType,
	int SourceTableCardIndex,
	int ScopeValue);

public interface ICardTriggerProvider
{
	IEnumerable<CardTriggerRequest> CollectCardTriggers(CardGameEvent gameEvent, int sequenceStart);
}

public sealed class CardEventBus
{
	private const int MaxQueuedTriggers = 128;
	private const int MaxChainDepth = 16;
	private const int MaxSameTriggerResolutions = 4;

	private readonly List<CardTriggerRequest> _pending = new();
	private readonly Dictionary<CardTriggerResolutionKey, int> _resolvedCounts = new();
	private int _pendingStartIndex;
	private int _nextSequence;
	private long _nextDispatchId = 1;
	private long _activeDispatchId;

	public int PendingCount => _pending.Count - _pendingStartIndex;

	public long BeginDispatch()
	{
		if (_activeDispatchId != 0)
		{
			throw new InvalidOperationException(
				"Cannot begin a card event dispatch while another dispatch is active.");
		}

		ResetDispatchState();
		long dispatchId = _nextDispatchId;
		_nextDispatchId = _nextDispatchId == long.MaxValue
			? 1
			: _nextDispatchId + 1;
		_activeDispatchId = dispatchId;
		return dispatchId;
	}

	public void Clear()
	{
		_activeDispatchId = 0;
		ResetDispatchState();
	}

	public void Publish(
		long dispatchId,
		CardGameEvent gameEvent,
		ICardTriggerProvider provider)
	{
		EnsureActiveDispatch(dispatchId);
		CompactPendingIfNeeded();
		if (gameEvent.Depth > MaxChainDepth
			|| PendingCount >= MaxQueuedTriggers)
		{
			return;
		}

		int acceptedRequestCount = 0;
		foreach (CardTriggerRequest request in provider.CollectCardTriggers(gameEvent, _nextSequence))
		{
			EnsureActiveDispatch(dispatchId);
			if (string.IsNullOrWhiteSpace(request.Rule.Trigger)
				|| string.IsNullOrWhiteSpace(request.Rule.EffectType))
			{
				continue;
			}

			acceptedRequestCount++;
			if (PendingCount < MaxQueuedTriggers)
			{
				_pending.Add(request);
			}
		}

		EnsureActiveDispatch(dispatchId);
		_nextSequence += acceptedRequestCount;
		SortPending();
	}

	public bool TryDequeue(
		long dispatchId,
		out CardTriggerRequest request)
	{
		EnsureActiveDispatch(dispatchId);
		while (PendingCount > 0)
		{
			request = _pending[_pendingStartIndex++];
			CardTriggerResolutionKey key = BuildTriggerKey(request);
			_resolvedCounts.TryGetValue(key, out int resolvedCount);
			CompactPendingIfNeeded();
			if (resolvedCount >= MaxSameTriggerResolutions)
			{
				continue;
			}

			_resolvedCounts[key] = resolvedCount + 1;
			return true;
		}

		request = default;
		return false;
	}

	public bool EndDispatch(long dispatchId)
	{
		if (dispatchId == 0 || dispatchId != _activeDispatchId)
		{
			return false;
		}

		_activeDispatchId = 0;
		ResetDispatchState();
		return true;
	}

	private void ResetDispatchState()
	{
		_pending.Clear();
		_resolvedCounts.Clear();
		_pendingStartIndex = 0;
		_nextSequence = 0;
	}

	private void EnsureActiveDispatch(long dispatchId)
	{
		if (dispatchId == 0 || dispatchId != _activeDispatchId)
		{
			throw new InvalidOperationException(
				"Card event dispatch ownership is no longer active.");
		}
	}

	private void SortPending()
	{
		int pendingCount = PendingCount;
		if (pendingCount <= 1)
		{
			return;
		}

		_pending.Sort(_pendingStartIndex, pendingCount, CardTriggerRequestComparer.Instance);
	}

	private void CompactPendingIfNeeded()
	{
		if (_pendingStartIndex == 0)
		{
			return;
		}

		if (_pendingStartIndex >= _pending.Count)
		{
			_pending.Clear();
			_pendingStartIndex = 0;
			return;
		}

		if (_pendingStartIndex < 32 && _pendingStartIndex * 2 < _pending.Count)
		{
			return;
		}

		_pending.RemoveRange(0, _pendingStartIndex);
		_pendingStartIndex = 0;
	}

	private static CardTriggerResolutionKey BuildTriggerKey(CardTriggerRequest request)
	{
		return new CardTriggerResolutionKey(
			request.Definition.Id,
			request.Rule.Trigger,
			request.Rule.EffectType,
			request.Event.SourceTableCardIndex,
			request.Event.Depth);
	}

	private sealed class CardTriggerRequestComparer : IComparer<CardTriggerRequest>
	{
		public static readonly CardTriggerRequestComparer Instance = new();

		public int Compare(CardTriggerRequest a, CardTriggerRequest b)
		{
			int priorityComparison = b.Rule.Priority.CompareTo(a.Rule.Priority);
			return priorityComparison != 0 ? priorityComparison : a.Sequence.CompareTo(b.Sequence);
		}
	}
}
