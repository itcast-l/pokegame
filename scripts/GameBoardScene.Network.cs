using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private readonly Dictionary<string, PlayingCard> _networkCardsByCode = new(StringComparer.Ordinal);
	private readonly Queue<Godot.Collections.Dictionary> _networkVisualEventQueue = new();
	private Godot.Collections.Dictionary? _networkQueuedSnapshot;
	private bool _networkVisualQueueProcessing;
	private int _networkSnapshotId;
	private int _networkAuthoritativeVisualSnapshotId = -1;
	private int _networkLastAppliedSnapshotId;
	private int _networkPendingHandIndex = -1;
	private int _networkPendingItemHandIndex = -1;
	private int _networkPendingTurnSequenceGeneration = -1;
	private bool _networkPendingSelectionRequestInFlight;
	private bool _networkAiProxyTurnRunning;
	private bool _networkPredictedHandVisualActive;
	private int _networkPredictedHandTableCardIndex = -1;
	private int _networkPredictedHandMatchedTableCardIndex = -1;
	private bool _networkPredictedHandMatchedTableCardFlipped;
	private string _networkPredictedHandBorrowTideAdjustedRank = string.Empty;
	private bool _networkPredictedItemVisualActive;
	private int _networkPredictedItemDisplayIndex = -1;
	private string _networkPredictedItemCardId = string.Empty;
	private string _networkPredictedItemPlayPresentation = string.Empty;
	private int _networkPredictedItemTargetTableCardIndex = -1;
	private bool _networkLocallyAdvancedPendingTargetSelection;
	private PendingItemSelectionMode _networkLocallyCommittedPendingSelectionMode = PendingItemSelectionMode.None;
	private int _networkLocallyCommittedPendingSelectionDisplayIndex = -1;
	private int _networkLocallyCommittedPendingSelectionHandIndex = -1;
	private string _networkLocallyCommittedPendingSelectionItemCardId = string.Empty;
	private PlayingCard _networkLocallyCommittedPendingSelectionOriginalCard;

	internal IPlayerInputHandler CreatePlayerInputHandler()
	{
		if (!IsNetworkMultiplayerMode())
		{
			return new LocalPlayerInputHandler(this);
		}

		return IsNetworkHost()
			? new NetworkHostInputHandler(this)
			: new NetworkPlayerInputHandler(this);
	}

	internal void ConfirmLocalHandCardPlay(int handIndex, int turnSequenceGeneration, Vector2? startGlobalPosition)
	{
		_ = PlayMainPlayerCardAndAdvanceAsync(handIndex, turnSequenceGeneration, startGlobalPosition);
	}

	internal async void RequestNetworkItemCardPlay(int itemHandIndex, int turnSequenceGeneration)
	{
		if (!IsNetworkMultiplayerMode() || IsNetworkHost())
		{
			ConfirmNetworkHostItemCardPlay(itemHandIndex, turnSequenceGeneration);
			return;
		}

		if (_networkPendingHandIndex >= 0 || _networkPendingItemHandIndex >= 0)
		{
			PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			return;
		}

		_networkPendingItemHandIndex = itemHandIndex;
		_networkPendingTurnSequenceGeneration = turnSequenceGeneration;
		NetworkManager? networkManager = GetNetworkManager();
		bool predictionStarted = BeginPredictedNetworkItemCardPlay(itemHandIndex, turnSequenceGeneration);
		if (predictionStarted)
		{
			networkManager?.RequestPlayItemCard(itemHandIndex, turnSequenceGeneration);
		}

		if (!await CompletePredictedNetworkItemCardPlayAsync(itemHandIndex, turnSequenceGeneration))
		{
			ClearNetworkPendingActionState();
			ResetNetworkPredictedItemVisualState();
			RenderPlayerHands();
			RenderTableCards();
			return;
		}
	}

	internal void RequestNetworkPendingItemSelection(int targetPlayerIndex, int targetHandIndex, int turnSequenceGeneration)
	{
		NetworkManager? networkManager = GetNetworkManager();
		if (networkManager == null)
		{
			return;
		}

		bool allowImmediateFollowupSelection = _networkLocallyAdvancedPendingTargetSelection
			&& _pendingItemSelectionMode == PendingItemSelectionMode.TargetHandCard
			&& targetHandIndex >= 0;
		if (!allowImmediateFollowupSelection && !NetworkSeatControlRules.CanSendPendingSelectionRequest(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			networkManager.State,
			_networkPendingSelectionRequestInFlight))
		{
			if (_networkPendingSelectionRequestInFlight)
			{
				PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			}
			return;
		}

		_networkPendingSelectionRequestInFlight = true;
		DisposeActiveSelectionOverlay();
		networkManager.RequestPendingItemSelection(targetPlayerIndex, targetHandIndex, turnSequenceGeneration);
	}

	internal async void RequestNetworkHandCardPlay(int handIndex, int turnSequenceGeneration)
	{
		if (!IsNetworkMultiplayerMode() || IsNetworkHost())
		{
			ConfirmLocalHandCardPlay(handIndex, turnSequenceGeneration, TryGetHandCardStartGlobalPosition(GetLocalHandPlayerIndex(), handIndex));
			return;
		}

		if (_networkPendingHandIndex >= 0 || _networkPendingItemHandIndex >= 0)
		{
			PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			return;
		}

		_networkPendingHandIndex = handIndex;
		_networkPendingTurnSequenceGeneration = turnSequenceGeneration;
		NetworkManager? networkManager = GetNetworkManager();
		bool predictionStarted = BeginPredictedNetworkHandCardPlay(handIndex, turnSequenceGeneration);
		if (predictionStarted)
		{
			networkManager?.RequestPlayHandCard(handIndex, turnSequenceGeneration);
		}

		if (!await CompletePredictedNetworkHandCardPlayAsync(handIndex, turnSequenceGeneration))
		{
			ClearNetworkPendingActionState();
			ResetNetworkPredictedHandVisualState();
			RenderPlayerHands();
			RenderTableCards();
			return;
		}
	}


	private static TEnum ReadEnum<TEnum>(Godot.Collections.Dictionary dictionary, string key, TEnum fallback)
		where TEnum : struct, Enum
	{
		return dictionary.TryGetValue(key, out Variant value)
			&& Enum.TryParse(value.AsString(), ignoreCase: true, out TEnum parsed)
			? parsed
			: fallback;
	}

	private bool IsNetworkMultiplayerMode()
	{
		return !string.IsNullOrWhiteSpace(_networkSessionId);
	}

	private bool IsNetworkHost()
	{
		return GetNetworkManager()?.IsHost ?? false;
	}

	private int GetLocalHandPlayerIndex()
	{
		return IsNetworkMultiplayerMode() ? Mathf.Clamp(_localPlayerIndex, 0, MaxPlayers - 1) : MainPlayerIndex;
	}

	private bool IsLocalHandPlayer(int playerIndex)
	{
		return playerIndex == GetLocalHandPlayerIndex();
	}

}
