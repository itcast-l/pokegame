using Godot;

public partial class GameBoardScene
{
	private const string DisabledCardProhibitedTexturePath = "res://assets/ui/disabled_card_prohibited_mark.png";
	private static readonly StyleBoxFlat MagnetSelectionPulseStyle = CreateMagnetSelectionPulseStyle();
	private static readonly Texture2D? DisabledCardProhibitedTexture = GD.Load<Texture2D>(DisabledCardProhibitedTexturePath);
	private static readonly Vector2 HandCardDescriptionLabelSize = new(212.0f, 50.0f);
	private static readonly Vector2 TableCardDescriptionLabelSize = new(224.0f, 52.0f);

	private int GetVisibleHandCardCountForRender(int playerIndex, int handCount)
	{
		if (!_isOpeningDealAnimationPlaying)
		{
			return handCount;
		}

		if (playerIndex < 0 || playerIndex >= _openingVisibleHandCardCounts.Count)
		{
			return 0;
		}

		return Mathf.Clamp(_openingVisibleHandCardCounts[playerIndex], 0, handCount);
	}

	private int GetVisiblePileCardCountForRender(int playerIndex, int pileCount)
	{
		if (!_isOpeningDealAnimationPlaying)
		{
			return pileCount;
		}

		if (playerIndex < 0 || playerIndex >= _openingVisiblePileCardCounts.Count)
		{
			return 0;
		}

		return Mathf.Clamp(_openingVisiblePileCardCounts[playerIndex], 0, pileCount);
	}

	private string GetAttachmentCardDescription(PlayingCard card, bool showCardBack)
	{
		if (showCardBack)
		{
			return string.Empty;
		}

		foreach (string itemCardId in card.GetAttachedItemCardIds())
		{
			if (_itemCardDescriptions.TryGetValue(itemCardId, out string? description))
			{
				return description;
			}
		}

		return string.Empty;
	}

	private string GetHandCardDescription(PlayingCard card, bool showCardBack)
	{
		string attachmentDescription = GetAttachmentCardDescription(card, showCardBack);
		if (!string.IsNullOrEmpty(attachmentDescription))
		{
			return attachmentDescription;
		}

		return showCardBack || !ShouldPlayedCardCollectTable(card)
			? string.Empty
			: Localize("GAME_CARD_COLLECTS_TABLE");
	}

