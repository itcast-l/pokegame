using System;
using Godot;

public partial class GameBoardScene
{
	private const double AdventureTutorialCalloutReadSeconds = 1.2;
	private const int AdventureTutorialOverlayZIndex = 3000;
	private const string AdventureTutorialTableHighlightNodeName = "AdventureTutorialTableHighlight";
	private const float AdventureTutorialCalloutGap = 14.0f;
	private const float AdventureTutorialViewportPadding = 18.0f;
	private const float AdventureTutorialCalloutMaxTextWidth = 246.0f;
	private const float AdventureTutorialPlayCalloutMaxTextWidth = 306.0f;
	private const float AdventureTutorialCalloutMinTextWidth = 116.0f;
	private const float AdventureTutorialPlayCalloutMinTextWidth = 286.0f;
	private const float AdventureTutorialCalloutHorizontalPadding = 12.0f;
	private const float AdventureTutorialCalloutVerticalPadding = 8.0f;
	private const float AdventureTutorialCalloutButtonGap = 4.0f;
	private const float AdventureTutorialCalloutTextLineHeight = 21.0f;
	private const float AdventureTutorialCalloutButtonHeight = 28.0f;
	private const string AdventureTutorialFlowBasicCapture = "basic_capture";
	private const string AdventureTutorialFlowBombItem = "bomb_item";
	private static readonly Vector2 AdventureTutorialTargetPadding = new(10.0f, 10.0f);

	private static readonly AdventureTutorialTourTarget[] AdventureTutorialTourTargets =
	{
		AdventureTutorialTourTarget.Player,
		AdventureTutorialTourTarget.Enemy,
		AdventureTutorialTourTarget.Hand,
		AdventureTutorialTourTarget.Deck,
		AdventureTutorialTourTarget.Table,
	};

	private AdventureTutorialPhase _adventureTutorialPhase = AdventureTutorialPhase.None;
	private int _adventureTutorialTourIndex;
	private int _adventureTutorialCalloutGeneration;
	private bool _canAdvanceTutorialCallout;
	private Control? _adventureTutorialOverlayRoot;
	private ColorRect? _adventureTutorialInputBlocker;
	private Panel? _adventureTutorialTargetOutline;
	private Panel? _adventureTutorialCalloutPanel;
	private Label? _adventureTutorialCalloutLabel;
	private Button? _adventureTutorialContinueButton;
	private Line2D? _adventureTutorialPointerLine;
	private Tween? _adventureTutorialPulseTween;

	private enum AdventureTutorialPhase
	{
		None,
		BasicIntro,
		Tour,
		MatchSeven,
		MatchTwo,
		BombIntro,
		BombCollectReward,
		BombPlay,
		BombEnemyTrigger,
		Complete,
	}

	private enum AdventureTutorialTourTarget
	{
		Player,
		Enemy,
		Hand,
		Deck,
		Table,
	}

	private enum AdventureTutorialCalloutPlacement
	{
		Above,
		Below,
		Left,
		Right,
	}

	private bool IsAdventureTutorialEnabled()
	{
		return adventure_mode && adventure_tutorial_mode;
	}

	private static string NormalizeAdventureTutorialFlow(string flow)
	{
		return string.Equals(flow, AdventureTutorialFlowBombItem, StringComparison.Ordinal)
			? AdventureTutorialFlowBombItem
			: AdventureTutorialFlowBasicCapture;
	}

	private bool IsBombItemTutorialFlow()
	{
		return string.Equals(_adventureTutorialFlow, AdventureTutorialFlowBombItem, StringComparison.Ordinal);
	}

	private bool IsBasicCaptureTutorialCardStepActive()
	{
		return IsAdventureTutorialEnabled()
			&& (_adventureTutorialPhase == AdventureTutorialPhase.MatchSeven
				|| _adventureTutorialPhase == AdventureTutorialPhase.MatchTwo);
	}

	private bool IsAdventureTutorialTourActive()
	{
		return IsAdventureTutorialEnabled() && _adventureTutorialPhase == AdventureTutorialPhase.Tour;
	}

	private bool IsAdventureTutorialBasicIntroActive()
	{
		return IsAdventureTutorialEnabled() && _adventureTutorialPhase == AdventureTutorialPhase.BasicIntro;
	}

	private bool IsAdventureTutorialBombIntroActive()
	{
		return IsAdventureTutorialEnabled() && _adventureTutorialPhase == AdventureTutorialPhase.BombIntro;
	}

	private bool IsAdventureTutorialContinueCalloutActive()
	{
		return IsAdventureTutorialBasicIntroActive()
			|| IsAdventureTutorialTourActive()
			|| IsAdventureTutorialBombIntroActive();
	}

	private bool TryHandleAdventureTutorialContinueInput(InputEvent inputEvent)
	{
		if (!IsAdventureTutorialContinueCalloutActive() || !inputEvent.IsActionPressed(GameInputActions.GameConfirm))
		{
			return false;
		}

		PressAdventureTutorialContinue();
		return true;
	}

	private bool IsAdventureTutorialCardStepActive()
	{
		return IsAdventureTutorialEnabled()
			&& (_adventureTutorialPhase == AdventureTutorialPhase.MatchSeven
				|| _adventureTutorialPhase == AdventureTutorialPhase.MatchTwo
				|| _adventureTutorialPhase == AdventureTutorialPhase.BombCollectReward);
	}

	private bool IsAdventureTutorialItemStepActive()
	{
		return IsAdventureTutorialEnabled() && _adventureTutorialPhase == AdventureTutorialPhase.BombPlay;
	}

	private bool IsAdventureTutorialEnemyStepActive()
	{
		return IsAdventureTutorialEnabled() && _adventureTutorialPhase == AdventureTutorialPhase.BombEnemyTrigger;
	}

	private bool IsAdventureTutorialPlayerActionStepActive()
	{
		return IsAdventureTutorialCardStepActive() || IsAdventureTutorialItemStepActive();
	}

	private bool IsAdventureTutorialOverlayActive()
	{
		return IsAdventureTutorialContinueCalloutActive()
			|| IsAdventureTutorialPlayerActionStepActive()
			|| IsAdventureTutorialEnemyStepActive();
	}

	private void ResetAdventureTutorialState()
	{
		_adventureTutorialPhase = AdventureTutorialPhase.None;
		_adventureTutorialTourIndex = 0;
		_canAdvanceTutorialCallout = false;
		_adventureTutorialCalloutGeneration++;
		_adventureTutorialPulseTween?.Kill();
		_adventureTutorialPulseTween = null;

		if (_adventureTutorialOverlayRoot != null && GodotObject.IsInstanceValid(_adventureTutorialOverlayRoot))
		{
			RemoveAndQueueFreeChild(this, _adventureTutorialOverlayRoot);
		}

		_adventureTutorialOverlayRoot = null;
		_adventureTutorialInputBlocker = null;
		_adventureTutorialTargetOutline = null;
		_adventureTutorialCalloutPanel = null;
		_adventureTutorialCalloutLabel = null;
		_adventureTutorialContinueButton = null;
		_adventureTutorialPointerLine = null;
	}
}
