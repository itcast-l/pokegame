using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private bool ShouldPlayFishDefeatFinisher(int winnerPlayerIndex)
	{
		return adventure_mode
			&& winnerPlayerIndex == 1
			&& MainPlayerIndex < player_count
			&& adventure_enemy_avatar_id == FishAdventureEnemyAvatarId
			&& FishDefeatFinisherFrames.Length > 0
			&& _defeatFinisherRoot != null
			&& GodotObject.IsInstanceValid(_defeatFinisherRoot)
			&& _defeatFinisherTexture != null
			&& GodotObject.IsInstanceValid(_defeatFinisherTexture);
	}

	private Task PlayDefeatFinisherIfNeededAsync(int winnerPlayerIndex)
	{
		return Task.CompletedTask;
	}

	private async Task PlayFishDefeatFinisherTweenAsync()
	{
		if (_defeatFinisherRoot == null
			|| !GodotObject.IsInstanceValid(_defeatFinisherRoot)
			|| _defeatFinisherTexture == null
			|| !GodotObject.IsInstanceValid(_defeatFinisherTexture))
		{
			return;
		}

		_defeatFinisherRoot.Visible = true;
		_defeatFinisherRoot.Modulate = Colors.White;
		_defeatFinisherTexture.Texture = FishDefeatFinisherFrames[0];
		_defeatFinisherTexture.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);
		FrameAnimationHelpers.TweenTextureFramesForDuration(
			tween,
			_defeatFinisherTexture,
			FishDefeatFinisherFrames,
			FishDefeatFinisherSeconds / FishDefeatFinisherFrames.Length,
			FishDefeatFinisherSeconds);
		tween.TweenProperty(_defeatFinisherTexture, "modulate:a", 1.0f, FishDefeatFinisherSeconds * 0.10f);
		tween.TweenProperty(_defeatFinisherTexture, "modulate:a", 0.0f, FishDefeatFinisherSeconds * 0.16f)
			.SetDelay(FishDefeatFinisherSeconds * 0.84f);
		tween.TweenMethod(
			Callable.From<float>(progress => UpdateFishDefeatFinisherScale(progress)),
			0.0f,
			1.0f,
			FishDefeatFinisherSeconds);

		await ToSignal(tween, Tween.SignalName.Finished);

		if (GodotObject.IsInstanceValid(_defeatFinisherTexture))
		{
			_defeatFinisherTexture.Texture = null;
			_defeatFinisherTexture.Modulate = Colors.White;
		}

		if (GodotObject.IsInstanceValid(_defeatFinisherRoot))
		{
			_defeatFinisherRoot.Visible = false;
			_defeatFinisherRoot.Modulate = Colors.White;
		}
	}

	private void UpdateFishDefeatFinisherScale(float progress)
	{
		if (_defeatFinisherTexture == null || !GodotObject.IsInstanceValid(_defeatFinisherTexture))
		{
			return;
		}

		float scale = Mathf.Lerp(0.94f, 1.05f, Mathf.SmoothStep(0.0f, 1.0f, progress));
		_defeatFinisherTexture.Scale = new Vector2(scale, scale);
		_defeatFinisherTexture.PivotOffset = _defeatFinisherTexture.Size / 2.0f;
	}
}
