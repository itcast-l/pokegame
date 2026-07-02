using Godot;

[Tool]
public partial class GameBoardLayoutAreasEditorPreview : Control
{
	private GameBoardLayoutState? _lastAppliedState;

	public override void _Ready()
	{
		SetProcess(Engine.IsEditorHint());
		TryRefreshPreview(force: true);
	}

	public override void _Process(double delta)
	{
		if (!Engine.IsEditorHint())
		{
			if (IsProcessing())
			{
				SetProcess(false);
			}

			return;
		}

		TryRefreshPreview();
	}

	private void TryRefreshPreview(bool force = false)
	{
		if (!Engine.IsEditorHint())
		{
			return;
		}

		TextureRect? boardArt = GetNodeOrNull<TextureRect>("../BoardArt");
		TextureRect? ultraWideBackdrop = GetNodeOrNull<TextureRect>("../UltraWideBackdrop");
		TextureRect? verticalBackdrop = GetNodeOrNull<TextureRect>("../VerticalBackdrop");
		Control? questionCardTransformArea = GetNodeOrNull<Control>("QuestionCardTransformArea");

		if (!GameBoardLayoutGeometry.TryBuildLayoutState(
			boardArt,
			GameBoardLayoutGeometry.DefaultBoardDesignSize,
			out GameBoardLayoutState state))
		{
			return;
		}

		if (!force
			&& _lastAppliedState.HasValue
			&& GameBoardLayoutGeometry.ApproximatelyEquals(_lastAppliedState.Value, state))
		{
			return;
		}

		GameBoardLayoutGeometry.ApplyLayout(
			state,
			boardArt,
			ultraWideBackdrop,
			verticalBackdrop,
			this,
			questionCardTransformArea);
		_lastAppliedState = state;
	}
}
