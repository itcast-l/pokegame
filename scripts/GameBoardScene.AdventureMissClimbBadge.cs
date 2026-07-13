using System;
using Godot;

public partial class GameBoardScene
{
	private const string AdventureMissClimbBadgeNodeName = "AdventureMissClimbBadge";
	private const string AdventureMissClimbGrowthUpgradeId = "clubs_miss_climb_growth";
	private const float AdventureMissClimbBadgeFadeInSeconds = 0.12f;
	private const float AdventureMissClimbBadgeFadeOutSeconds = 0.10f;
	private static readonly Vector2 AdventureMissClimbBadgeSize = new(44.0f, 24.0f);
	private static readonly StyleBoxFlat AdventureMissClimbBadgeStyle = CreateAdventureMissClimbBadgeStyle();

	private void AddAdventureMissClimbBadge(
		Control cardSlot,
		TextureRect cardVisual,
		int playerIndex,
		PlayingCard card,
		bool showCardBack,
		bool showInputSelection)
	{
		if (!TryGetAdventureMissClimbBadgeGrowth(
			playerIndex,
			card,
			showCardBack,
			out int growthStacks))
		{
			return;
		}

		var badge = new Panel
		{
			Name = AdventureMissClimbBadgeNodeName,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = new Vector2(
				cardVisual.Size.X - AdventureMissClimbBadgeSize.X + 5.0f,
				-5.0f),
			Size = AdventureMissClimbBadgeSize,
			PivotOffset = AdventureMissClimbBadgeSize / 2.0f,
			Scale = Vector2.One * 0.9f,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			Visible = false,
			ZIndex = 12,
			ZAsRelative = true,
		};
		badge.AddThemeStyleboxOverride("panel", AdventureMissClimbBadgeStyle);

		var label = new Label
		{
			Name = "GrowthLabel",
			Text = $"♣ +{growthStacks}",
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.73f, 1.0f));
		label.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.08f, 0.035f, 0.96f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.03f, 0.01f, 0.92f));
		label.AddThemeConstantOverride("outline_size", 1);
		label.AddThemeConstantOverride("shadow_offset_x", 0);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		badge.AddChild(label);
		cardVisual.AddChild(badge);

		cardSlot.MouseFilter = MouseFilterEnum.Stop;
		bool isHovered = false;
		Tween? visibilityTween = null;
		int visibilityGeneration = 0;

		void SetVisible(bool visible, bool animate)
		{
			visibilityGeneration++;
			int generation = visibilityGeneration;
			visibilityTween?.Kill();
			visibilityTween = null;

			if (visible)
			{
				badge.Visible = true;
			}

			float targetAlpha = visible ? 1.0f : 0.0f;
			Vector2 targetScale = Vector2.One * (visible ? 1.0f : 0.96f);
			if (!animate)
			{
				badge.Modulate = new Color(1.0f, 1.0f, 1.0f, targetAlpha);
				badge.Scale = targetScale;
				badge.Visible = visible;
				return;
			}

			visibilityTween = badge.CreateTween();
			visibilityTween.SetParallel(true);
			visibilityTween.SetTrans(visible ? Tween.TransitionType.Cubic : Tween.TransitionType.Sine);
			visibilityTween.SetEase(visible ? Tween.EaseType.Out : Tween.EaseType.InOut);
			float duration = visible
				? AdventureMissClimbBadgeFadeInSeconds
				: AdventureMissClimbBadgeFadeOutSeconds;
			visibilityTween.TweenProperty(badge, "modulate:a", targetAlpha, duration);
			visibilityTween.TweenProperty(badge, "scale", targetScale, duration);
			if (!visible)
			{
				visibilityTween.Finished += () =>
				{
					if (generation == visibilityGeneration)
					{
						badge.Visible = false;
					}
				};
			}
		}

		cardSlot.MouseEntered += () =>
		{
			isHovered = true;
			SetVisible(true, animate: true);
		};
		cardSlot.MouseExited += () =>
		{
			isHovered = false;
			SetVisible(showInputSelection, animate: true);
		};
		cardSlot.TreeExiting += () =>
		{
			visibilityGeneration++;
			visibilityTween?.Kill();
		};

		SetVisible(isHovered || showInputSelection, animate: showInputSelection);
	}

	private bool TryGetAdventureMissClimbBadgeGrowth(
		int playerIndex,
		PlayingCard card,
		bool showCardBack,
		out int growthStacks)
	{
		growthStacks = GetAdventureMissClimbBadgeGrowthForRender(playerIndex);
		return growthStacks > 0
			&& !showCardBack
			&& TryGetNumericRankValue(card.Rank, out int rankValue)
			&& rankValue + growthStacks <= 10;
	}

	private int GetAdventureMissClimbBadgeGrowthForRender(int playerIndex)
	{
		if (playerIndex != MainPlayerIndex
			|| !IsLocalHandPlayer(playerIndex)
			|| !ShouldUseAdventureUpgradeSystem())
		{
			return 0;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true
			|| !runState.HasAcquiredUpgrade(AdventureMissClimbGrowthUpgradeId))
		{
			return 0;
		}

		return Math.Clamp(
			runState.EffectState.ClubMissClimbGrowthStacks,
			0,
			AdventureUpgradeEffectState.MaxClubMissClimbGrowthStacks);
	}

	private static StyleBoxFlat CreateAdventureMissClimbBadgeStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.025f, 0.19f, 0.085f, 0.94f),
			BorderColor = new Color(0.53f, 0.91f, 0.39f, 0.96f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6,
			ShadowColor = new Color(0.0f, 0.035f, 0.01f, 0.72f),
			ShadowSize = 3,
			ShadowOffset = new Vector2(0.0f, 2.0f),
		};
	}
}
