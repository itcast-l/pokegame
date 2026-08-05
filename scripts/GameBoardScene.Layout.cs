using System;
using Godot;

public partial class GameBoardScene
{
	private static readonly Vector2 OpponentHorizontalHandCardOffset = new(45, 0);
	private static readonly Vector2 VerticalHandCardOffset = new(0, 35);
	private GameBoardLayoutVariant? _currentBoardLayoutVariant;
	private bool _responsiveBoardVisualsDirty;
	private Rect2[]? _authoredStandardAvatarAreas;
	private Rect2[]? _authoredStandardScoreMeters;
	private Vector2[]? _authoredStandardScoreEffectAnchors;

	private void RefreshResponsiveBoardLayout()
	{
		if (_boardCanvas == null
			|| !GodotObject.IsInstanceValid(_boardCanvas)
			|| _boardArt == null
			|| !GodotObject.IsInstanceValid(_boardArt)
			|| _verticalBackdrop == null
			|| !GodotObject.IsInstanceValid(_verticalBackdrop)
			|| _layoutAreas == null
			|| !GodotObject.IsInstanceValid(_layoutAreas))
		{
			return;
		}

		CaptureAuthoredStandardAvatarLayout();
		GameBoardLayoutState state = GameBoardLayoutGeometry.BuildLayoutState(
			Size,
			_currentBoardLayoutVariant);
		_currentBoardLayoutVariant = state.Variant;

		ApplyControlRect(_boardCanvas, state.BoardCanvasRect, clearMinimumSize: true);
		_boardArt.Visible = state.Variant == GameBoardLayoutVariant.Standard;
		_verticalBackdrop.Visible = state.Variant == GameBoardLayoutVariant.VerticalComposite;

		_layoutAreas.SetAnchorsPreset(LayoutPreset.TopLeft);
		_layoutAreas.Position = state.DesignOffset;
		_layoutAreas.Size = GameBoardLayoutGeometry.DesignSize;
		_layoutAreas.Scale = Vector2.One * state.DesignScale;
		ApplyBoardLayoutProfile(state.Profile, state.Variant);
	}

