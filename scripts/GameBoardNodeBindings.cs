using System;
using System.Collections.Generic;
using Godot;

public sealed class GameBoardNodeBindings
{
	private GameBoardNodeBindings(GameBoardScene owner)
	{
		BackdropFill = Require<TextureRect>(owner, "BackdropFill");
		BoardCanvas = Require<Control>(owner, "BoardCanvas");
		VerticalBackdrop = Require<TextureRect>(owner, "BoardCanvas/VerticalBackdrop");
		BoardArt = Require<TextureRect>(owner, "BoardCanvas/BoardArt");
		LayoutAreas = Require<Control>(owner, "BoardCanvas/LayoutAreas");
		CaptureRequirementWatermark = Require<Label>(owner, "%CaptureRequirementWatermark");
		CenterArena = Require<Control>(owner, "%CenterArena");
		TableCardLayoutArea = Require<Control>(owner, "%TableCardLayoutArea");
		TopPlayerArea = Require<Control>(owner, "%TopPlayerArea");
		BottomPlayerArea = Require<Control>(owner, "%BottomPlayerArea");
		LeftPlayerArea = Require<Control>(owner, "%LeftPlayerArea");
		RightPlayerArea = Require<Control>(owner, "%RightPlayerArea");
		QuestionCardTransformArea = Require<Control>(owner, "%QuestionCardTransformArea");
		PlayerAreas = new[]
		{
			BottomPlayerArea,
			TopPlayerArea,
			LeftPlayerArea,
			RightPlayerArea,
		};
		AvatarAreas = new[]
		{
			Require<DraggableAvatarArea>(owner, "%Player1AvatarArea"),
			Require<DraggableAvatarArea>(owner, "%Player2AvatarArea"),
			Require<DraggableAvatarArea>(owner, "%Player3AvatarArea"),
			Require<DraggableAvatarArea>(owner, "%Player4AvatarArea"),
		};
		ScoreMeters = new[]
		{
			Require<ScoreAvatarMeter>(owner, "%Player1ScoreMeter"),
			Require<ScoreAvatarMeter>(owner, "%Player2ScoreMeter"),
			Require<ScoreAvatarMeter>(owner, "%Player3ScoreMeter"),
			Require<ScoreAvatarMeter>(owner, "%Player4ScoreMeter"),
		};
		ScoreEffectTargets = new[]
		{
			Require<ScoreEffectTarget>(owner, "%Player1ScoreEffectTarget"),
			Require<ScoreEffectTarget>(owner, "%Player2ScoreEffectTarget"),
			Require<ScoreEffectTarget>(owner, "%Player3ScoreEffectTarget"),
			Require<ScoreEffectTarget>(owner, "%Player4ScoreEffectTarget"),
		};
		HandAreas = new[]
		{
			Require<Control>(owner, "%Player1HandArea"),
			Require<Control>(owner, "%Player2HandArea"),
			Require<Control>(owner, "%Player3HandArea"),
			Require<Control>(owner, "%Player4HandArea"),
		};
		CardPileAreas = new[]
		{
			Require<Control>(owner, "%Player1CardPileArea"),
			Require<Control>(owner, "%Player2CardPileArea"),
			Require<Control>(owner, "%Player3CardPileArea"),
			Require<Control>(owner, "%Player4CardPileArea"),
		};
	}

	public TextureRect BackdropFill { get; }
	public Control BoardCanvas { get; }
	public TextureRect VerticalBackdrop { get; }
	public TextureRect BoardArt { get; }
	public Control LayoutAreas { get; }
	public Label CaptureRequirementWatermark { get; }
	public Control CenterArena { get; }
	public Control TableCardLayoutArea { get; }
	public Control TopPlayerArea { get; }
	public Control BottomPlayerArea { get; }
	public Control LeftPlayerArea { get; }
	public Control RightPlayerArea { get; }
	public Control QuestionCardTransformArea { get; }
	public IReadOnlyList<Control> PlayerAreas { get; }
	public IReadOnlyList<DraggableAvatarArea> AvatarAreas { get; }
	public IReadOnlyList<ScoreAvatarMeter> ScoreMeters { get; }
	public IReadOnlyList<ScoreEffectTarget> ScoreEffectTargets { get; }
	public IReadOnlyList<Control> HandAreas { get; }
	public IReadOnlyList<Control> CardPileAreas { get; }

	public static GameBoardNodeBindings BindRequired(GameBoardScene owner)
	{
		return new GameBoardNodeBindings(owner ?? throw new ArgumentNullException(nameof(owner)));
	}

	private static T Require<T>(Node owner, NodePath path) where T : Node
	{
		T? node = owner.GetNodeOrNull<T>(path);
		return node ?? throw new InvalidOperationException($"Required game-board node is missing or has the wrong type: {path}");
	}
}
