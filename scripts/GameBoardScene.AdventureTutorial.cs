using System;
using Godot;

public partial class GameBoardScene
{
	private const double AdventureTutorialCalloutReadSeconds = 1.2;
	private const string AdventureTutorialOverlayScenePath = "res://scenes/adventure_tutorial_overlay.tscn";
	private const string AdventureTutorialTableHighlightNodeName = "AdventureTutorialTableHighlight";

	private static readonly AdventureTutorialTourTarget[] AdventureTutorialTourTargets =
	{
		AdventureTutorialTourTarget.Player,
		AdventureTutorialTourTarget.Enemy,
		AdventureTutorialTourTarget.Hand,
		AdventureTutorialTourTarget.Deck,
		AdventureTutorialTourTarget.Table,
	};

	private AdventureTutorialPhase GetAdventureTutorialPhase() =>
		(AdventureTutorialPhase)CurrentMatchFlowState.ScriptedSequenceStep;
	private int GetAdventureTutorialTourIndex() =>
		CurrentMatchFlowState.ScriptedTourIndex;
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

	private bool IsBombItemTutorialFlow()
	{
		return RuntimeConfiguration.TutorialFlow == AdventureTutorialFlowKind.BombItem;
	}

	private bool IsBasicCaptureTutorialCardStepActive()
	{
		return IsAdventureTutorialEnabled()
			&& (GetAdventureTutorialPhase() == AdventureTutorialPhase.MatchSeven
				|| GetAdventureTutorialPhase() == AdventureTutorialPhase.MatchTwo);
	}

	private bool IsAdventureTutorialTourActive()
	{
		return IsAdventureTutorialEnabled() && GetAdventureTutorialPhase() == AdventureTutorialPhase.Tour;
	}

	private bool IsAdventureTutorialBasicIntroActive()
	{
		return IsAdventureTutorialEnabled() && GetAdventureTutorialPhase() == AdventureTutorialPhase.BasicIntro;
	}

	private bool IsAdventureTutorialBombIntroActive()
	{
		return IsAdventureTutorialEnabled() && GetAdventureTutorialPhase() == AdventureTutorialPhase.BombIntro;
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
			&& (GetAdventureTutorialPhase() == AdventureTutorialPhase.MatchSeven
				|| GetAdventureTutorialPhase() == AdventureTutorialPhase.MatchTwo
				|| GetAdventureTutorialPhase() == AdventureTutorialPhase.BombCollectReward);
	}

	private bool IsAdventureTutorialItemStepActive()
	{
		return IsAdventureTutorialEnabled() && GetAdventureTutorialPhase() == AdventureTutorialPhase.BombPlay;
	}

	private bool IsAdventureTutorialEnemyStepActive()
	{
		return IsAdventureTutorialEnabled() && GetAdventureTutorialPhase() == AdventureTutorialPhase.BombEnemyTrigger;
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
		TryUpdateMatchFlowState(state => state with
		{
			ScriptedSequenceStep = (int)AdventureTutorialPhase.None,
			ScriptedTourIndex = 0,
		});
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