	private void CaptureAuthoredStandardAvatarLayout()
	{
		if (_authoredStandardAvatarAreas != null
			|| _layoutAreas == null
			|| !GodotObject.IsInstanceValid(_layoutAreas))
		{
			return;
		}

		_authoredStandardAvatarAreas = new Rect2[MaxPlayers];
		_authoredStandardScoreMeters = new Rect2[MaxPlayers];
		_authoredStandardScoreEffectAnchors = new Vector2[MaxPlayers];

		Rect2 designRect = new(Vector2.Zero, GameBoardLayoutGeometry.DesignSize);
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control playerArea = _nodeBindings.PlayerAreas[playerIndex];
			Control avatarArea = _nodeBindings.AvatarAreas[playerIndex];
			Control scoreMeter = _nodeBindings.ScoreMeters[playerIndex];
			Marker2D scoreEffectTarget = _nodeBindings.ScoreEffectTargets[playerIndex];
			Rect2 playerRect = GetAuthoredControlRect(playerArea, designRect);
			_authoredStandardAvatarAreas[playerIndex] = GetAuthoredControlRect(avatarArea, playerRect);
			_authoredStandardScoreMeters[playerIndex] = GetAuthoredControlRect(scoreMeter, playerRect);
			_authoredStandardScoreEffectAnchors[playerIndex] = playerRect.Position + scoreEffectTarget.Position;
		}
	}

	private static Rect2 GetAuthoredControlRect(Control control, Rect2 parentRect)
	{
		Vector2 topLeft = parentRect.Position
			+ parentRect.Size * new Vector2(control.AnchorLeft, control.AnchorTop)
			+ new Vector2(control.OffsetLeft, control.OffsetTop);
		Vector2 bottomRight = parentRect.Position
			+ parentRect.Size * new Vector2(control.AnchorRight, control.AnchorBottom)
			+ new Vector2(control.OffsetRight, control.OffsetBottom);
		Vector2 unscaledSize = bottomRight - topLeft;
		Vector2 scaledPosition = topLeft + control.PivotOffset - control.PivotOffset * control.Scale;
		return new Rect2(scaledPosition, unscaledSize * control.Scale);
	}

	private void ApplyBoardLayoutProfile(GameBoardLayoutProfile profile, GameBoardLayoutVariant variant)
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control playerArea = _nodeBindings.PlayerAreas[playerIndex];
			ApplyControlRect(
				playerArea,
				new Rect2(Vector2.Zero, GameBoardLayoutGeometry.DesignSize),
				clearMinimumSize: true);

			Control avatarArea = _nodeBindings.AvatarAreas[playerIndex];
			Rect2 avatarRect = variant == GameBoardLayoutVariant.Standard && _authoredStandardAvatarAreas != null
				? _authoredStandardAvatarAreas[playerIndex]
				: profile.AvatarAreas[playerIndex];
			ApplyControlRect(avatarArea, avatarRect, clearMinimumSize: true);
			avatarArea.Scale = Vector2.One;

			Control scoreMeter = _nodeBindings.ScoreMeters[playerIndex];
			Rect2 scoreMeterRect = variant == GameBoardLayoutVariant.Standard && _authoredStandardScoreMeters != null
				? _authoredStandardScoreMeters[playerIndex]
				: profile.ScoreMeters[playerIndex];
			ApplyControlRect(scoreMeter, scoreMeterRect, clearMinimumSize: false);
			scoreMeter.PivotOffset = scoreMeter.Size / 2.0f;

			Marker2D scoreEffectTarget = _nodeBindings.ScoreEffectTargets[playerIndex];
			scoreEffectTarget.Position = variant == GameBoardLayoutVariant.Standard && _authoredStandardScoreEffectAnchors != null
				? _authoredStandardScoreEffectAnchors[playerIndex]
				: profile.ScoreEffectAnchors[playerIndex];

			Control handArea = _nodeBindings.HandAreas[playerIndex];
			ApplyControlRect(handArea, profile.HandAreas[playerIndex], clearMinimumSize: true);

			Control pileArea = _nodeBindings.CardPileAreas[playerIndex];
			ApplyControlRect(pileArea, profile.CardPileAreas[playerIndex], clearMinimumSize: true);
		}

		ApplyControlRect(_nodeBindings.TableCardLayoutArea, profile.TableCardArea, clearMinimumSize: true);
		ApplyControlRect(_nodeBindings.QuestionCardTransformArea, profile.QuestionCardArea, clearMinimumSize: true);
		ApplyControlRect(_nodeBindings.CenterArena, profile.CenterArena, clearMinimumSize: true);
		if (_questionCardTransformArea != null && GodotObject.IsInstanceValid(_questionCardTransformArea))
		{
			_questionCardTransformArea.PivotOffset = _questionCardTransformArea.Size / 2.0f;
		}
	}

	private static void ApplyControlRect(Control control, Rect2 rect, bool clearMinimumSize)
	{
		control.SetAnchorsPreset(LayoutPreset.TopLeft);
		control.Position = rect.Position;
		if (clearMinimumSize)
		{
			control.CustomMinimumSize = Vector2.Zero;
		}
		control.Size = rect.Size;
		control.Scale = Vector2.One;
	}

	private void MarkResponsiveBoardVisualsDirty()
	{
		_responsiveBoardVisualsDirty = true;
		RequestBoardFrameUpdate();
	}

	private void FlushResponsiveBoardVisualsIfNeeded()
	{
		if (!_responsiveBoardVisualsDirty)
		{
			return;
		}

		_responsiveBoardVisualsDirty = false;
		RefreshResponsiveBoardVisuals();
	}

	private void CreatePlayerCardPiles()
	{
		_playerCardPiles.Clear();
		Array.Clear(_playerCardPileVisualBindings);

		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			int viewSlotIndex = GetPlayerViewSlotIndex(playerIndex);
			Control pileArea = _nodeBindings.CardPileAreas[viewSlotIndex];
			var pile = new Control
			{
				Name = $"Player{playerIndex + 1}CardPile",
				MouseFilter = MouseFilterEnum.Stop,
				AnchorLeft = 0.5f,
				AnchorTop = 0.5f,
				AnchorRight = 0.5f,
				AnchorBottom = 0.5f,
				OffsetLeft = -CardPileSize.X / 2.0f,
				OffsetTop = -CardPileSize.Y / 2.0f,
				OffsetRight = CardPileSize.X / 2.0f,
				OffsetBottom = CardPileSize.Y / 2.0f,
			};
			int capturedPlayerIndex = playerIndex;
			pile.MouseEntered += () => SetCardPileCountVisible(capturedPlayerIndex, true);
			pile.MouseExited += () => SetCardPileCountVisible(capturedPlayerIndex, false);

			pileArea.AddChild(pile);
			_playerCardPiles.Add(pile);
			GetOrCreatePlayerCardPileVisuals(playerIndex, pile);
		}
	}

	private void CreatePlayerHandAreas()
	{
		_playerHandAreas.Clear();

		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			int viewSlotIndex = GetPlayerViewSlotIndex(playerIndex);
			Control handArea = _nodeBindings.HandAreas[viewSlotIndex];
			_playerHandAreas.Add(handArea);
		}
	}

	private void BindQuestionCardTransformArea()
	{
		_questionCardTransformArea = _nodeBindings.QuestionCardTransformArea;
		_questionCardTransformArea.PivotOffset = _questionCardTransformArea.Size / 2.0f;
		_questionCardTransformArea.Visible = false;

		var styleBox = new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.30f, 0.38f, 0.16f),
			BorderColor = new Color(0.52f, 0.98f, 1.0f, 0.58f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ShadowColor = new Color(0.0f, 0.78f, 0.92f, 0.18f),
			ShadowSize = 10,
		};
		if (_questionCardTransformArea is Panel panel)
		{
			panel.AddThemeStyleboxOverride("panel", styleBox);
		}
	}

	private Vector2 GetHandAreaSize(int playerIndex)
	{
		Vector2 cardOffset = GetHandCardOffset(playerIndex);
		return new Vector2(
			CardVisualSize.X + Math.Abs(cardOffset.X) * (StartingHandCards - 1),
			CardVisualSize.Y + Math.Abs(cardOffset.Y) * (StartingHandCards - 1));
	}

	private Vector2 GetHandCardOffset(int playerIndex)
	{
		return GetPlayerViewSlotIndex(playerIndex) switch
		{
			2 or 3 => VerticalHandCardOffset,
			1 => OpponentHorizontalHandCardOffset,
			_ => HorizontalHandCardOffset,
		};
	}

	private Vector2 GetHandCardOffset(int playerIndex, int cardCount)
	{
		return GetHandCardOffset(playerIndex, cardCount, GetHandAreaSize(playerIndex));
	}

	private Vector2 GetHandCardOffset(int playerIndex, int cardCount, Vector2 handAreaSize)
	{
		Vector2 baseOffset = GetHandCardOffset(playerIndex);
		if (cardCount <= 1)
		{
			return baseOffset;
		}

		if (!Mathf.IsZeroApprox(baseOffset.X))
		{
			float availableSpacing = Math.Max(0.0f, handAreaSize.X - CardVisualSize.X);
			float spacing = Math.Min(Math.Abs(baseOffset.X), availableSpacing / (cardCount - 1));
			return new Vector2(baseOffset.X < 0.0f ? -spacing : spacing, baseOffset.Y);
		}

		float availableVerticalSpacing = Math.Max(0.0f, handAreaSize.Y - CardVisualSize.Y);
		float verticalSpacing = Math.Min(Math.Abs(baseOffset.Y), availableVerticalSpacing / (cardCount - 1));
		return new Vector2(baseOffset.X, baseOffset.Y < 0.0f ? -verticalSpacing : verticalSpacing);
	}

	private Vector2 GetHandFirstCardPosition(int playerIndex, int cardCount, Vector2 handAreaSize, Vector2 cardOffset)
	{
		if (cardCount <= 0)
		{
			return Vector2.Zero;
		}

		Vector2 groupSize = new(
			CardVisualSize.X + Math.Abs(cardOffset.X) * (cardCount - 1),
			CardVisualSize.Y + Math.Abs(cardOffset.Y) * (cardCount - 1));
		Vector2 startPosition = (handAreaSize - groupSize) / 2.0f;
		if (cardOffset.X < 0.0f)
		{
			startPosition.X += groupSize.X - CardVisualSize.X;
		}
		if (cardOffset.Y < 0.0f)
		{
			startPosition.Y += groupSize.Y - CardVisualSize.Y;
		}

		return startPosition;
	}

	private void SetCardPileCountVisible(int playerIndex, bool visible)
	{
		Label? countLabel = GetPlayerCardPileCountLabel(playerIndex);
		if (countLabel != null)
		{
			countLabel.Visible = visible;
		}
	}

	private void UpdatePlayerVisibility()
	{
		_bottomPlayerArea.Visible = player_count >= 1;
		_topPlayerArea.Visible = player_count >= 2;
		_leftPlayerArea.Visible = player_count >= 3;
		_rightPlayerArea.Visible = player_count >= 4;
		MarkSeatControlIndicatorLayoutDirty();
		MarkActivePlayerStatusTooltipLayoutDirty();
	}
}
