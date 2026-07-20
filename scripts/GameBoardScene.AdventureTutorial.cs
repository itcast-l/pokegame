using System;
using Godot;

public partial class GameBoardScene
{
	private const double AdventureTutorialCalloutReadSeconds = 1.2;
	private const string AdventureTutorialOverlayScenePath = "res://scenes/adventure_tutorial_overlay.tscn";
	private const string AdventureTutorialTableHighlightNodeName = "AdventureTutorialTableHighlight";
	private const string AdventureTutorialFlowBasicCapture = "basic_capture";
	private const string AdventureTutorialFlowBombItem = "bomb_item";

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
	private AdventureTutorialOverlay? _adventureTutorialOverlay;

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

		if (_adventureTutorialOverlay != null && GodotObject.IsInstanceValid(_adventureTutorialOverlay))
		{
			_adventureTutorialOverlay.HideOverlay();
			RemoveAndQueueFreeChild(this, _adventureTutorialOverlay);
		}

		_adventureTutorialOverlay = null;
	}
}
