using Godot;

public partial class GameBoardScene
{
	private void EnsureAdventureTutorialOverlay()
	{
		if (_adventureTutorialOverlay != null && GodotObject.IsInstanceValid(_adventureTutorialOverlay))
		{
			return;
		}

		PackedScene? overlayScene = GD.Load<PackedScene>(AdventureTutorialOverlayScenePath);
		if (overlayScene?.Instantiate() is not AdventureTutorialOverlay overlay)
		{
			GD.PushError($"Missing adventure tutorial overlay scene: {AdventureTutorialOverlayScenePath}");
			return;
		}

		AddChild(overlay);
		overlay.SetTweenFactory(CreateBoardTween);
		overlay.ContinueRequested += PressAdventureTutorialContinue;
		_adventureTutorialOverlay = overlay;
	}

	private static StyleBoxFlat CreateAdventureTutorialTableHighlightStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(1.0f, 0.85f, 0.24f, 0.08f),
			BorderColor = new Color(1.0f, 0.88f, 0.38f, 0.9f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 7,
			CornerRadiusTopRight = 7,
			CornerRadiusBottomRight = 7,
			CornerRadiusBottomLeft = 7,
			ShadowColor = new Color(1.0f, 0.75f, 0.18f, 0.32f),
			ShadowSize = 10,
		};
	}

	private void RefreshAdventureTutorialOverlayText()
	{
		if (_adventureTutorialOverlay == null || !GodotObject.IsInstanceValid(_adventureTutorialOverlay))
		{
			return;
		}

		_adventureTutorialOverlay.SetContent(
			Localize(GetAdventureTutorialTextKey()),
			Localize("GAME_TUTORIAL_CONTINUE"),
			IsAdventureTutorialContinueCalloutActive());
	}

	private string GetAdventureTutorialTextKey()
	{
		if (GetAdventureTutorialPhase() == AdventureTutorialPhase.BombIntro)
		{
			return "GAME_TUTORIAL_ITEM_INTRO";
		}

		if (GetAdventureTutorialPhase() == AdventureTutorialPhase.BombCollectReward)
		{
			return "GAME_TUTORIAL_ITEM_COLLECT_BOMB";
		}

		if (GetAdventureTutorialPhase() == AdventureTutorialPhase.BombPlay)
		{
			return "GAME_TUTORIAL_ITEM_PLAY_BOMB";
		}

		if (GetAdventureTutorialPhase() == AdventureTutorialPhase.BombEnemyTrigger)
		{
			return "GAME_TUTORIAL_ITEM_ENEMY_TRIGGER_BOMB";
		}

		if (GetAdventureTutorialPhase() == AdventureTutorialPhase.BasicIntro)
		{
			return "GAME_TUTORIAL_BASIC_INTRO";
		}

		if (GetAdventureTutorialPhase() == AdventureTutorialPhase.MatchSeven)
		{
			return "GAME_TUTORIAL_STEP_MATCH";
		}

		if (GetAdventureTutorialPhase() == AdventureTutorialPhase.MatchTwo)
		{
			return "GAME_TUTORIAL_STEP_CONTINUE";
		}

		if (IsAdventureTutorialTourActive())
		{
			return AdventureTutorialTourTargets[Mathf.Clamp(GetAdventureTutorialTourIndex(), 0, AdventureTutorialTourTargets.Length - 1)] switch
			{
				AdventureTutorialTourTarget.Player => "GAME_TUTORIAL_TOUR_PLAYER",
				AdventureTutorialTourTarget.Enemy => "GAME_TUTORIAL_TOUR_ENEMY",
				AdventureTutorialTourTarget.Hand => "GAME_TUTORIAL_TOUR_HAND",
				AdventureTutorialTourTarget.Deck => "GAME_TUTORIAL_TOUR_DECK",
				_ => "GAME_TUTORIAL_TOUR_TABLE",
			};
		}

		return "GAME_TUTORIAL_TOUR_TABLE";
	}

	private void RefreshAdventureTutorialOverlayLayout()
	{
		if (_adventureTutorialOverlay == null || !GodotObject.IsInstanceValid(_adventureTutorialOverlay))
		{
			return;
		}

		if (!IsAdventureTutorialOverlayActive() || !TryGetAdventureTutorialTargetRect(out Rect2 targetRect))
		{
			_adventureTutorialOverlay.HideOverlay();
			return;
		}

		Vector2? fixedCalloutCenter = TryGetAdventureTutorialFixedCalloutCenter(out Vector2 center)
			? center
			: null;
		_adventureTutorialOverlay.SetTargetLayout(
			targetRect,
			fixedCalloutCenter,
			GetAdventureTutorialCalloutPlacementPreference());
		_adventureTutorialOverlay.ShowOverlay();
	}

	private bool TryGetAdventureTutorialFixedCalloutCenter(out Vector2 center)
	{
		center = default;
		bool shouldCenterOnTable = IsAdventureTutorialBombIntroActive()
			|| IsAdventureTutorialBasicIntroActive();
		if (IsAdventureTutorialTourActive())
		{
			AdventureTutorialTourTarget target = AdventureTutorialTourTargets[
				Mathf.Clamp(GetAdventureTutorialTourIndex(), 0, AdventureTutorialTourTargets.Length - 1)];
			shouldCenterOnTable = target == AdventureTutorialTourTarget.Player
				|| target == AdventureTutorialTourTarget.Enemy
				|| target == AdventureTutorialTourTarget.Hand;
		}

		if (!shouldCenterOnTable || !TryGetControlGlobalRect("%TableCardLayoutArea", out Rect2 tableRect))
		{
			return false;
		}

		center = tableRect.GetCenter();
		return true;
	}

	private AdventureTutorialOverlay.CalloutPlacement[] GetAdventureTutorialCalloutPlacementPreference()
	{
		if (IsAdventureTutorialPlayerActionStepActive())
		{
			return new[]
			{
				AdventureTutorialOverlay.CalloutPlacement.Above,
				AdventureTutorialOverlay.CalloutPlacement.Left,
				AdventureTutorialOverlay.CalloutPlacement.Right,
				AdventureTutorialOverlay.CalloutPlacement.Below,
			};
		}

		if (IsAdventureTutorialEnemyStepActive())
		{
			return new[]
			{
				AdventureTutorialOverlay.CalloutPlacement.Below,
				AdventureTutorialOverlay.CalloutPlacement.Left,
				AdventureTutorialOverlay.CalloutPlacement.Right,
				AdventureTutorialOverlay.CalloutPlacement.Above,
			};
		}

		AdventureTutorialTourTarget target = AdventureTutorialTourTargets[
			Mathf.Clamp(GetAdventureTutorialTourIndex(), 0, AdventureTutorialTourTargets.Length - 1)];
		return target switch
		{
			AdventureTutorialTourTarget.Player => new[]
			{
				AdventureTutorialOverlay.CalloutPlacement.Above,
				AdventureTutorialOverlay.CalloutPlacement.Right,
				AdventureTutorialOverlay.CalloutPlacement.Left,
				AdventureTutorialOverlay.CalloutPlacement.Below,
			},
			AdventureTutorialTourTarget.Enemy => new[]
			{
				AdventureTutorialOverlay.CalloutPlacement.Below,
				AdventureTutorialOverlay.CalloutPlacement.Right,
				AdventureTutorialOverlay.CalloutPlacement.Left,
				AdventureTutorialOverlay.CalloutPlacement.Above,
			},
			AdventureTutorialTourTarget.Hand => new[]
			{
				AdventureTutorialOverlay.CalloutPlacement.Above,
				AdventureTutorialOverlay.CalloutPlacement.Right,
				AdventureTutorialOverlay.CalloutPlacement.Left,
				AdventureTutorialOverlay.CalloutPlacement.Below,
			},
			AdventureTutorialTourTarget.Deck => new[]
			{
				AdventureTutorialOverlay.CalloutPlacement.Above,
				AdventureTutorialOverlay.CalloutPlacement.Left,
				AdventureTutorialOverlay.CalloutPlacement.Right,
				AdventureTutorialOverlay.CalloutPlacement.Below,
			},
			_ => new[]
			{
				AdventureTutorialOverlay.CalloutPlacement.Below,
				AdventureTutorialOverlay.CalloutPlacement.Above,
				AdventureTutorialOverlay.CalloutPlacement.Right,
				AdventureTutorialOverlay.CalloutPlacement.Left,
			},
		};
	}

	private bool TryGetAdventureTutorialTargetRect(out Rect2 targetRect)
	{
		targetRect = default;
		if (IsAdventureTutorialBombIntroActive() || IsAdventureTutorialBasicIntroActive())
		{
			return TryGetControlGlobalRect("%TableCardLayoutArea", out targetRect);
		}

		if (IsAdventureTutorialCardStepActive())
		{
			if (TryGetAdventureTutorialAllowedHandIndex(out int handIndex)
				&& TryGetHandCardGlobalRect(MainPlayerIndex, handIndex, out targetRect))
			{
				return true;
			}

			return TryGetControlGlobalRect("%Player1HandArea", out targetRect);
		}

		if (IsAdventureTutorialItemStepActive())
		{
			if (TryGetAdventureTutorialAllowedItemDisplayIndex(out int displayIndex)
				&& TryGetHandCardGlobalRect(MainPlayerIndex, displayIndex, out targetRect))
			{
				return true;
			}

			return TryGetControlGlobalRect("%Player1HandArea", out targetRect);
		}

		if (IsAdventureTutorialEnemyStepActive())
		{
			return TryGetControlGlobalRect("%Player2HandArea", out targetRect)
				|| TryGetControlGlobalRect("%TableCardLayoutArea", out targetRect);
		}

		if (!IsAdventureTutorialTourActive())
		{
			return false;
		}

		return AdventureTutorialTourTargets[Mathf.Clamp(GetAdventureTutorialTourIndex(), 0, AdventureTutorialTourTargets.Length - 1)] switch
		{
			AdventureTutorialTourTarget.Player => TryGetMergedControlGlobalRect("%Player1AvatarArea", "%Player1ScoreMeter", out targetRect),
			AdventureTutorialTourTarget.Enemy => TryGetMergedControlGlobalRect("%Player2AvatarArea", "%Player2ScoreMeter", out targetRect),
			AdventureTutorialTourTarget.Hand => TryGetControlGlobalRect("%Player1HandArea", out targetRect),
			AdventureTutorialTourTarget.Deck => TryGetControlGlobalRect("%Player1CardPileArea", out targetRect),
			_ => TryGetControlGlobalRect("%TableCardLayoutArea", out targetRect),
		};
	}

	private bool TryGetControlGlobalRect(string nodePath, out Rect2 rect)
	{
		rect = default;
		if (GetNodeOrNull<Control>(nodePath) is not Control control)
		{
			return false;
		}

		rect = control.GetGlobalRect();
		return true;
	}

	private bool TryGetMergedControlGlobalRect(string firstNodePath, string secondNodePath, out Rect2 rect)
	{
		if (!TryGetControlGlobalRect(firstNodePath, out Rect2 firstRect)
			|| !TryGetControlGlobalRect(secondNodePath, out Rect2 secondRect))
		{
			rect = default;
			return false;
		}

		rect = MergeRects(firstRect, secondRect);
		return true;
	}

	private bool TryGetHandCardGlobalRect(int playerIndex, int handIndex, out Rect2 rect)
	{
		rect = default;
		if (playerIndex < 0 || playerIndex >= _playerHandAreas.Count)
		{
			return false;
		}

		Control? cardSlot = _playerHandAreas[playerIndex].GetNodeOrNull<Control>($"HandCard_{handIndex}");
		if (cardSlot == null)
		{
			return false;
		}

		rect = cardSlot.GetGlobalRect();
		return true;
	}

	private static Rect2 MergeRects(Rect2 first, Rect2 second)
	{
		Vector2 position = new(Mathf.Min(first.Position.X, second.Position.X), Mathf.Min(first.Position.Y, second.Position.Y));
		Vector2 end = new(Mathf.Max(first.End.X, second.End.X), Mathf.Max(first.End.Y, second.End.Y));
		return new Rect2(position, end - position);
	}

	private void PulseAdventureTutorialCallout()
	{
		if (_adventureTutorialOverlay != null && GodotObject.IsInstanceValid(_adventureTutorialOverlay))
		{
			_adventureTutorialOverlay.Pulse();
		}
	}
}
