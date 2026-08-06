using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private readonly record struct AdventureUpgradeOverlayOptionModel(
		string Suit,
		string Rarity,
		string Title,
		string Description);

	private sealed class AdventureUpgradeOverlayCardMotionState
	{
		internal AdventureUpgradeOverlayCardMotionState(
			Button button,
			Control cardFrame,
			AdventureUpgradeChoiceArt choiceArt,
			Color baseModulate)
		{
			Button = button;
			CardFrame = cardFrame;
			ChoiceArt = choiceArt;
			BaseModulate = baseModulate;
			BasePosition = Vector2.Zero;
			BaseScale = Vector2.One * AdventureUpgradeOverlayRestScale;
			BaseRotationDegrees = 0.0f;
			StartPosition = BasePosition;
			StartScale = BaseScale;
			StartRotationDegrees = BaseRotationDegrees;
			TargetPosition = BasePosition;
			TargetScale = BaseScale;
			TargetRotationDegrees = BaseRotationDegrees;
		}

		internal Button Button { get; }
		internal Control CardFrame { get; }
		internal AdventureUpgradeChoiceArt ChoiceArt { get; }
		internal Color BaseModulate { get; }
		internal bool PointerHovered { get; set; }
		internal Vector2 BasePosition { get; set; }
		internal Vector2 BaseScale { get; set; }
		internal float BaseRotationDegrees { get; set; }
		internal Vector2 StartPosition { get; set; }
		internal Vector2 StartScale { get; set; }
		internal float StartRotationDegrees { get; set; }
		internal float RevealDelaySeconds { get; set; }
		internal float RevealElapsedSeconds { get; set; }
		internal bool RevealCompleted { get; set; }
		internal bool InteractionUnlocked { get; set; }
		internal Vector2 TargetPosition { get; set; }
		internal Vector2 TargetScale { get; set; }
		internal float TargetRotationDegrees { get; set; }
		internal float TargetHighlightAmount { get; set; }
	}

	private sealed class AdventureUpgradeOverlayRevealState
	{
		internal AdventureUpgradeOverlayRevealState(
			Control root,
			ColorRect backdrop,
			Panel selectionPanel,
			TextureRect selectionHalo,
			int visibleCardCount)
		{
			Root = root;
			Backdrop = backdrop;
			SelectionPanel = selectionPanel;
			SelectionHalo = selectionHalo;
			VisibleCardCount = visibleCardCount;
			BackdropBaseModulate = backdrop.Modulate;
			SelectionPanelBaseModulate = selectionPanel.Modulate;
			SelectionHaloBaseModulate = selectionHalo.Modulate;
		}

		internal Control Root { get; }
		internal ColorRect Backdrop { get; }
		internal Panel SelectionPanel { get; }
		internal TextureRect SelectionHalo { get; }
		internal int VisibleCardCount { get; }
		internal Color BackdropBaseModulate { get; }
		internal Color SelectionPanelBaseModulate { get; }
		internal Color SelectionHaloBaseModulate { get; }
		internal float ElapsedSeconds { get; set; }
		internal bool InteractionUnlocked { get; set; }
		internal bool VisualRevealCompleted { get; set; }
	}

	private static readonly Color DefaultAdventureUpgradeTitleColor = new(0.93f, 0.96f, 1.0f, 0.99f);
	private static readonly Color DefaultAdventureUpgradeGlowColor = new(0.48f, 0.92f, 1.0f, 0.06f);
	private static readonly Color DefaultAdventureUpgradeRuleColor = new(0.98f, 0.9f, 0.7f, 0.08f);
	private static readonly int[] AdventureUpgradeOverlayPreferredOrder = { 1, 0, 2 };
	private const float AdventureUpgradeOverlayRestScale = 0.97f;
	private const float AdventureUpgradeOverlayHoverScale = 1.015f;
	private const float AdventureUpgradeOverlayRevealStartScale = 0.92f;
	private const float AdventureUpgradeOverlayHoverLift = 8.0f;
	private const float AdventureUpgradeOverlayRevealStartYOffset = 16.0f;
	private const float AdventureUpgradeOverlayRevealStaggerSeconds = 0.06f;
	private const float AdventureUpgradeOverlayRevealDurationSeconds = 0.36f;
	private const float AdventureUpgradeOverlayBackdropFadeDurationSeconds = 0.14f;
	private const float AdventureUpgradeOverlayPanelFadeDurationSeconds = 0.18f;
	private const float AdventureUpgradeOverlayMotionSmoothSpeed = 30.0f;
	private const float AdventureUpgradeOverlayHighlightSmoothSpeed = 10.0f;
	private const float AdventureUpgradeOverlayHoverHighlightAmount = 1.0f;
	private const float AdventureUpgradeOverlayMotionSnapDistance = 0.001f;

	private TaskCompletionSource<AdventureUpgradeSelectionResult>? _adventureUpgradeSelectionCompletionSource;
	private AdventureUpgradeOverlayRevealState? _adventureUpgradeOverlayRevealState;
	private readonly Dictionary<ulong, AdventureUpgradeOverlayCardMotionState> _adventureUpgradeOverlayCardMotionStates = new();
	private readonly List<ulong> _staleAdventureUpgradeMotionStateIds = new();
	private bool _adventureUpgradeOverlayFrameUpdateActive;
	private IDisposable? _adventureUpgradeOverlayFrameRegistration;
	private int _debugAdventureUpgradePointerEnteredCallbacks;
	private int _debugAdventureUpgradePointerExitedCallbacks;

	private async Task<AdventureUpgradeSelectionResult> ShowAdventureUpgradeSelectionAsync(
		AdventureUpgradeTriggerSource triggerSource,
		IReadOnlyList<AdventureSuitUpgradeDefinition> offer)
	{
		DisposeActiveSelectionOverlay();

		var overlayOptions = new List<AdventureUpgradeOverlayOptionModel>(offer.Count);
		for (int index = 0; index < offer.Count; index++)
		{
			AdventureSuitUpgradeDefinition definition = offer[index];
			overlayOptions.Add(new AdventureUpgradeOverlayOptionModel(
				definition.Suit,
				definition.Rarity,
				definition.DisplayName,
				FormatAdventureUpgradeDescription(definition)));
		}

		var completionSource = new TaskCompletionSource<AdventureUpgradeSelectionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		_adventureUpgradeSelectionCompletionSource = completionSource;
		if (!ShowAdventureUpgradeSelectionOverlay(overlayOptions))
		{
			_adventureUpgradeSelectionCompletionSource = null;
			return new AdventureUpgradeSelectionResult(
				AdventureUpgradeSelectionOutcome.DisplayFailed,
				-1);
		}

		AdventureUpgradeSelectionResult selectionResult = await completionSource.Task;
		if (ReferenceEquals(_adventureUpgradeSelectionCompletionSource, completionSource))
		{
			_adventureUpgradeSelectionCompletionSource = null;
		}

		return selectionResult;
	}

	private void CancelAdventureUpgradeSelection()
	{
		ResolveAdventureUpgradeSelection(
			new AdventureUpgradeSelectionResult(
				AdventureUpgradeSelectionOutcome.Canceled,
				-1),
			clearOverlay: false);
	}

	private void ResolveAdventureUpgradeSelection(
		AdventureUpgradeSelectionResult result,
		bool clearOverlay)
	{
		TaskCompletionSource<AdventureUpgradeSelectionResult>? completionSource = _adventureUpgradeSelectionCompletionSource;
		_adventureUpgradeSelectionCompletionSource = null;
		completionSource?.TrySetResult(result);
		if (clearOverlay)
		{
			DisposeActiveSelectionOverlay();
		}
	}

	private bool ShowAdventureUpgradeSelectionOverlay(IReadOnlyList<AdventureUpgradeOverlayOptionModel> options)
	{
		if (ConsumeDebugForceNextAdventureUpgradeOverlayDisplayFailure())
		{
			return false;
		}

		PackedScene? overlayScene = GD.Load<PackedScene>(AdventureUpgradeSelectionOverlayScenePath);
		if (overlayScene?.Instantiate() is not Control selectionOverlay)
		{
			GD.PushError($"Missing adventure upgrade selection overlay scene: {AdventureUpgradeSelectionOverlayScenePath}");
			return false;
		}

		var overlayLayer = new CanvasLayer
		{
			Name = "AdventureUpgradeSelectionLayer",
			Layer = 40,
		};
		AddChild(overlayLayer);
		overlayLayer.AddChild(selectionOverlay);
		ResetAdventureUpgradeOverlayCardMotionStates();
		_debugAdventureUpgradePointerEnteredCallbacks = 0;
		_debugAdventureUpgradePointerExitedCallbacks = 0;
		var session = new SelectionOverlaySession(
			SelectionOverlayKind.AdventureUpgrade,
			selectionOverlay,
			options.Count,
			optionIndex =>
			{
				if (optionIndex < 0 || optionIndex >= options.Count)
				{
					return false;
				}

				ResolveAdventureUpgradeSelection(
					new AdventureUpgradeSelectionResult(
						AdventureUpgradeSelectionOutcome.Confirmed,
						optionIndex),
					clearOverlay: true);
				return true;
			},
			cancel: CancelAdventureUpgradeSelection,
			dispose: () =>
			{
				CancelAdventureUpgradeSelection();
				ResetAdventureUpgradeOverlayCardMotionStates();
				if (IsInstanceValid(overlayLayer))
				{
					overlayLayer.QueueFree();
				}
			},
			abnormalRelease: () =>
			{
				ResolveAdventureUpgradeSelection(
					new AdventureUpgradeSelectionResult(
						AdventureUpgradeSelectionOutcome.Confirmed,
						0),
					clearOverlay: false);
			});
		RegisterSelectionOverlaySession(session);
		try
		{
			ConfigureAdventureUpgradeSelectionOverlay(session, selectionOverlay, options);
		}
		catch (Exception error)
		{
			GD.PushError($"Could not configure adventure upgrade selection overlay: {error}");
			DisposeActiveSelectionOverlay();
			return false;
		}
		PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08);
		return true;
	}

	private string FormatAdventureUpgradeDescription(AdventureSuitUpgradeDefinition definition)
	{
		string description = GetAdventureUpgradeDescription(definition.DescriptionKey);
		AdventureUpgradeManifestEntry? entry = null;
		if (_adventureUpgradeCatalog != null
			&& _adventureUpgradeCatalog.TryCreateManifest(out AdventureUpgradeManifest manifest)
			&& manifest.TryGetEnabledEntry(definition.Id, out AdventureUpgradeManifestEntry resolvedEntry))
		{
			entry = resolvedEntry;
		}

		return AdventureUpgradeDefinitionDescriptionFormatter.Format(
			definition,
			entry,
			description,
			GetAdventureUpgradeRunState()?.Effects.QuestionRewardCount ?? 0);
	}

	private void ConfigureAdventureUpgradeSelectionOverlay(
		SelectionOverlaySession session,
		Control selectionOverlay,
		IReadOnlyList<AdventureUpgradeOverlayOptionModel> options)
	{
		Control? header = selectionOverlay.GetNodeOrNull<Control>("Margin/Layout/Header");
		if (header != null)
		{
			header.Visible = true;
		}
		Label? title = selectionOverlay.GetNodeOrNull<Label>("Margin/Layout/Header/SelectionTitle");
		if (title != null)
		{
			title.Text = Localize("GAME_SELECT_UPGRADE");
			ApplyAdventureUpgradeSelectionTitleTypography(title);
		}

		ColorRect backdrop = selectionOverlay.GetNode<ColorRect>("TableDarken");
		Panel selectionPanel = selectionOverlay.GetNode<Panel>("Margin/SelectionPanel");
		TextureRect selectionHalo = selectionOverlay.GetNode<TextureRect>("Margin/SelectionHalo");
		int visibleOptionCount = Mathf.Min(options.Count, 3);
		InitializeAdventureUpgradeSelectionOverlayReveal(
			selectionOverlay,
			backdrop,
			selectionPanel,
			selectionHalo,
			visibleOptionCount);

		for (int index = 0; index < 3; index++)
		{
			int capturedIndex = index;
			Button button = selectionOverlay.GetNode<Button>($"Margin/Layout/CardsRow/Option{index + 1}Button");
			AdventureUpgradeChoiceArt choiceArt = button.GetNode<AdventureUpgradeChoiceArt>("CardFrame/ChoiceArt");
			Control cardFrame = button.GetNode<Control>("CardFrame");

			bool visible = index < options.Count;
			Color baseModulate = cardFrame.Modulate;
			button.Visible = visible;
			button.Text = string.Empty;
			button.Disabled = !visible;
			button.FocusMode = visible ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
			cardFrame.PivotOffset = button.Size * 0.5f;
			cardFrame.Position = GetAdventureUpgradeCardFrameOffset(index);
			cardFrame.RotationDegrees = GetAdventureUpgradeCardFrameRotation(index);
			cardFrame.Scale = Vector2.One * AdventureUpgradeOverlayRestScale;
			cardFrame.Modulate = baseModulate;

			BindSelectionOverlayButton(
				session,
				button,
				capturedIndex,
				RefreshAdventureUpgradeOverlayHighlight,
				HandleAdventureUpgradeOverlayPointerEvent);

			if (!visible)
			{
				button.ReleaseFocus();
				choiceArt.ConfigureChoice(null, string.Empty, string.Empty, false);
				choiceArt.SetHighlightAmount(0.0f);
				continue;
			}

			RegisterAdventureUpgradeOverlayCardMotionState(button, cardFrame, choiceArt, baseModulate);
			InitializeAdventureUpgradeOverlayCardReveal(button, index, visibleOptionCount);

			AdventureUpgradeOverlayOptionModel option = options[index];
			AdventureUpgradeCardVisualEntryResource? visualEntry = AdventureUpgradeCardVisualResolver.ResolveEntry(option.Suit, option.Rarity);
			Texture2D? cardTexture = AdventureUpgradeCardVisualResolver.LoadAtlasTextureForRarity(option.Suit, option.Rarity);
			choiceArt.ConfigureChoice(cardTexture, string.Empty, option.Description, false);
			choiceArt.SetPalette(
				visualEntry?.TitleColor ?? DefaultAdventureUpgradeTitleColor,
				visualEntry?.GlowColor ?? DefaultAdventureUpgradeGlowColor,
				visualEntry?.RuleColor ?? DefaultAdventureUpgradeRuleColor);
			RefreshAdventureUpgradeOverlayHighlight(button);
		}

		if (visibleOptionCount == 0)
		{
			CallDeferred(nameof(FocusAdventureUpgradeSelectionOverlayDefaultButton));
		}
	}

	private void FocusAdventureUpgradeSelectionOverlayDefaultButton()
	{
		if (_adventureUpgradeOverlayRevealState != null
			&& !_adventureUpgradeOverlayRevealState.InteractionUnlocked)
		{
			return;
		}

		SelectionOverlaySession? session = GetActiveSelectionOverlaySession(SelectionOverlayKind.AdventureUpgrade);
		if (session == null
			|| !IsSelectionOverlaySessionVisible(session)
			|| !session.Root.IsInsideTree())
		{
			return;
		}

		foreach (int index in AdventureUpgradeOverlayPreferredOrder)
		{
			Button button = session.Root.GetNode<Button>($"Margin/Layout/CardsRow/Option{index + 1}Button");
			if (!button.Visible || button.Disabled || !IsInstanceValid(button))
			{
				continue;
			}

			button.GrabFocus();
			RefreshAdventureUpgradeOverlayHighlight(button);
			return;
		}
	}

	private void RefreshAdventureUpgradeOverlayHighlight(Button button)
	{
		if (!TryGetAdventureUpgradeOverlayCardMotionState(button, out AdventureUpgradeOverlayCardMotionState state))
		{
			return;
		}

		bool canInteract = state.InteractionUnlocked && button.Visible && !button.Disabled;
		bool pointerHovered = canInteract && state.PointerHovered;
		bool focused = canInteract && button.HasFocus();
		bool active = pointerHovered || focused;

		state.TargetPosition = state.BasePosition + (active ? new Vector2(0.0f, -AdventureUpgradeOverlayHoverLift) : Vector2.Zero);
		state.TargetScale = active
			? Vector2.One * AdventureUpgradeOverlayHoverScale
			: state.BaseScale;
		state.TargetRotationDegrees = state.BaseRotationDegrees;
		state.TargetHighlightAmount = active ? AdventureUpgradeOverlayHoverHighlightAmount : 0.0f;

		state.CardFrame.PivotOffset = button.Size * 0.5f;
		state.CardFrame.ZIndex = active ? 3 : 0;
		button.ZIndex = active ? 2 : 0;
		RequestAdventureUpgradeOverlayFrameUpdate();
	}

	private static Vector2 GetAdventureUpgradeCardFrameOffset(int optionIndex)
	{
		return Vector2.Zero;
	}

	private static float GetAdventureUpgradeCardFrameRotation(int optionIndex)
	{
		return 0.0f;
	}

	private void RegisterAdventureUpgradeOverlayCardMotionState(
		Button button,
		Control cardFrame,
		AdventureUpgradeChoiceArt choiceArt,
		Color baseModulate)
	{
		_adventureUpgradeOverlayCardMotionStates[button.GetInstanceId()] =
			new AdventureUpgradeOverlayCardMotionState(button, cardFrame, choiceArt, baseModulate);
	}

	private bool TryGetAdventureUpgradeOverlayCardMotionState(
		Button button,
		out AdventureUpgradeOverlayCardMotionState state)
	{
		if (_adventureUpgradeOverlayCardMotionStates.TryGetValue(button.GetInstanceId(), out AdventureUpgradeOverlayCardMotionState? existingState))
		{
			state = existingState;
			return true;
		}

		state = null!;
		return false;
	}

	public Godot.Collections.Dictionary DebugGetAdventureUpgradeOverlayMotionState(int optionIndex)
	{
		var result = new Godot.Collections.Dictionary();
		SelectionOverlaySession? session = GetActiveSelectionOverlaySession(SelectionOverlayKind.AdventureUpgrade);
		if (session == null || optionIndex < 0 || optionIndex >= session.OptionCount)
		{
			return result;
		}

		Button? button = session.Root.GetNodeOrNull<Button>($"Margin/Layout/CardsRow/Option{optionIndex + 1}Button");
		if (button == null
			|| !TryGetAdventureUpgradeOverlayCardMotionState(button, out AdventureUpgradeOverlayCardMotionState state))
		{
			return result;
		}

		result["reveal_completed"] = state.RevealCompleted;
		result["interaction_unlocked"] = state.InteractionUnlocked;
		result["pointer_hovered"] = state.PointerHovered;
		result["position"] = state.CardFrame.Position;
		result["scale"] = state.CardFrame.Scale;
		result["rotation_degrees"] = state.CardFrame.RotationDegrees;
		result["frame_z_index"] = state.CardFrame.ZIndex;
		result["button_z_index"] = button.ZIndex;
		result["button_disabled"] = button.Disabled;
		result["has_focus"] = button.HasFocus();
		result["card_visual"] = state.ChoiceArt.DebugGetCardVisualState();
		result["pointer_entered_callbacks"] = _debugAdventureUpgradePointerEnteredCallbacks;
		result["pointer_exited_callbacks"] = _debugAdventureUpgradePointerExitedCallbacks;
		result["active_event_subscriptions"] = session.ActiveSubscriptionCount;
		return result;
	}

	public Godot.Collections.Dictionary DebugGetAdventureUpgradeOverlayEventMetrics()
	{
		return new Godot.Collections.Dictionary
		{
			["pointer_entered_callbacks"] = _debugAdventureUpgradePointerEnteredCallbacks,
			["pointer_exited_callbacks"] = _debugAdventureUpgradePointerExitedCallbacks,
			["active_subscription_groups"] = _activeSelectionOverlaySession?.ActiveSubscriptionCount ?? 0,
		};
	}

	public bool DebugSetAdventureUpgradeFocusState(int optionIndex, bool focused)
	{
		SelectionOverlaySession? session = GetActiveSelectionOverlaySession(SelectionOverlayKind.AdventureUpgrade);
		if (session == null || optionIndex < 0 || optionIndex >= session.OptionCount)
		{
			return false;
		}

		Button? button = session.Root.GetNodeOrNull<Button>($"Margin/Layout/CardsRow/Option{optionIndex + 1}Button");
		if (button == null || button.Disabled || !button.Visible)
		{
			return false;
		}

		if (focused)
		{
			button.GrabFocus();
		}
		else if (button.HasFocus())
		{
			button.ReleaseFocus();
		}
		RefreshAdventureUpgradeOverlayHighlight(button);
		return true;
	}

	private void SetAdventureUpgradeOverlayPointerHover(Button button, bool hovered)
	{
		if (!TryGetAdventureUpgradeOverlayCardMotionState(button, out AdventureUpgradeOverlayCardMotionState state))
		{
			return;
		}

		if (state.PointerHovered == hovered)
		{
			return;
		}

		state.PointerHovered = hovered;
		RefreshAdventureUpgradeOverlayHighlight(button);
		if (!state.RevealCompleted || !state.InteractionUnlocked || !IsInstanceValid(state.CardFrame))
		{
			return;
		}

		Vector2 immediatePositionTarget = hovered ? state.TargetPosition : state.BasePosition;
		Vector2 immediateScaleTarget = hovered ? state.TargetScale : state.BaseScale;
		state.CardFrame.Position = state.CardFrame.Position.Lerp(immediatePositionTarget, 0.28f);
		state.CardFrame.Scale = state.CardFrame.Scale.Lerp(immediateScaleTarget, 0.28f);
		RequestAdventureUpgradeOverlayFrameUpdate();
	}

	private void HandleAdventureUpgradeOverlayPointerEvent(Button button, bool hovered)
	{
		if (hovered)
		{
			_debugAdventureUpgradePointerEnteredCallbacks++;
		}
		else
		{
			_debugAdventureUpgradePointerExitedCallbacks++;
		}
		SetAdventureUpgradeOverlayPointerHover(button, hovered);
	}

	private void InitializeAdventureUpgradeSelectionOverlayReveal(
		Control selectionOverlay,
		ColorRect backdrop,
		Panel selectionPanel,
		TextureRect selectionHalo,
		int visibleOptionCount)
	{
		_adventureUpgradeOverlayRevealState = new AdventureUpgradeOverlayRevealState(
			selectionOverlay,
			backdrop,
			selectionPanel,
			selectionHalo,
			visibleOptionCount);
		SetAdventureUpgradeOverlayAlpha(backdrop, _adventureUpgradeOverlayRevealState.BackdropBaseModulate, 0.0f);
		SetAdventureUpgradeOverlayAlpha(selectionPanel, _adventureUpgradeOverlayRevealState.SelectionPanelBaseModulate, 0.0f);
		SetAdventureUpgradeOverlayAlpha(selectionHalo, _adventureUpgradeOverlayRevealState.SelectionHaloBaseModulate, 0.0f);
		RequestAdventureUpgradeOverlayFrameUpdate();
	}

	private void InitializeAdventureUpgradeOverlayCardReveal(Button button, int optionIndex, int visibleOptionCount)
	{
		if (!TryGetAdventureUpgradeOverlayCardMotionState(button, out AdventureUpgradeOverlayCardMotionState state))
		{
			return;
		}

		state.PointerHovered = false;
		state.BasePosition = GetAdventureUpgradeCardFrameOffset(optionIndex);
		state.BaseScale = Vector2.One * AdventureUpgradeOverlayRestScale;
		state.BaseRotationDegrees = GetAdventureUpgradeCardFrameRotation(optionIndex);
		state.StartPosition = state.BasePosition + new Vector2(0.0f, AdventureUpgradeOverlayRevealStartYOffset);
		state.StartScale = Vector2.One * AdventureUpgradeOverlayRevealStartScale;
		state.StartRotationDegrees = state.BaseRotationDegrees;
		state.RevealDelaySeconds = GetAdventureUpgradeOverlayRevealDelay(optionIndex, visibleOptionCount);
		state.RevealElapsedSeconds = 0.02f;
		state.RevealCompleted = false;
		state.InteractionUnlocked = false;
		state.TargetPosition = state.BasePosition;
		state.TargetScale = state.BaseScale;
		state.TargetRotationDegrees = state.BaseRotationDegrees;
		state.TargetHighlightAmount = 0.0f;
		state.CardFrame.Position = state.StartPosition;
		state.CardFrame.Scale = state.StartScale;
		state.CardFrame.RotationDegrees = state.StartRotationDegrees;
		state.CardFrame.Modulate = CreateAdventureUpgradeOverlayAlphaColor(state.BaseModulate, 0.0f);
		UpdateAdventureUpgradeOverlayCardReveal(state, 0.0);
		state.ChoiceArt.SetHighlightAmount(0.0f);
		button.ReleaseFocus();
		button.Disabled = true;
		button.FocusMode = Control.FocusModeEnum.None;
	}

	private void UnlockAdventureUpgradeSelectionOverlayInteraction()
	{
		if (_adventureUpgradeOverlayRevealState == null
			|| _adventureUpgradeOverlayRevealState.InteractionUnlocked)
		{
			return;
		}

		_adventureUpgradeOverlayRevealState.InteractionUnlocked = true;
		foreach (AdventureUpgradeOverlayCardMotionState state in _adventureUpgradeOverlayCardMotionStates.Values)
		{
			if (!IsInstanceValid(state.Button) || !state.Button.Visible)
			{
				continue;
			}

			state.InteractionUnlocked = true;
			state.Button.Disabled = false;
			state.Button.FocusMode = Control.FocusModeEnum.All;
			RefreshAdventureUpgradeOverlayHighlight(state.Button);
		}

		CallDeferred(nameof(FocusAdventureUpgradeSelectionOverlayDefaultButton));
	}

	private void ResetAdventureUpgradeOverlayCardMotionStates()
	{
		_adventureUpgradeOverlayRevealState = null;
		_adventureUpgradeOverlayCardMotionStates.Clear();
		_staleAdventureUpgradeMotionStateIds.Clear();
		StopAdventureUpgradeOverlayFrameUpdate();
		RefreshBoardProcessState();
	}

	private void UpdateAdventureUpgradeOverlayCardMotion(double delta)
	{
		UpdateAdventureUpgradeSelectionOverlayReveal(delta);

		if (_adventureUpgradeOverlayCardMotionStates.Count == 0)
		{
			StopAdventureUpgradeOverlayFrameUpdate();
			return;
		}

		float motionWeight = ComputeAdventureUpgradeOverlaySmoothingWeight(delta, AdventureUpgradeOverlayMotionSmoothSpeed);
		float highlightWeight = ComputeAdventureUpgradeOverlaySmoothingWeight(delta, AdventureUpgradeOverlayHighlightSmoothSpeed);
		_staleAdventureUpgradeMotionStateIds.Clear();

		foreach (KeyValuePair<ulong, AdventureUpgradeOverlayCardMotionState> pair in _adventureUpgradeOverlayCardMotionStates)
		{
			AdventureUpgradeOverlayCardMotionState state = pair.Value;
			if (!IsInstanceValid(state.Button) || !IsInstanceValid(state.CardFrame) || !IsInstanceValid(state.ChoiceArt))
			{
					_staleAdventureUpgradeMotionStateIds.Add(pair.Key);
				continue;
			}

			state.CardFrame.PivotOffset = state.Button.Size * 0.5f;
			UpdateAdventureUpgradeOverlayCardReveal(state, delta);

			if (!state.RevealCompleted)
			{
				continue;
			}

			state.CardFrame.Position = state.CardFrame.Position.Lerp(state.TargetPosition, motionWeight);
			state.CardFrame.Scale = state.CardFrame.Scale.Lerp(state.TargetScale, motionWeight);
			state.CardFrame.RotationDegrees = Mathf.Lerp(state.CardFrame.RotationDegrees, state.TargetRotationDegrees, motionWeight);
			state.CardFrame.Modulate = state.BaseModulate;
			float highlightAmount = Mathf.Lerp(state.ChoiceArt.HighlightAmount, state.TargetHighlightAmount, highlightWeight);
			if (Mathf.Abs(highlightAmount - state.TargetHighlightAmount) <= AdventureUpgradeOverlayMotionSnapDistance)
			{
				highlightAmount = state.TargetHighlightAmount;
			}
			if (!Mathf.IsEqualApprox(state.ChoiceArt.HighlightAmount, highlightAmount))
			{
				state.ChoiceArt.SetHighlightAmount(highlightAmount);
			}

			if (state.CardFrame.Position.DistanceTo(state.TargetPosition) <= AdventureUpgradeOverlayMotionSnapDistance)
			{
				state.CardFrame.Position = state.TargetPosition;
			}

			if (state.CardFrame.Scale.DistanceTo(state.TargetScale) <= AdventureUpgradeOverlayMotionSnapDistance)
			{
				state.CardFrame.Scale = state.TargetScale;
			}

			if (Mathf.Abs(state.CardFrame.RotationDegrees - state.TargetRotationDegrees) <= 0.02f)
			{
				state.CardFrame.RotationDegrees = state.TargetRotationDegrees;
			}
		}

			foreach (ulong staleStateId in _staleAdventureUpgradeMotionStateIds)
		{
			_adventureUpgradeOverlayCardMotionStates.Remove(staleStateId);
		}

		if (AreAdventureUpgradeOverlayCardsFullyRevealed())
		{
			UnlockAdventureUpgradeSelectionOverlayInteraction();
		}

		if (!HasAdventureUpgradeOverlayFrameWork())
		{
			StopAdventureUpgradeOverlayFrameUpdate();
		}
	}

	private void UpdateAdventureUpgradeSelectionOverlayReveal(double delta)
	{
		if (_adventureUpgradeOverlayRevealState == null
			|| _adventureUpgradeOverlayRevealState.VisualRevealCompleted)
		{
			return;
		}

		if (!IsInstanceValid(_adventureUpgradeOverlayRevealState.Root)
			|| !IsInstanceValid(_adventureUpgradeOverlayRevealState.Backdrop)
			|| !IsInstanceValid(_adventureUpgradeOverlayRevealState.SelectionPanel)
			|| !IsInstanceValid(_adventureUpgradeOverlayRevealState.SelectionHalo))
		{
			_adventureUpgradeOverlayRevealState = null;
			return;
		}

		_adventureUpgradeOverlayRevealState.ElapsedSeconds += (float)delta;
		float backdropAlpha = Mathf.Clamp(_adventureUpgradeOverlayRevealState.ElapsedSeconds / AdventureUpgradeOverlayBackdropFadeDurationSeconds, 0.0f, 1.0f);
		float panelAlpha = Mathf.Clamp(_adventureUpgradeOverlayRevealState.ElapsedSeconds / AdventureUpgradeOverlayPanelFadeDurationSeconds, 0.0f, 1.0f);

		SetAdventureUpgradeOverlayAlpha(
			_adventureUpgradeOverlayRevealState.Backdrop,
			_adventureUpgradeOverlayRevealState.BackdropBaseModulate,
			backdropAlpha);
		SetAdventureUpgradeOverlayAlpha(
			_adventureUpgradeOverlayRevealState.SelectionPanel,
			_adventureUpgradeOverlayRevealState.SelectionPanelBaseModulate,
			panelAlpha);
		SetAdventureUpgradeOverlayAlpha(
			_adventureUpgradeOverlayRevealState.SelectionHalo,
			_adventureUpgradeOverlayRevealState.SelectionHaloBaseModulate,
			panelAlpha);
		if (backdropAlpha >= 1.0f && panelAlpha >= 1.0f)
		{
			_adventureUpgradeOverlayRevealState.VisualRevealCompleted = true;
		}
	}

	private void RequestAdventureUpgradeOverlayFrameUpdate()
	{
		_adventureUpgradeOverlayFrameUpdateActive = true;
		_adventureUpgradeOverlayFrameRegistration ??=
			_frameScheduler.Register(UpdateAdventureUpgradeOverlayCardMotion);
		RequestBoardFrameUpdate();
	}

	private void StopAdventureUpgradeOverlayFrameUpdate()
	{
		_adventureUpgradeOverlayFrameUpdateActive = false;
		_adventureUpgradeOverlayFrameRegistration?.Dispose();
		_adventureUpgradeOverlayFrameRegistration = null;
	}

	private bool HasAdventureUpgradeOverlayFrameWork()
	{
		if (_adventureUpgradeOverlayRevealState?.VisualRevealCompleted == false)
		{
			return true;
		}

		foreach (AdventureUpgradeOverlayCardMotionState state in _adventureUpgradeOverlayCardMotionStates.Values)
		{
			if (!state.RevealCompleted
				|| state.CardFrame.Position.DistanceTo(state.TargetPosition) > AdventureUpgradeOverlayMotionSnapDistance
				|| state.CardFrame.Scale.DistanceTo(state.TargetScale) > AdventureUpgradeOverlayMotionSnapDistance
				|| Mathf.Abs(state.CardFrame.RotationDegrees - state.TargetRotationDegrees) > 0.02f
				|| Mathf.Abs(state.ChoiceArt.HighlightAmount - state.TargetHighlightAmount) > AdventureUpgradeOverlayMotionSnapDistance)
			{
				return true;
			}
		}
		return false;
	}

	private void UpdateAdventureUpgradeOverlayCardReveal(
		AdventureUpgradeOverlayCardMotionState state,
		double delta)
	{
		if (state.RevealCompleted)
		{
			return;
		}

		state.RevealElapsedSeconds += (float)delta;
		float revealTime = state.RevealElapsedSeconds - state.RevealDelaySeconds;
		if (revealTime <= 0.0f)
		{
			state.CardFrame.Position = state.StartPosition;
			state.CardFrame.Scale = state.StartScale;
			state.CardFrame.RotationDegrees = state.StartRotationDegrees;
			state.CardFrame.Modulate = CreateAdventureUpgradeOverlayAlphaColor(state.BaseModulate, 0.0f);
			state.ChoiceArt.SetHighlightAmount(0.0f);
			return;
		}

		float rawRevealT = Mathf.Clamp(revealTime / AdventureUpgradeOverlayRevealDurationSeconds, 0.0f, 1.0f);
		float easedRevealT = EaseOutAdventureUpgradeOverlayReveal(rawRevealT);
		float overshootWeight = ComputeAdventureUpgradeOverlayRevealOvershoot(rawRevealT);
		Vector2 revealPosition = state.StartPosition.Lerp(state.BasePosition, easedRevealT)
			+ new Vector2(0.0f, -AdventureUpgradeOverlayHoverLift * 0.9f * overshootWeight);
		Vector2 revealScale = state.StartScale.Lerp(state.BaseScale, easedRevealT)
			+ Vector2.One * ((AdventureUpgradeOverlayHoverScale - AdventureUpgradeOverlayRestScale) * overshootWeight);
		if (state.PointerHovered)
		{
			revealPosition = revealPosition.Lerp(state.TargetPosition, rawRevealT);
			revealScale = revealScale.Lerp(state.TargetScale, rawRevealT);
		}

		state.CardFrame.Position = revealPosition;
		state.CardFrame.Scale = revealScale;
		state.CardFrame.RotationDegrees = Mathf.Lerp(state.StartRotationDegrees, state.BaseRotationDegrees, easedRevealT);
		state.CardFrame.Modulate = CreateAdventureUpgradeOverlayAlphaColor(state.BaseModulate, rawRevealT);
		state.ChoiceArt.SetHighlightAmount(Mathf.Lerp(0.0f, state.TargetHighlightAmount, easedRevealT));

		if (rawRevealT < 1.0f)
		{
			return;
		}

		state.RevealCompleted = true;
		state.CardFrame.Position = state.BasePosition;
		state.CardFrame.Scale = state.BaseScale;
		state.CardFrame.RotationDegrees = state.BaseRotationDegrees;
		state.CardFrame.Modulate = state.BaseModulate;
	}

	private bool AreAdventureUpgradeOverlayCardsFullyRevealed()
	{
		if (_adventureUpgradeOverlayRevealState == null
			|| _adventureUpgradeOverlayRevealState.InteractionUnlocked
			|| _adventureUpgradeOverlayRevealState.VisibleCardCount == 0)
		{
			return false;
		}

		int revealedCount = 0;
		foreach (AdventureUpgradeOverlayCardMotionState state in _adventureUpgradeOverlayCardMotionStates.Values)
		{
			if (!IsInstanceValid(state.Button) || !state.Button.Visible)
			{
				continue;
			}

			if (!state.RevealCompleted)
			{
				return false;
			}

			revealedCount++;
		}

		return revealedCount >= _adventureUpgradeOverlayRevealState.VisibleCardCount;
	}

	private static float GetAdventureUpgradeOverlayRevealDelay(int optionIndex, int visibleOptionCount)
	{
		int revealOrder = 0;
		foreach (int candidateIndex in AdventureUpgradeOverlayPreferredOrder)
		{
			if (candidateIndex >= visibleOptionCount)
			{
				continue;
			}

			if (candidateIndex == optionIndex)
			{
				return revealOrder * AdventureUpgradeOverlayRevealStaggerSeconds;
			}

			revealOrder++;
		}

		return 0.0f;
	}

	private static void SetAdventureUpgradeOverlayAlpha(CanvasItem item, Color baseModulate, float alphaMultiplier)
	{
		item.Modulate = CreateAdventureUpgradeOverlayAlphaColor(baseModulate, alphaMultiplier);
	}

	private static Color CreateAdventureUpgradeOverlayAlphaColor(Color baseModulate, float alphaMultiplier)
	{
		Color color = baseModulate;
		color.A = baseModulate.A * Mathf.Clamp(alphaMultiplier, 0.0f, 1.0f);
		return color;
	}

	private static float EaseOutAdventureUpgradeOverlayReveal(float value)
	{
		float inverse = 1.0f - Mathf.Clamp(value, 0.0f, 1.0f);
		return 1.0f - inverse * inverse * inverse;
	}

	private static float ComputeAdventureUpgradeOverlayRevealOvershoot(float value)
	{
		float clamped = Mathf.Clamp(value, 0.0f, 1.0f);
		return Mathf.Sin(clamped * Mathf.Pi);
	}

	private static float ComputeAdventureUpgradeOverlaySmoothingWeight(double delta, float speed)
	{
		return 1.0f - Mathf.Exp(-speed * (float)delta);
	}

	private static int ExtractAdventureUpgradeOptionIndex(Button button)
	{
		string name = button.Name.ToString();
		if (!name.StartsWith("Option") || !name.EndsWith("Button"))
		{
			return 0;
		}

		string digits = name["Option".Length..^"Button".Length];
		return int.TryParse(digits, out int optionNumber)
			? Mathf.Max(0, optionNumber - 1)
			: 0;
	}
}
