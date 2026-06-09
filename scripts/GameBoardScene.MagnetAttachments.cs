using System;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task ResolvePendingMagnetAttachmentsAsync(int turnSequenceGeneration)
	{
		while (_pendingItemAttachmentRewards.Count > 0)
		{
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			PendingItemAttachmentReward reward = _pendingItemAttachmentRewards.Dequeue();
			if (!PlayerHasAttachableMagnetCard(reward.PlayerIndex, reward.ItemCardId))
			{
				continue;
			}

			if (ShouldUseInteractiveHumanItemFollowup(reward.PlayerIndex))
			{
				await ResolveHumanMagnetAttachmentAsync(reward.PlayerIndex, reward.ItemCardId, turnSequenceGeneration);
			}
			else
			{
				if (AttachMagnetToRandomHandCard(reward.PlayerIndex, reward.ItemCardId))
				{
					PlayGameSfx(GameSfx.MagnetAttach, -1.0f, 0.97f, 1.03f, 0.08);
				}
			}

			RenderPlayerHands();
		}
	}

	private async Task ResolveMainPlayerMagnetAttachmentAsync(string itemCardId, int turnSequenceGeneration)
	{
		await ResolveHumanMagnetAttachmentAsync(MainPlayerIndex, itemCardId, turnSequenceGeneration);
	}

	private async Task ResolveHumanMagnetAttachmentAsync(int ownerPlayerIndex, string itemCardId, int turnSequenceGeneration)
	{
		if (_isResolvingMagnetAttachmentSelection || !PlayerHasAttachableMagnetCard(ownerPlayerIndex, itemCardId))
		{
			return;
		}

		bool wasProcessingCardPlay = _isProcessingCardPlay;
		_isResolvingMagnetAttachmentSelection = true;
		_isProcessingCardPlay = true;
		_pendingItemCardId = itemCardId;
		_pendingItemOwnerIndex = ownerPlayerIndex;
		_pendingItemHandIndex = -1;
		_pendingItemTurnSequenceGeneration = turnSequenceGeneration;
		_pendingItemSelectionMode = PendingItemSelectionMode.MagnetHandCard;
		_pendingMagnetAttachmentCompletion = new TaskCompletionSource<bool>();
		RenderPlayerHands();
		SendBoardSnapshotIfHost();
		ShowMagnetHandSelection();
		BeginNetworkAiProxyTurnForPendingSelectionIfNeeded();

		try
		{
			System.Threading.Tasks.Task timeoutTask = WaitForMagnetAttachmentTimeoutAsync();
			await Task.WhenAny(_pendingMagnetAttachmentCompletion.Task, timeoutTask);
			if (turnSequenceGeneration == _turnSequenceGeneration
				&& _pendingItemSelectionMode == PendingItemSelectionMode.MagnetHandCard
				&& _pendingItemOwnerIndex == ownerPlayerIndex)
			{
				await CompleteMagnetAttachmentSelectionAsync(PickRandomAttachableMagnetHandIndex(ownerPlayerIndex, itemCardId));
			}

			while (_pendingItemSelectionMode == PendingItemSelectionMode.MagnetHandCard
				&& turnSequenceGeneration == _turnSequenceGeneration
				&& !IsGameOver())
			{
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}
		}
		finally
		{
			if (_pendingItemSelectionMode == PendingItemSelectionMode.MagnetHandCard
				&& _pendingItemOwnerIndex == ownerPlayerIndex)
			{
				ClearSelectionOverlay();
				ClearPendingItemSelectionState();
			}

			_isResolvingMagnetAttachmentSelection = false;
			_pendingMagnetAttachmentCompletion = null;
			_isProcessingCardPlay = wasProcessingCardPlay;
			RenderPlayerHands();
		}
	}

	private async System.Threading.Tasks.Task WaitForMagnetAttachmentTimeoutAsync()
	{
		await ToSignal(GetTree().CreateTimer(MagnetAttachmentSelectionSeconds), SceneTreeTimer.SignalName.Timeout);
	}

	private void ShowMagnetHandSelection()
	{
		ClearSelectionOverlay();
		if (!IsMainPlayerMagnetSelectionActive())
		{
			ClearMagnetAttachmentCountdown();
			return;
		}

		int ownerPlayerIndex = GetPendingSelectionLocalOwnerIndex();
		if (!PlayerHasAttachableMagnetCard(ownerPlayerIndex, _pendingItemCardId))
		{
			if (!ShouldSendNetworkPendingItemSelectionRequest())
			{
				_ = CompleteMagnetAttachmentSelectionAsync(-1);
			}
			else
			{
				ClearMagnetAttachmentCountdown();
			}

			return;
		}

		RenderPlayerHands();
		ShowMagnetAttachmentCountdown();
	}

	private async Task CompleteMagnetAttachmentSelectionAsync(int handIndex)
	{
		if (_pendingItemSelectionMode != PendingItemSelectionMode.MagnetHandCard)
		{
			return;
		}

		if (_isAnimatingMagnetAttachment)
		{
			return;
		}

		int ownerPlayerIndex = _pendingItemOwnerIndex;
		string itemCardId = _pendingItemCardId;
		int attachHandIndex = IsValidMagnetAttachmentHandIndex(ownerPlayerIndex, handIndex, itemCardId)
			? handIndex
			: PickRandomAttachableMagnetHandIndex(ownerPlayerIndex, itemCardId);
		if (attachHandIndex < 0)
		{
			ClearSelectionOverlay();
			ClearPendingItemSelectionState();
			RenderPlayerHands();
			SendBoardSnapshotIfHost();
			return;
		}

		_isAnimatingMagnetAttachment = true;
		ClearMagnetAttachmentCountdown();
		await PlayMagnetAttachmentToHandCardAnimationAsync(ownerPlayerIndex, attachHandIndex, itemCardId);
		bool attached = AttachMagnetToHandCard(ownerPlayerIndex, attachHandIndex, itemCardId);
		if (!attached)
		{
			AttachMagnetToRandomHandCard(ownerPlayerIndex, itemCardId);
		}

		ClearSelectionOverlay();
		ClearPendingItemSelectionState();
		RenderPlayerHands();
		SendBoardSnapshotIfHost();
	}

	private bool PlayerHasAttachableMagnetCard(int playerIndex, string itemCardId)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return false;
		}

		foreach (PlayingCard card in _playerHands[playerIndex])
		{
			if (IsAttachableMagnetCard(card, itemCardId))
			{
				return true;
			}
		}

		return false;
	}

	private bool AttachMagnetToHandCard(int playerIndex, int handIndex, string itemCardId)
	{
		if (!IsValidMagnetAttachmentHandIndex(playerIndex, handIndex, itemCardId))
		{
			return false;
		}

		PlayingCard card = _playerHands[playerIndex][handIndex];
		_playerHands[playerIndex][handIndex] = card.WithAttachedItemEffect(itemCardId);
		return true;
	}

	private bool AttachMagnetToRandomHandCard(int playerIndex, string itemCardId)
	{
		int handIndex = PickRandomAttachableMagnetHandIndex(playerIndex, itemCardId);
		return AttachMagnetToHandCard(playerIndex, handIndex, itemCardId);
	}

	private int PickRandomAttachableMagnetHandIndex(int playerIndex, string itemCardId)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return -1;
		}

		return NetworkAiFallbackRules.PickRandomLegalCandidate(
			_playerHands[playerIndex].Count,
			handIndex => IsAttachableMagnetCard(_playerHands[playerIndex][handIndex], itemCardId),
			_rng.RandiRange(0, int.MaxValue));
	}

	private bool IsValidMagnetAttachmentHandIndex(int playerIndex, int handIndex, string itemCardId)
	{
		return playerIndex >= 0
			&& playerIndex < _playerHands.Count
			&& handIndex >= 0
			&& handIndex < _playerHands[playerIndex].Count
			&& IsAttachableMagnetCard(_playerHands[playerIndex][handIndex], itemCardId);
	}

	private bool IsAttachableMagnetCard(PlayingCard card, string itemCardId)
	{
		if (IsExcludedDeckRank(card.Rank))
		{
			return false;
		}

		return !card.HasAttachedItemEffect(itemCardId);
	}

	private partial class MagnetAttachmentApplyRing : Control
	{
		private float _ringProgress;
		private float _ringAlpha;

		public float ring_progress
		{
			get => _ringProgress;
			set
			{
				_ringProgress = Mathf.Clamp(value, 0.0f, 1.0f);
				QueueRedraw();
			}
		}

		public float ring_alpha
		{
			get => _ringAlpha;
			set
			{
				_ringAlpha = Mathf.Clamp(value, 0.0f, 1.0f);
				QueueRedraw();
			}
		}

		public override void _Draw()
		{
			if (_ringAlpha <= 0.0f)
			{
				return;
			}

			Vector2 center = Size / 2.0f;
			float radius = Mathf.Lerp(30.0f, 58.0f, _ringProgress);
			DrawCircle(center, radius + 13.0f, new Color(0.08f, 0.78f, 0.92f, 0.14f * _ringAlpha));
			DrawArc(center, radius + 7.0f, -Mathf.Pi / 2.0f, -Mathf.Pi / 2.0f + FullCircleRadians * _ringProgress, 72, new Color(0.42f, 1.0f, 0.95f, 0.92f * _ringAlpha), 6.0f, true);
			DrawArc(center, radius - 7.0f, 0.0f, FullCircleRadians, 72, new Color(1.0f, 0.94f, 0.38f, 0.44f * _ringAlpha), 2.5f, true);
			DrawArc(center, radius + 15.0f, 0.0f, FullCircleRadians, 72, new Color(1.0f, 1.0f, 1.0f, 0.22f * _ringAlpha), 2.0f, true);
		}
	}
}
