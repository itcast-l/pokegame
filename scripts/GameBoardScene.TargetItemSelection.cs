using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private bool _debugForceNextRequiredItemSelectionOverlayDisplayFailure;
	private bool _debugForceNextRequiredItemSelectionOverlayInstantiationFailure;
	private enum SelectionOverlayCreationResult
	{
		Shown,
		NoLegalOptions,
		DisplayFailed,
	}

	private void RefreshNetworkPendingSelectionOverlay()
	{
		if (!IsNetworkMultiplayerMode() || IsNetworkHost())
		{
			return;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.HandLimitDiscard)
		{
			ShowHandLimitDiscardSelection();
			return;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.AttachmentRewardHandCard)
		{
			ShowMagnetHandSelection();
			return;
		}

		DismissActiveSelectionOverlay(invokeCancel: false);
	}

	private bool ShouldSendNetworkPendingItemSelectionRequest()
	{
		return IsNetworkMultiplayerMode() && !IsNetworkHost();
	}

	private int GetPendingSelectionLocalOwnerIndex()
	{
		return IsNetworkMultiplayerMode() ? GetLocalHandPlayerIndex() : MainPlayerIndex;
	}

	private SelectionOverlayCreationResult ShowSelectionOverlay(
		string title,
		IReadOnlyList<(string Text, Action Action)> options,
		bool showCancel)
	{
		DisposeActiveSelectionOverlay();
		if (options.Count == 0)
		{
			return SelectionOverlayCreationResult.NoLegalOptions;
		}

		var optionLabels = new List<string>(options.Count);
		var optionActions = new List<Action>(options.Count);
		foreach ((string text, Action action) in options)
		{
			optionLabels.Add(text);
			optionActions.Add(action);
		}

		ItemSelectionOverlay? selectionOverlay = null;
		try
		{
			if (_debugForceNextRequiredItemSelectionOverlayDisplayFailure)
			{
				_debugForceNextRequiredItemSelectionOverlayDisplayFailure = false;
				return SelectionOverlayCreationResult.DisplayFailed;
			}
			PackedScene? selectionOverlayScene = GD.Load<PackedScene>(ItemSelectionOverlayScenePath);
			if (_debugForceNextRequiredItemSelectionOverlayInstantiationFailure)
			{
				_debugForceNextRequiredItemSelectionOverlayInstantiationFailure = false;
				return SelectionOverlayCreationResult.DisplayFailed;
			}
			selectionOverlay = selectionOverlayScene?.Instantiate() as ItemSelectionOverlay;
			if (selectionOverlay == null)
			{
				GD.PushError($"Missing item selection overlay scene: {ItemSelectionOverlayScenePath}");
				return SelectionOverlayCreationResult.DisplayFailed;
			}

			AddChild(selectionOverlay);
			PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08);
			selectionOverlay.Configure(title, optionLabels, Localize("GAME_CANCEL"), showCancel);
			selectionOverlay.OptionSelected += optionIndex => InvokeOption(optionIndex, optionActions);
			selectionOverlay.CancelRequested += RejectRequiredSelectionCancel;
		}
		catch (Exception exception)
		{
			GD.PushError($"Item selection overlay creation failed: {exception}");
			if (selectionOverlay != null && IsInstanceValid(selectionOverlay))
			{
				selectionOverlay.QueueFree();
			}
			return SelectionOverlayCreationResult.DisplayFailed;
		}
		if (selectionOverlay == null)
		{
			return SelectionOverlayCreationResult.DisplayFailed;
		}

		RegisterSelectionOverlaySession(new SelectionOverlaySession(
			SelectionOverlayKind.ItemSelection,
			selectionOverlay,
			optionActions.Count,
			optionIndex => InvokeOption(optionIndex, optionActions),
			cancel: RejectRequiredSelectionCancel,
			dispose: () =>
			{
				if (IsInstanceValid(selectionOverlay))
				{
					RemoveChild(selectionOverlay);
					selectionOverlay.CallDeferred(Node.MethodName.QueueFree);
				}
			},
			abnormalRelease: () => InvokeOption(0, optionActions)));
		return SelectionOverlayCreationResult.Shown;
	}

	private void ResolveRequiredItemSelectionCreationFailure(
		SelectionOverlayCreationResult creationResult,
		IReadOnlyList<(string Text, Action Action)> legalOptions)
	{
		if (creationResult == SelectionOverlayCreationResult.Shown)
		{
			return;
		}

		if (legalOptions.Count > 0)
		{
			legalOptions[0].Action.Invoke();
			return;
		}

		GD.PushError(
			$"Required selection has no legal candidates: mode={_pendingItemSelectionMode} owner={_pendingItemOwnerIndex}.");
		if (ShouldSendNetworkPendingItemSelectionRequest())
		{
			return;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.HandLimitDiscard)
		{
			_pendingHandLimitDiscardCompletion?.TrySetResult(-1);
		}
		else if (_pendingItemSelectionMode == PendingItemSelectionMode.AttachmentRewardHandCard)
		{
			_ = CompleteMagnetAttachmentSelectionAsync(-1);
		}
	}

	private bool InvokeOption(int optionIndex, IReadOnlyList<Action> optionActions)
	{
		if (optionIndex < 0 || optionIndex >= optionActions.Count)
		{
			PlayGameSfx(GameSfx.UiInvalid, -3.0f, 0.98f, 1.02f, 0.08);
			return false;
		}

		optionActions[optionIndex].Invoke();
		return true;
	}

	private void RejectRequiredSelectionCancel()
	{
		PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
	}
}