	private static Label CreateCardDescriptionLabel(string text, Vector2 position, Vector2 size)
	{
		var label = new Label
		{
			Name = "CardDescriptionLabel",
			Text = text,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = position,
			Size = size,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Visible = false,
			ZAsRelative = false,
			ZIndex = TableCardPointPopupZIndex - 10,
		};
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", new Color(0.36f, 0.96f, 1.0f, 1.0f));
		label.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.05f, 0.09f, 0.92f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.03f, 0.05f, 0.95f));
		label.AddThemeConstantOverride("outline_size", 2);
		label.AddThemeConstantOverride("shadow_offset_x", 0);
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		return label;
	}

	private Vector2 GetHandCardDescriptionPosition(int playerIndex)
	{
		var labelSize = HandCardDescriptionLabelSize;
		float y = GetPlayerViewSlotIndex(playerIndex) == 1 ? CardVisualSize.Y + 4.0f : -labelSize.Y - 4.0f;
		return new Vector2((CardVisualSize.X - labelSize.X) / 2.0f, y);
	}

	private Vector2 GetTableCardDescriptionPosition(Vector2 cardPosition, Vector2 labelSize)
	{
		float x = Mathf.Clamp(
			cardPosition.X + (CardVisualSize.X - labelSize.X) / 2.0f,
			0.0f,
			Mathf.Max(0.0f, _centerArena.Size.X - labelSize.X));
		float y = cardPosition.Y - labelSize.Y - 4.0f;
		if (y < 0.0f)
		{
			y = cardPosition.Y + CardVisualSize.Y + 4.0f;
		}

		return new Vector2(x, y);
	}

	private static void AddMagnetSelectionPulse(Control parent, Vector2 position, Vector2 size, float rotationDegrees)
	{
		Vector2 glowInset = new(7.0f, 8.0f);
		Vector2 glowSize = size + glowInset * 2.0f;
		var glow = new Panel
		{
			Name = "MagnetSelectionPulse",
			MouseFilter = MouseFilterEnum.Ignore,
			Position = position - glowInset,
			Size = glowSize,
			PivotOffset = glowSize / 2.0f,
			RotationDegrees = rotationDegrees,
			ZIndex = -1,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.74f),
			Scale = Vector2.One * 0.98f,
		};
		glow.AddThemeStyleboxOverride("panel", MagnetSelectionPulseStyle);
		parent.AddChild(glow);

		Tween tween = glow.CreateTween();
		tween.SetLoops();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(glow, "modulate", new Color(1.0f, 1.0f, 1.0f, 1.0f), 0.48f);
		tween.TweenProperty(glow, "scale", Vector2.One * 1.04f, 0.48f);
		tween.Chain().TweenProperty(glow, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.68f), 0.48f);
		tween.TweenProperty(glow, "scale", Vector2.One * 0.98f, 0.48f);
	}

	private static void StartMagnetSelectionBounce(Control cardSlot, Vector2 basePosition)
	{
		Tween tween = cardSlot.CreateTween();
		tween.SetLoops();
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(cardSlot, "position", basePosition + new Vector2(0.0f, -3.0f), 0.42f);
		tween.TweenProperty(cardSlot, "position", basePosition, 0.42f);
	}

	private static StyleBoxFlat CreateMagnetSelectionPulseStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.0f, 0.0f, 0.0f, 0.0f),
			BorderColor = new Color(0.68f, 1.0f, 0.96f, 0.62f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6,
			ShadowColor = new Color(0.34f, 1.0f, 0.96f, 0.24f),
			ShadowSize = 16,
		};
	}

	private Texture2D? GetCardAttachmentOverlayTexture(PlayingCard card)
	{
		Texture2D? overlayTexture = null;
		int bestPriority = int.MinValue;

		foreach (string itemCardId in card.GetAttachedItemCardIds())
		{
			if (!_itemAttachmentOverlaysById.TryGetValue(itemCardId, out ItemAttachmentOverlay candidate)
				|| candidate.Priority < bestPriority)
			{
				continue;
			}

			overlayTexture = candidate.Texture;
			bestPriority = candidate.Priority;
		}

		return overlayTexture;
	}

	private static void AddCardAttachmentOverlay(
		Node parent,
		Texture2D overlayTexture,
		Vector2 position,
		Vector2 size,
		float rotationDegrees = 0.0f,
		int zIndex = 1,
		float alpha = 1.0f)
	{
		var overlay = new TextureRect
		{
			Name = "CardAttachmentOverlay",
			Texture = overlayTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = position,
			Size = size,
			PivotOffset = size / 2.0f,
			RotationDegrees = rotationDegrees,
			ZIndex = zIndex,
			Modulate = new Color(1.0f, 1.0f, 1.0f, alpha),
		};
		parent.AddChild(overlay);
	}

	private static void AddDisabledCardProhibitedMark(Node parent, Vector2 position, Vector2 size, float rotationDegrees, int zIndex)
	{
		var mark = new DisabledCardProhibitedMark
		{
			Name = "DisabledCardProhibitedMark",
			MouseFilter = MouseFilterEnum.Ignore,
			Position = position,
			Size = size,
			PivotOffset = size / 2.0f,
			RotationDegrees = rotationDegrees,
			ZIndex = zIndex,
			MarkerTexture = DisabledCardProhibitedTexture,
		};
		parent.AddChild(mark);
	}

	private partial class DisabledCardProhibitedMark : Control
	{
		public Texture2D? MarkerTexture { get; set; }

		public override void _Draw()
		{
			Vector2 center = Size / 2.0f;
			float radius = Mathf.Min(Size.X, Size.Y) * 0.26f;
			float slashOffset = radius * 0.72f;
			float strokeWidth = Mathf.Max(4.0f, Mathf.Min(Size.X, Size.Y) * 0.055f);
			var shadowColor = new Color(0.02f, 0.0f, 0.0f, 0.42f);
			var rimColor = new Color(1.0f, 0.12f, 0.12f, 0.94f);
			var highlightColor = new Color(1.0f, 1.0f, 1.0f, 0.78f);

			if (MarkerTexture != null)
			{
				DrawTextureRect(MarkerTexture, new Rect2(Vector2.Zero, Size), false, new Color(1.0f, 1.0f, 1.0f, 0.92f));
				return;
			}

			DrawArc(center + new Vector2(1.5f, 2.0f), radius, 0.0f, Mathf.Tau, 72, shadowColor, strokeWidth + 2.0f, true);
			DrawLine(
				center + new Vector2(-slashOffset + 1.5f, slashOffset + 2.0f),
				center + new Vector2(slashOffset + 1.5f, -slashOffset + 2.0f),
				shadowColor,
				strokeWidth + 2.0f,
				true);
			DrawArc(center, radius, 0.0f, Mathf.Tau, 72, rimColor, strokeWidth, true);
			DrawLine(
				center + new Vector2(-slashOffset, slashOffset),
				center + new Vector2(slashOffset, -slashOffset),
				rimColor,
				strokeWidth,
				true);
			DrawArc(center, radius - strokeWidth * 0.42f, -0.2f, Mathf.Pi * 1.08f, 48, highlightColor, 1.4f, true);
		}
	}

	private static float GetItemCardVisualScale(string itemCardId)
	{
		return 1.0f;
	}

	private float GetHandCardVisualScaleForPlayer(int playerIndex)
	{
		return IsLocalHandPlayer(playerIndex) ? 1.18f : 0.74f;
	}

	private float GetHandDisplayCardVisualScale(int playerIndex, int displayIndex)
	{
		float playerScale = GetHandCardVisualScaleForPlayer(playerIndex);
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count
			|| displayIndex < _playerHands[playerIndex].Count)
		{
			return playerScale;
		}

		int itemIndex = displayIndex - _playerHands[playerIndex].Count;
		if (itemIndex < 0 || itemIndex >= _playerItemHands[playerIndex].Count)
		{
			return playerScale;
		}

		return playerScale * GetItemCardVisualScale(_playerItemHands[playerIndex][itemIndex].Id);
	}

	private Vector2 GetHandDisplayCardVisualBasePosition(int playerIndex, int displayIndex)
	{
		float visualScale = GetHandDisplayCardVisualScale(playerIndex, displayIndex);
		return GetCardVisualBasePosition(visualScale);
	}

	private static Vector2 GetCardVisualBasePosition(float visualScale)
	{
		return (CardVisualSize - CardVisualSize * visualScale) / 2.0f;
	}

	private static Vector2 GetItemCardVisualSize(string itemCardId)
	{
		return CardVisualSize * GetItemCardVisualScale(itemCardId);
	}

	private static Vector2 GetItemCardVisualPosition(string itemCardId)
	{
		return (CardVisualSize - GetItemCardVisualSize(itemCardId)) / 2.0f;
	}

	private float GetCardRotationDegreesForPlayer(int playerIndex)
	{
		return GetPlayerViewSlotIndex(playerIndex) switch
		{
			2 => 90.0f,
			3 => -90.0f,
			_ => 0.0f,
		};
	}

	private float GetCardPileRotationDegreesForPlayer(int playerIndex)
	{
		return GetCardRotationDegreesForPlayer(playerIndex);
	}

	private static TextureRect? FindHandCardVisual(Control? handCardSlot)
	{
		if (handCardSlot == null)
		{
			return null;
		}

		for (int childIndex = 0; childIndex < handCardSlot.GetChildCount(); childIndex++)
		{
			Node child = handCardSlot.GetChild(childIndex);
			if (child is TextureRect textureRect)
			{
				return textureRect;
			}
		}

		return null;
	}
}
