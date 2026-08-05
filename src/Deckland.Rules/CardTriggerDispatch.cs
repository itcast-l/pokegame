using System.Collections.Immutable;

namespace Deckland.Rules;

public readonly record struct ItemCardEffectRule(
	string Trigger,
	string EffectType,
	global::CardEffectValues EffectValues,
	int Priority);

public readonly record struct CardEventPosition(float X, float Y);

public sealed record CardGameEvent
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
		IEnumerable<int>? extraTableCardIndexes = null,
		CardEventPosition? originGlobalCenter = null,
		int depth = 0)
	{
		Trigger = trigger ?? string.Empty;
		ActorPlayerIndex = actorPlayerIndex;
		SourceCardId = sourceCardId ?? string.Empty;
		SourceHandIndex = sourceHandIndex;
		SourceTableCardIndex = sourceTableCardIndex;
		TargetPlayerIndex = targetPlayerIndex;
		TargetHandIndex = targetHandIndex;
		CaptureStartIndex = captureStartIndex;
		ExtraTableCardIndexes = extraTableCardIndexes?.ToImmutableArray()
			?? ImmutableArray<int>.Empty;
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
	public ImmutableArray<int> ExtraTableCardIndexes { get; }
	public CardEventPosition? OriginGlobalCenter { get; }
	public int Depth { get; }

	public CardGameEvent CreateFollowUp(
		string trigger,
		string sourceCardId = "",
		int sourceTableCardIndex = -1)
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
	string DefinitionId,
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
	IEnumerable<CardTriggerRequest> CollectCardTriggers(
		CardGameEvent gameEvent,
		int sequenceStart);
}

public sealed partial class AuthorityRuleSession
{
	private readonly CardTriggerDispatchState _cardTriggerDispatch = new();

	public int PendingCardTriggerCount => _cardTriggerDispatch.PendingCount;

	public long BeginCardTriggerDispatch()
	{
		return _cardTriggerDispatch.BeginDispatch();
	}

	public void PublishCardTriggerEvent(
		long dispatchId,
		CardGameEvent gameEvent,
		ICardTriggerProvider provider)
	{
		_cardTriggerDispatch.Publish(dispatchId, gameEvent, provider);
	}

	public bool TryTakeCardTrigger(
		long dispatchId,
		out CardTriggerRequest request)
	{
		return _cardTriggerDispatch.TryDequeue(dispatchId, out request);
	}

	public bool EndCardTriggerDispatch(long dispatchId)
	{
		return _cardTriggerDispatch.EndDispatch(dispatchId);
	}

	public void ClearCardTriggerDispatch()
	{
		_cardTriggerDispatch.Clear();
	}
}

internal sealed class CardTriggerDispatchState
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

	internal int PendingCount => _pending.Count - _pendingStartIndex;

	internal long BeginDispatch()
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

	internal void Clear()
	{
		_activeDispatchId = 0;
		ResetDispatchState();
	}

	internal void Publish(
		long dispatchId,
		CardGameEvent gameEvent,
		ICardTriggerProvider provider)
	{
		ArgumentNullException.ThrowIfNull(gameEvent);
		ArgumentNullException.ThrowIfNull(provider);
		EnsureActiveDispatch(dispatchId);
		CompactPendingIfNeeded();
		if (gameEvent.Depth > MaxChainDepth
			|| PendingCount >= MaxQueuedTriggers)
		{
			return;
		}

		int acceptedRequestCount = 0;
		foreach (CardTriggerRequest request in provider.CollectCardTriggers(
			gameEvent,
			_nextSequence))
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

	internal bool TryDequeue(
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

	internal bool EndDispatch(long dispatchId)
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

		_pending.Sort(
			_pendingStartIndex,
			pendingCount,
			CardTriggerRequestComparer.Instance);
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

		if (_pendingStartIndex < 32
			&& _pendingStartIndex * 2 < _pending.Count)
		{
			return;
		}

		_pending.RemoveRange(0, _pendingStartIndex);
		_pendingStartIndex = 0;
	}

	private static CardTriggerResolutionKey BuildTriggerKey(
		CardTriggerRequest request)
	{
		return new CardTriggerResolutionKey(
			request.DefinitionId,
			request.Rule.Trigger,
			request.Rule.EffectType,
			request.Event.SourceTableCardIndex,
			request.Event.Depth);
	}

	private sealed class CardTriggerRequestComparer
		: IComparer<CardTriggerRequest>
	{
		internal static readonly CardTriggerRequestComparer Instance = new();

		public int Compare(CardTriggerRequest a, CardTriggerRequest b)
		{
			int priorityComparison = b.Rule.Priority.CompareTo(a.Rule.Priority);
			return priorityComparison != 0
				? priorityComparison
				: a.Sequence.CompareTo(b.Sequence);
		}
	}
}
