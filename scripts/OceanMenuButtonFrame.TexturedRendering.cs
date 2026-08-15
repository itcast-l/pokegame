using Godot;

public partial class OceanMenuButtonFrame : Control
{
	private readonly StyleBoxTexture _texturedFrameStyle = new()
	{
		AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch,
		AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch,
		DrawCenter = true,
	};
	private Texture2D? _cachedTexturedFrameTexture;
	private float _cachedTexturePatchMarginHorizontal = float.NaN;
	private float _cachedTexturePatchMarginVertical = float.NaN;

	private bool TryDrawTexturedFrame(Rect2 rect)
	{
		Texture2D? texture = FrameStyle switch
		{
			ButtonFrameStyle.CardSlabs => CardSlabTexture,
			ButtonFrameStyle.AdventureGlassPlaque => null,
			ButtonFrameStyle.AdventureGlassSelector => null,
			ButtonFrameStyle.AdventureGlassMenuRow => null,
			ButtonFrameStyle.FirstVersionRow => null,
			ButtonFrameStyle.FirstVersionBack => null,
			ButtonFrameStyle.FirstVersionPrimary => null,
			ButtonFrameStyle.IndividualToolRow => null,
			ButtonFrameStyle.IndividualAction => null,
			ButtonFrameStyle.IndividualSecondaryAction => null,
			_ => FlipOrnament && OceanPlaqueFlippedTexture != null
				? OceanPlaqueFlippedTexture
				: OceanPlaqueTexture,
		};
		if (texture == null)
		{
			return false;
		}

		bool pressed = _button?.ButtonPressed == true || HasPressedInteractiveDescendant();
		bool focused = Selected || _glow > 0.08f;
		if (!pressed && !focused && !ShowIdleTexture)
		{
			return true;
		}

		Texture2D stateTexture = pressed && PressedTexture != null
			? PressedTexture
			: focused && FocusTexture != null
				? FocusTexture
				: texture;
		DrawTexturedState(stateTexture, rect);
		if (stateTexture == texture
			&& SelectionGlowTexture != null
			&& (_glow > 0.01f || Selected))
		{
			float alpha = (Selected
				? 0.56f + _glow * 0.08f
				: 0.18f + _glow * 0.22f) * SelectionGlowStrength;
			DrawTextureRect(
				SelectionGlowTexture,
				rect.GrowIndividual(4.0f, 4.0f, 4.0f, 4.0f),
				false,
				new Color(1.0f, 1.0f, 1.0f, Mathf.Min(alpha, 1.0f)));
		}

		return true;
	}

	private bool HasPressedInteractiveDescendant()
	{
		return HasVisibleControl(_pressedControls);
	}

	private void DrawTexturedState(Texture2D texture, Rect2 rect)
	{
		if (!UseNinePatchTexture)
		{
			DrawTextureRect(texture, rect, false);
			return;
		}

		if (_cachedTexturedFrameTexture != texture
			|| _cachedTexturePatchMarginHorizontal != TexturePatchMarginHorizontal
			|| _cachedTexturePatchMarginVertical != TexturePatchMarginVertical)
		{
			_texturedFrameStyle.Texture = texture;
			_texturedFrameStyle.TextureMarginLeft = TexturePatchMarginHorizontal;
			_texturedFrameStyle.TextureMarginTop = TexturePatchMarginVertical;
			_texturedFrameStyle.TextureMarginRight = TexturePatchMarginHorizontal;
			_texturedFrameStyle.TextureMarginBottom = TexturePatchMarginVertical;
			_cachedTexturedFrameTexture = texture;
			_cachedTexturePatchMarginHorizontal = TexturePatchMarginHorizontal;
			_cachedTexturePatchMarginVertical = TexturePatchMarginVertical;
		}
		DrawStyleBox(_texturedFrameStyle, rect);
	}
}
