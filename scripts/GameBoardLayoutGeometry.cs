using System;
using System.Collections.Generic;
using Godot;

public enum GameBoardLayoutVariant
{
	Standard,
	VerticalComposite,
}

public sealed class GameBoardLayoutProfile
{
	public GameBoardLayoutProfile(
		IReadOnlyList<Rect2> avatarAreas,
		IReadOnlyList<Rect2> scoreMeters,
		IReadOnlyList<Vector2> scoreEffectAnchors,
		IReadOnlyList<Rect2> handAreas,
		IReadOnlyList<Rect2> cardPileAreas,
		Rect2 tableCardArea,
		Rect2 questionCardArea,
		Rect2 centerArena)
	{
		if (avatarAreas.Count != 4
			|| scoreMeters.Count != 4
			|| scoreEffectAnchors.Count != 4
			|| handAreas.Count != 4
			|| cardPileAreas.Count != 4)
		{
			throw new ArgumentException("Game board layout profiles require four player entries per collection.");
		}

		AvatarAreas = avatarAreas;
		ScoreMeters = scoreMeters;
		ScoreEffectAnchors = scoreEffectAnchors;
		HandAreas = handAreas;
		CardPileAreas = cardPileAreas;
		TableCardArea = tableCardArea;
		QuestionCardArea = questionCardArea;
		CenterArena = centerArena;
	}

	public IReadOnlyList<Rect2> AvatarAreas { get; }
	public IReadOnlyList<Rect2> ScoreMeters { get; }
	public IReadOnlyList<Vector2> ScoreEffectAnchors { get; }
	public IReadOnlyList<Rect2> HandAreas { get; }
	public IReadOnlyList<Rect2> CardPileAreas { get; }
	public Rect2 TableCardArea { get; }
	public Rect2 QuestionCardArea { get; }
	public Rect2 CenterArena { get; }
}

public readonly record struct GameBoardLayoutState(
	Rect2 BoardCanvasRect,
	float DesignScale,
	Vector2 DesignOffset,
	GameBoardLayoutVariant Variant,
	GameBoardLayoutProfile Profile);

public static class GameBoardLayoutGeometry
{
	public static readonly Vector2 DesignSize = new(1280.0f, 720.0f);
	public const float MinimumCanvasAspect = 1.5f;
	public const float MaximumCanvasAspect = 16.0f / 9.0f;
	public const float StandardToVerticalThreshold = 1.58f;
	public const float VerticalToStandardThreshold = 1.60f;

	public static GameBoardLayoutProfile StandardProfile { get; } = new(
		avatarAreas: new[]
		{
			new Rect2(280.0f, 541.0f, 94.0f, 93.0f),
			new Rect2(379.0f, 31.0f, 78.0f, 78.0f),
			new Rect2(128.0f, 145.0f, 78.0f, 78.0f),
			new Rect2(1072.0f, 146.0f, 78.0f, 78.0f),
		},
		scoreMeters: new[]
		{
			new Rect2(277.51199f, 537.73999f, 98.976013f, 99.52002f),
			new Rect2(372.0f, 24.0f, 92.0f, 92.0f),
			new Rect2(121.0f, 138.0f, 92.0f, 92.0f),
			new Rect2(1065.0f, 139.0f, 92.0f, 92.0f),
		},
		scoreEffectAnchors: new[]
		{
			new Vector2(327.0f, 587.5f),
			new Vector2(418.0f, 70.0f),
			new Vector2(167.0f, 184.0f),
			new Vector2(1111.0f, 185.0f),
		},
		handAreas: new[]
		{
			new Rect2(455.0f, 542.0f, 360.0f, 130.0f),
			new Rect2(506.0f, 34.0f, 270.0f, 78.0f),
			new Rect2(115.0f, 222.0f, 92.0f, 266.0f),
			new Rect2(1069.0f, 227.0f, 92.0f, 266.0f),
		},
		cardPileAreas: new[]
		{
			new Rect2(849.0f, 527.0f, 124.0f, 128.0f),
			new Rect2(806.0f, 16.0f, 124.0f, 128.0f),
			new Rect2(104.0f, 438.0f, 124.0f, 128.0f),
			new Rect2(1055.0f, 433.0f, 124.0f, 128.0f),
		},
		tableCardArea: new Rect2(292.5f, 182.0f, 683.648f, 264.264f),
		questionCardArea: new Rect2(574.0f, 238.0f, 138.0f, 168.0f),
		centerArena: new Rect2(262.375f, 159.0f, 762.66626f, 408.23999f));

