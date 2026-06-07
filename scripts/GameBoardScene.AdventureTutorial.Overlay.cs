using System;
using Godot;

public partial class GameBoardScene
{
	private void EnsureAdventureTutorialOverlay()
	{
		if (_adventureTutorialOverlayRoot != null && GodotObject.IsInstanceValid(_adventureTutorialOverlayRoot))
		{
			return;
		}

		var root = new Control
		{
			Name = "AdventureTutorialOverlay",
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = AdventureTutorialOverlayZIndex,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
		};
		AddChild(root);
		_adventureTutorialOverlayRoot = root;

		var blocker = new ColorRect
		{
			Name = "AdventureTutorialInputBlocker",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
			Color = new Color(0.0f, 0.04f, 0.06f, 0.03f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		root.AddChild(blocker);
		_adventureTutorialInputBlocker = blocker;

		var pointerLine = new Line2D
		{
			Name = "AdventureTutorialPointer",
			DefaultColor = new Color(1.0f, 0.88f, 0.42f, 0.92f),
			Width = 3.0f,
			ZIndex = 1,
		};
		root.AddChild(pointerLine);
		_adventureTutorialPointerLine = pointerLine;

		var targetOutline = new Panel
		{
			Name = "AdventureTutorialTargetOutline",
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 2,
		};
		targetOutline.AddThemeStyleboxOverride("panel", CreateAdventureTutorialTargetStyle());
		root.AddChild(targetOutline);
		_adventureTutorialTargetOutline = targetOutline;

		var calloutPanel = new Panel
		{
			Name = "AdventureTutorialCallout",
			MouseFilter = MouseFilterEnum.Stop,
			ZIndex = 3,
		};
		calloutPanel.AddThemeStyleboxOverride("panel", CreateAdventureTutorialCalloutStyle());
		root.AddChild(calloutPanel);
		_adventureTutorialCalloutPanel = calloutPanel;

		var label = new Label
		{
			Name = "AdventureTutorialText",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", 15);
		label.AddThemeColorOverride("font_color", new Color(0.96f, 1.0f, 0.94f, 1.0f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.92f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		calloutPanel.AddChild(label);
		_adventureTutorialCalloutLabel = label;

		var button = new Button
		{
			Name = "AdventureTutorialContinueButton",
			FocusMode = Control.FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		button.AddThemeFontSizeOverride("font_size", 13);
		button.Pressed += PressAdventureTutorialContinue;
		calloutPanel.AddChild(button);
		_adventureTutorialContinueButton = button;
	}

	private static StyleBoxFlat CreateAdventureTutorialTargetStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(1.0f, 0.82f, 0.20f, 0.08f),
			BorderColor = new Color(1.0f, 0.88f, 0.38f, 0.95f),
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ShadowColor = new Color(1.0f, 0.75f, 0.18f, 0.36f),
			ShadowSize = 12,
		};
	}

	private static StyleBoxFlat CreateAdventureTutorialCalloutStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.005f, 0.10f, 0.13f, 0.62f),
			BorderColor = new Color(0.92f, 0.84f, 0.42f, 0.50f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ShadowColor = new Color(0.0f, 0.02f, 0.04f, 0.36f),
			ShadowSize = 12,
			ShadowOffset = new Vector2(0.0f, 4.0f),
		};
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
		if (_adventureTutorialCalloutLabel == null || !GodotObject.IsInstanceValid(_adventureTutorialCalloutLabel))
		{
			return;
		}

		_adventureTutorialCalloutLabel.Text = Localize(GetAdventureTutorialTextKey());
		if (_adventureTutorialContinueButton != null && GodotObject.IsInstanceValid(_adventureTutorialContinueButton))
		{
			_adventureTutorialContinueButton.Text = Localize("GAME_TUTORIAL_CONTINUE");
			_adventureTutorialContinueButton.Visible = IsAdventureTutorialContinueCalloutActive();
		}
	}

	private string GetAdventureTutorialTextKey()
	{
		if (_adventureTutorialPhase == AdventureTutorialPhase.BombIntro)
		{
			return "GAME_TUTORIAL_ITEM_INTRO";
		}

		if (_adventureTutorialPhase == AdventureTutorialPhase.BombCollectReward)
		{
			return "GAME_TUTORIAL_ITEM_COLLECT_BOMB";
		}

		if (_adventureTutorialPhase == AdventureTutorialPhase.BombPlay)
		{
			return "GAME_TUTORIAL_ITEM_PLAY_BOMB";
		}

		if (_adventureTutorialPhase == AdventureTutorialPhase.BombEnemyTrigger)
		{
			return "GAME_TUTORIAL_ITEM_ENEMY_TRIGGER_BOMB";
		}

		if (_adventureTutorialPhase == AdventureTutorialPhase.BasicIntro)
		{
			return "GAME_TUTORIAL_BASIC_INTRO";
		}

		if (_adventureTutorialPhase == AdventureTutorialPhase.MatchSeven)
		{
			return "GAME_TUTORIAL_STEP_MATCH";
		}

		if (_adventureTutorialPhase == AdventureTutorialPhase.MatchTwo)
		{
			return "GAME_TUTORIAL_STEP_CONTINUE";
		}

		if (IsAdventureTutorialTourActive())
		{
			return AdventureTutorialTourTargets[Mathf.Clamp(_adventureTutorialTourIndex, 0, AdventureTutorialTourTargets.Length - 1)] switch
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
		if (_adventureTutorialOverlayRoot == null
			|| !GodotObject.IsInstanceValid(_adventureTutorialOverlayRoot)
			|| _adventureTutorialCalloutPanel == null
			|| !GodotObject.IsInstanceValid(_adventureTutorialCalloutPanel)
			|| _adventureTutorialTargetOutline == null
			|| !GodotObject.IsInstanceValid(_adventureTutorialTargetOutline)
			|| _adventureTutorialPointerLine == null
			|| !GodotObject.IsInstanceValid(_adventureTutorialPointerLine))
		{
			return;
		}

		bool active = IsAdventureTutorialOverlayActive();
		_adventureTutorialOverlayRoot.Visible = active;
		if (!active || !TryGetAdventureTutorialTargetRect(out Rect2 targetRect))
		{
			return;
		}

		if (_adventureTutorialInputBlocker != null && GodotObject.IsInstanceValid(_adventureTutorialInputBlocker))
		{
			_adventureTutorialInputBlocker.MouseFilter = IsAdventureTutorialContinueCalloutActive()
				? MouseFilterEnum.Stop
				: MouseFilterEnum.Ignore;
			_adventureTutorialInputBlocker.Visible = IsAdventureTutorialContinueCalloutActive();
		}

		Rect2 paddedTarget = new(
			targetRect.Position - AdventureTutorialTargetPadding,
			targetRect.Size + AdventureTutorialTargetPadding * 2.0f);
		_adventureTutorialTargetOutline.Position = paddedTarget.Position;
		_adventureTutorialTargetOutline.Size = paddedTarget.Size;

		Vector2 viewportSize = GetViewportRect().Size;
		Vector2 calloutSize = GetAdventureTutorialCalloutSize();
		_adventureTutorialCalloutPanel.Size = calloutSize;
		_adventureTutorialCalloutPanel.CustomMinimumSize = Vector2.Zero;
		LayoutAdventureTutorialCalloutContent(calloutSize);

		Vector2 targetCenter = targetRect.GetCenter();
		Vector2 calloutPosition = GetAdventureTutorialCalloutPosition(targetRect, calloutSize, viewportSize);
		_adventureTutorialCalloutPanel.Position = calloutPosition;

		Rect2 calloutRect = new(calloutPosition, calloutSize);
		Vector2 lineStart = GetClosestPointOnRect(calloutRect, targetCenter);
		_adventureTutorialPointerLine.Points = new[] { lineStart, targetCenter };
	}

	private Vector2 GetAdventureTutorialCalloutSize()
	{
		string text = _adventureTutorialCalloutLabel?.Text ?? Localize(GetAdventureTutorialTextKey());
		bool showButton = IsAdventureTutorialContinueCalloutActive();
		float maxTextWidth = showButton
			? AdventureTutorialCalloutMaxTextWidth
			: AdventureTutorialPlayCalloutMaxTextWidth;
		float viewportTextWidth = Mathf.Max(
			AdventureTutorialCalloutMinTextWidth,
			GetViewportRect().Size.X
				- AdventureTutorialViewportPadding * 2.0f
				- AdventureTutorialCalloutHorizontalPadding * 2.0f);
		maxTextWidth = Mathf.Min(maxTextWidth, viewportTextWidth);
		float minTextWidth = showButton
			? AdventureTutorialCalloutMinTextWidth
			: Mathf.Min(AdventureTutorialPlayCalloutMinTextWidth, maxTextWidth);
		float naturalTextWidth = Mathf.Max(minTextWidth, EstimateAdventureTutorialTextWidth(text));
		float textWidth = Mathf.Min(naturalTextWidth, maxTextWidth);
		int lineCount = Math.Max(1, (int)Math.Ceiling(naturalTextWidth / textWidth));
		float textHeight = lineCount * AdventureTutorialCalloutTextLineHeight;
		float width = textWidth + AdventureTutorialCalloutHorizontalPadding * 2.0f;
		float height = textHeight + AdventureTutorialCalloutVerticalPadding * 2.0f;

		if (showButton)
		{
			float buttonWidth = GetAdventureTutorialContinueButtonWidth();
			width = Mathf.Max(width, buttonWidth + AdventureTutorialCalloutHorizontalPadding * 2.0f);
			height += AdventureTutorialCalloutButtonGap + AdventureTutorialCalloutButtonHeight;
		}

		return new Vector2((float)Math.Ceiling(width), (float)Math.Ceiling(height));
	}

	private void LayoutAdventureTutorialCalloutContent(Vector2 calloutSize)
	{
		if (_adventureTutorialCalloutLabel == null || !GodotObject.IsInstanceValid(_adventureTutorialCalloutLabel))
		{
			return;
		}

		bool showButton = IsAdventureTutorialContinueCalloutActive();
		float textWidth = Mathf.Max(1.0f, calloutSize.X - AdventureTutorialCalloutHorizontalPadding * 2.0f);
		float textHeight = calloutSize.Y - AdventureTutorialCalloutVerticalPadding * 2.0f;
		if (showButton)
		{
			textHeight -= AdventureTutorialCalloutButtonGap + AdventureTutorialCalloutButtonHeight;
		}

		_adventureTutorialCalloutLabel.Position = new Vector2(
			AdventureTutorialCalloutHorizontalPadding,
			AdventureTutorialCalloutVerticalPadding);
		_adventureTutorialCalloutLabel.Size = new Vector2(textWidth, Mathf.Max(1.0f, textHeight));

		if (_adventureTutorialContinueButton == null || !GodotObject.IsInstanceValid(_adventureTutorialContinueButton))
		{
			return;
		}

		float buttonWidth = GetAdventureTutorialContinueButtonWidth();
		_adventureTutorialContinueButton.Size = new Vector2(buttonWidth, AdventureTutorialCalloutButtonHeight);
		_adventureTutorialContinueButton.Position = new Vector2(
			calloutSize.X - AdventureTutorialCalloutHorizontalPadding - buttonWidth,
			calloutSize.Y - AdventureTutorialCalloutVerticalPadding - AdventureTutorialCalloutButtonHeight);
	}

	private float GetAdventureTutorialContinueButtonWidth()
	{
		string text = _adventureTutorialContinueButton?.Text ?? Localize("GAME_TUTORIAL_CONTINUE");
		return Mathf.Max(56.0f, EstimateAdventureTutorialTextWidth(text) + 26.0f);
	}

	private static float EstimateAdventureTutorialTextWidth(string text)
	{
		float width = 0.0f;
		foreach (char character in text)
		{
			if (char.IsWhiteSpace(character))
			{
				width += 4.0f;
			}
			else if (character <= 0x007f)
			{
				width += 8.0f;
			}
			else
			{
				width += 15.0f;
			}
		}

		return width;
	}

	private Vector2 GetAdventureTutorialCalloutPosition(Rect2 targetRect, Vector2 calloutSize, Vector2 viewportSize)
	{
		if (TryGetAdventureTutorialFixedCalloutPosition(calloutSize, viewportSize, out Vector2 fixedPosition))
		{
			return fixedPosition;
		}

		AdventureTutorialCalloutPlacement[] placements = GetAdventureTutorialCalloutPlacementPreference();
		Vector2 bestPosition = ClampAdventureTutorialCalloutPosition(
			GetAdventureTutorialCalloutCandidatePosition(targetRect, calloutSize, placements[0]),
			calloutSize,
			viewportSize);
		float bestScore = float.MaxValue;
		Vector2 targetCenter = targetRect.GetCenter();

		for (int index = 0; index < placements.Length; index++)
		{
			Vector2 position = ClampAdventureTutorialCalloutPosition(
				GetAdventureTutorialCalloutCandidatePosition(targetRect, calloutSize, placements[index]),
				calloutSize,
				viewportSize);
			Rect2 calloutRect = new(position, calloutSize);
			float overlapArea = GetAdventureTutorialRectOverlapArea(calloutRect, targetRect);
			float distanceScore = calloutRect.GetCenter().DistanceTo(targetCenter) * 0.01f;
			float score = overlapArea * 100.0f + index * 10.0f + distanceScore;
			if (score < bestScore)
			{
				bestScore = score;
				bestPosition = position;
			}
		}

		return bestPosition;
	}

	private bool TryGetAdventureTutorialFixedCalloutPosition(Vector2 calloutSize, Vector2 viewportSize, out Vector2 position)
	{
		position = default;
		if (IsAdventureTutorialBombIntroActive())
		{
			if (!TryGetControlGlobalRect("%TableCardLayoutArea", out Rect2 introTableRect))
			{
				return false;
			}

			position = ClampAdventureTutorialCalloutPosition(
				introTableRect.GetCenter() - calloutSize * 0.5f,
				calloutSize,
				viewportSize);
			return true;
		}

		if (IsAdventureTutorialBasicIntroActive())
		{
			if (!TryGetControlGlobalRect("%TableCardLayoutArea", out Rect2 introTableRect))
			{
				return false;
			}

			position = ClampAdventureTutorialCalloutPosition(
				introTableRect.GetCenter() - calloutSize * 0.5f,
				calloutSize,
				viewportSize);
			return true;
		}

		if (!IsAdventureTutorialTourActive())
		{
			return false;
		}

		AdventureTutorialTourTarget target = AdventureTutorialTourTargets[
			Mathf.Clamp(_adventureTutorialTourIndex, 0, AdventureTutorialTourTargets.Length - 1)];
		if (target != AdventureTutorialTourTarget.Player
			&& target != AdventureTutorialTourTarget.Enemy
			&& target != AdventureTutorialTourTarget.Hand)
		{
			return false;
		}

		if (!TryGetControlGlobalRect("%TableCardLayoutArea", out Rect2 tableRect))
		{
			return false;
		}

		position = ClampAdventureTutorialCalloutPosition(
			tableRect.GetCenter() - calloutSize * 0.5f,
			calloutSize,
			viewportSize);
		return true;
	}

	private AdventureTutorialCalloutPlacement[] GetAdventureTutorialCalloutPlacementPreference()
	{
		if (IsAdventureTutorialPlayerActionStepActive())
		{
			return new[]
			{
				AdventureTutorialCalloutPlacement.Above,
				AdventureTutorialCalloutPlacement.Left,
				AdventureTutorialCalloutPlacement.Right,
				AdventureTutorialCalloutPlacement.Below,
			};
		}

		if (IsAdventureTutorialEnemyStepActive())
		{
			return new[]
			{
				AdventureTutorialCalloutPlacement.Below,
				AdventureTutorialCalloutPlacement.Left,
				AdventureTutorialCalloutPlacement.Right,
				AdventureTutorialCalloutPlacement.Above,
			};
		}

		AdventureTutorialTourTarget target = AdventureTutorialTourTargets[
			Mathf.Clamp(_adventureTutorialTourIndex, 0, AdventureTutorialTourTargets.Length - 1)];
		return target switch
		{
			AdventureTutorialTourTarget.Player => new[]
			{
				AdventureTutorialCalloutPlacement.Above,
				AdventureTutorialCalloutPlacement.Right,
				AdventureTutorialCalloutPlacement.Left,
				AdventureTutorialCalloutPlacement.Below,
			},
			AdventureTutorialTourTarget.Enemy => new[]
			{
				AdventureTutorialCalloutPlacement.Below,
				AdventureTutorialCalloutPlacement.Right,
				AdventureTutorialCalloutPlacement.Left,
				AdventureTutorialCalloutPlacement.Above,
			},
			AdventureTutorialTourTarget.Hand => new[]
			{
				AdventureTutorialCalloutPlacement.Above,
				AdventureTutorialCalloutPlacement.Right,
				AdventureTutorialCalloutPlacement.Left,
				AdventureTutorialCalloutPlacement.Below,
			},
			AdventureTutorialTourTarget.Deck => new[]
			{
				AdventureTutorialCalloutPlacement.Above,
				AdventureTutorialCalloutPlacement.Left,
				AdventureTutorialCalloutPlacement.Right,
				AdventureTutorialCalloutPlacement.Below,
			},
			_ => new[]
			{
				AdventureTutorialCalloutPlacement.Below,
				AdventureTutorialCalloutPlacement.Above,
				AdventureTutorialCalloutPlacement.Right,
				AdventureTutorialCalloutPlacement.Left,
			},
		};
	}

	private static Vector2 GetAdventureTutorialCalloutCandidatePosition(
		Rect2 targetRect,
		Vector2 calloutSize,
		AdventureTutorialCalloutPlacement placement)
	{
		Vector2 targetCenter = targetRect.GetCenter();
		return placement switch
		{
			AdventureTutorialCalloutPlacement.Below => new Vector2(
				targetCenter.X - calloutSize.X * 0.5f,
				targetRect.End.Y + AdventureTutorialCalloutGap),
			AdventureTutorialCalloutPlacement.Left => new Vector2(
				targetRect.Position.X - calloutSize.X - AdventureTutorialCalloutGap,
				targetCenter.Y - calloutSize.Y * 0.5f),
			AdventureTutorialCalloutPlacement.Right => new Vector2(
				targetRect.End.X + AdventureTutorialCalloutGap,
				targetCenter.Y - calloutSize.Y * 0.5f),
			_ => new Vector2(
				targetCenter.X - calloutSize.X * 0.5f,
				targetRect.Position.Y - calloutSize.Y - AdventureTutorialCalloutGap),
		};
	}

	private static Vector2 ClampAdventureTutorialCalloutPosition(Vector2 position, Vector2 calloutSize, Vector2 viewportSize)
	{
		float maxX = Mathf.Max(AdventureTutorialViewportPadding, viewportSize.X - calloutSize.X - AdventureTutorialViewportPadding);
		float maxY = Mathf.Max(AdventureTutorialViewportPadding, viewportSize.Y - calloutSize.Y - AdventureTutorialViewportPadding);
		return new Vector2(
			Mathf.Clamp(position.X, AdventureTutorialViewportPadding, maxX),
			Mathf.Clamp(position.Y, AdventureTutorialViewportPadding, maxY));
	}

	private static float GetAdventureTutorialRectOverlapArea(Rect2 first, Rect2 second)
	{
		float left = Mathf.Max(first.Position.X, second.Position.X);
		float top = Mathf.Max(first.Position.Y, second.Position.Y);
		float right = Mathf.Min(first.End.X, second.End.X);
		float bottom = Mathf.Min(first.End.Y, second.End.Y);
		if (right <= left || bottom <= top)
		{
			return 0.0f;
		}

		return (right - left) * (bottom - top);
	}

	private bool TryGetAdventureTutorialTargetRect(out Rect2 targetRect)
	{
		targetRect = default;
		if (IsAdventureTutorialBombIntroActive())
		{
			return TryGetControlGlobalRect("%TableCardLayoutArea", out targetRect);
		}

		if (IsAdventureTutorialBasicIntroActive())
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

		return AdventureTutorialTourTargets[Mathf.Clamp(_adventureTutorialTourIndex, 0, AdventureTutorialTourTargets.Length - 1)] switch
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

	private static Vector2 GetClosestPointOnRect(Rect2 rect, Vector2 point)
	{
		return new Vector2(
			Mathf.Clamp(point.X, rect.Position.X, rect.End.X),
			Mathf.Clamp(point.Y, rect.Position.Y, rect.End.Y));
	}

	private void PulseAdventureTutorialCallout()
	{
		if (_adventureTutorialCalloutPanel == null || !GodotObject.IsInstanceValid(_adventureTutorialCalloutPanel))
		{
			return;
		}

		_adventureTutorialPulseTween?.Kill();
		_adventureTutorialCalloutPanel.Modulate = Colors.White;
		_adventureTutorialPulseTween = _adventureTutorialCalloutPanel.CreateTween();
		_adventureTutorialPulseTween.SetTrans(Tween.TransitionType.Sine);
		_adventureTutorialPulseTween.SetEase(Tween.EaseType.InOut);
		_adventureTutorialPulseTween.TweenProperty(_adventureTutorialCalloutPanel, "modulate", new Color(1.0f, 0.92f, 0.52f, 1.0f), 0.10f);
		_adventureTutorialPulseTween.TweenProperty(_adventureTutorialCalloutPanel, "modulate", Colors.White, 0.16f);
	}
}
