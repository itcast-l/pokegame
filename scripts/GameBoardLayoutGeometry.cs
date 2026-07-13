using Godot;

public readonly record struct GameBoardLayoutState(
	Rect2 VisibleRect,
	bool ShowVerticalBackdrop,
	bool ShowBoardArt);

public static class GameBoardLayoutGeometry
{
	public static readonly Vector2 DefaultBoardDesignSize = new(1920.0f, 1080.0f);

	public static bool TryBuildLayoutState(TextureRect? boardArt, Vector2 fallbackTextureSize, out GameBoardLayoutState state)
	{
		state = default;
		if (boardArt == null || !GodotObject.IsInstanceValid(boardArt))
		{
			return false;
		}

		Rect2 boardArtRect = boardArt.GetGlobalRect();
		Vector2 textureSize = ResolveBoardArtTextureSize(boardArt, fallbackTextureSize);
		state = BuildLayoutState(boardArtRect, textureSize);
		return true;
	}

	public static GameBoardLayoutState BuildLayoutState(Rect2 boardArtRect, Vector2 textureSize)
	{
		Rect2 visibleRect = GetVisibleRect(boardArtRect, textureSize);
		bool hasHorizontalLetterboxing = visibleRect.Size.X < boardArtRect.Size.X - 0.5f;
		bool hasVerticalLetterboxing = visibleRect.Size.Y < boardArtRect.Size.Y - 0.5f;
		bool useVerticalCompositeBackdrop = hasVerticalLetterboxing && !hasHorizontalLetterboxing;
		return new GameBoardLayoutState(
			visibleRect,
			useVerticalCompositeBackdrop,
			!useVerticalCompositeBackdrop);
	}

	public static Rect2 GetBoardArtVisibleRect(TextureRect? boardArt, Vector2 fallbackTextureSize, Rect2 fallbackRect)
	{
		return TryBuildLayoutState(boardArt, fallbackTextureSize, out GameBoardLayoutState state)
			? state.VisibleRect
			: fallbackRect;
	}

	public static Vector2 ResolveBoardArtTextureSize(TextureRect? boardArt, Vector2 fallbackTextureSize)
	{
		if (boardArt?.Texture != null)
		{
			Vector2 textureSize = boardArt.Texture.GetSize();
			if (textureSize.X > 0.0f && textureSize.Y > 0.0f)
			{
				return textureSize;
			}
		}

		return fallbackTextureSize;
	}

	public static Rect2 GetVisibleRect(Rect2 boardArtRect, Vector2 textureSize)
	{
		if (textureSize.X <= 0.0f
			|| textureSize.Y <= 0.0f
			|| boardArtRect.Size.X <= 0.0f
			|| boardArtRect.Size.Y <= 0.0f)
		{
			return boardArtRect;
		}

		float textureAspect = textureSize.X / textureSize.Y;
		float controlAspect = boardArtRect.Size.X / boardArtRect.Size.Y;
		Vector2 visibleSize = controlAspect > textureAspect
			? new Vector2(boardArtRect.Size.Y * textureAspect, boardArtRect.Size.Y)
			: new Vector2(boardArtRect.Size.X, boardArtRect.Size.X / textureAspect);
		Vector2 visiblePosition = boardArtRect.Position + (boardArtRect.Size - visibleSize) / 2.0f;
		return new Rect2(visiblePosition, visibleSize);
	}

	public static void ApplyLayout(
		GameBoardLayoutState state,
		TextureRect? boardArt,
		TextureRect? verticalBackdrop,
		Control? layoutAreas,
		Control? questionCardTransformArea)
	{
		if (layoutAreas == null || !GodotObject.IsInstanceValid(layoutAreas))
		{
			return;
		}

		if (verticalBackdrop != null && GodotObject.IsInstanceValid(verticalBackdrop))
		{
			verticalBackdrop.Visible = state.ShowVerticalBackdrop;
		}

		if (boardArt != null && GodotObject.IsInstanceValid(boardArt))
		{
			boardArt.Visible = state.ShowBoardArt;
		}

		if (questionCardTransformArea != null && GodotObject.IsInstanceValid(questionCardTransformArea))
		{
			questionCardTransformArea.PivotOffset = questionCardTransformArea.Size / 2.0f;
		}
	}

	public static bool ApproximatelyEquals(GameBoardLayoutState first, GameBoardLayoutState second, float tolerance = 0.05f)
	{
		return first.ShowVerticalBackdrop == second.ShowVerticalBackdrop
			&& first.ShowBoardArt == second.ShowBoardArt
			&& Mathf.Abs(first.VisibleRect.Position.X - second.VisibleRect.Position.X) <= tolerance
			&& Mathf.Abs(first.VisibleRect.Position.Y - second.VisibleRect.Position.Y) <= tolerance
			&& Mathf.Abs(first.VisibleRect.Size.X - second.VisibleRect.Size.X) <= tolerance
			&& Mathf.Abs(first.VisibleRect.Size.Y - second.VisibleRect.Size.Y) <= tolerance;
	}
}