	public static GameBoardLayoutProfile VerticalCompositeProfile { get; } = new(
		avatarAreas: new[]
		{
			new Rect2(285.33334f, 524.0f, 96.0f, 96.0f),
			new Rect2(381.66666f, 70.333336f, 78.0f, 78.0f),
			new Rect2(149.0f, 158.33333f, 78.0f, 78.0f),
			new Rect2(1051.6666f, 158.33333f, 78.0f, 78.0f),
		},
		scoreMeters: new[]
		{
			new Rect2(287.33334f, 526.0f, 92.0f, 92.0f),
			new Rect2(374.66666f, 63.333336f, 92.0f, 92.0f),
			new Rect2(142.0f, 151.33333f, 92.0f, 92.0f),
			new Rect2(1044.6666f, 151.33333f, 92.0f, 92.0f),
		},
		scoreEffectAnchors: new[]
		{
			new Vector2(333.33334f, 572.0f),
			new Vector2(420.66666f, 109.333336f),
			new Vector2(188.0f, 197.33333f),
			new Vector2(1090.6666f, 197.33333f),
		},
		handAreas: new[]
		{
			new Rect2(437.0f, 503.0f, 360.0f, 130.0f),
			new Rect2(491.0f, 83.0f, 270.0f, 78.0f),
			new Rect2(195.0f, 225.0f, 92.0f, 266.0f),
			new Rect2(991.0f, 228.0f, 92.0f, 266.0f),
		},
		cardPileAreas: new[]
		{
			new Rect2(806.0f, 493.0f, 124.0f, 128.0f),
			new Rect2(762.0f, 46.0f, 124.0f, 128.0f),
			new Rect2(183.0f, 435.0f, 124.0f, 128.0f),
			new Rect2(978.0f, 433.0f, 124.0f, 128.0f),
		},
		tableCardArea: new Rect2(330.0f, 182.0f, 602.948f, 264.264f),
		questionCardArea: new Rect2(562.0f, 238.0f, 138.0f, 168.0f),
		centerArena: new Rect2(318.0f, 159.0f, 643.49963f, 408.23999f));

	public static GameBoardLayoutState BuildLayoutState(
		Vector2 viewportSize,
		GameBoardLayoutVariant? currentVariant = null)
	{
		Vector2 safeViewportSize = new(
			Math.Max(1.0f, viewportSize.X),
			Math.Max(1.0f, viewportSize.Y));
		float viewportAspect = safeViewportSize.X / safeViewportSize.Y;
		Rect2 canvasRect;
		if (viewportAspect < MinimumCanvasAspect)
		{
			Vector2 canvasSize = new(safeViewportSize.X, safeViewportSize.X / MinimumCanvasAspect);
			canvasRect = new Rect2(
				new Vector2(0.0f, (safeViewportSize.Y - canvasSize.Y) / 2.0f),
				canvasSize);
		}
		else if (viewportAspect > MaximumCanvasAspect)
		{
			Vector2 canvasSize = new(safeViewportSize.Y * MaximumCanvasAspect, safeViewportSize.Y);
			canvasRect = new Rect2(
				new Vector2((safeViewportSize.X - canvasSize.X) / 2.0f, 0.0f),
				canvasSize);
		}
		else
		{
			canvasRect = new Rect2(Vector2.Zero, safeViewportSize);
		}

		GameBoardLayoutVariant variant = ResolveVariant(viewportAspect, currentVariant);
		float designScale = Math.Max(
			canvasRect.Size.X / DesignSize.X,
			canvasRect.Size.Y / DesignSize.Y);
		Vector2 designOffset = (canvasRect.Size - DesignSize * designScale) / 2.0f;
		GameBoardLayoutProfile profile = variant == GameBoardLayoutVariant.Standard
			? StandardProfile
			: VerticalCompositeProfile;
		return new GameBoardLayoutState(canvasRect, designScale, designOffset, variant, profile);
	}

	public static GameBoardLayoutVariant ResolveVariant(
		float viewportAspect,
		GameBoardLayoutVariant? currentVariant)
	{
		if (!currentVariant.HasValue)
		{
			return viewportAspect >= VerticalToStandardThreshold
				? GameBoardLayoutVariant.Standard
				: GameBoardLayoutVariant.VerticalComposite;
		}

		return currentVariant.Value switch
		{
			GameBoardLayoutVariant.Standard when viewportAspect <= StandardToVerticalThreshold
				=> GameBoardLayoutVariant.VerticalComposite,
			GameBoardLayoutVariant.VerticalComposite when viewportAspect >= VerticalToStandardThreshold
				=> GameBoardLayoutVariant.Standard,
			_ => currentVariant.Value,
		};
	}

	public static bool ApproximatelyEquals(GameBoardLayoutState first, GameBoardLayoutState second, float tolerance = 0.05f)
	{
		return first.Variant == second.Variant
			&& RectApproximatelyEquals(first.BoardCanvasRect, second.BoardCanvasRect, tolerance)
			&& Mathf.Abs(first.DesignScale - second.DesignScale) <= tolerance
			&& first.DesignOffset.DistanceTo(second.DesignOffset) <= tolerance;
	}

	private static bool RectApproximatelyEquals(Rect2 first, Rect2 second, float tolerance)
	{
		return first.Position.DistanceTo(second.Position) <= tolerance
			&& first.Size.DistanceTo(second.Size) <= tolerance;
	}
}
