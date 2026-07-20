using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const string AdventureTraceHuntMarkerOverlayNodeName = "AdventureTraceHuntMarker";
	private const string AdventureTraceHuntMarkerIntroNodeName = "AdventureTraceHuntMarkerIntro";
	private const float AdventureTraceHuntMarkerFrameSeconds = 0.08f;
	private const int AdventureTraceHuntMarkerIntroZIndex = PlayedCardFlightZIndex + 940;
	private static readonly Vector2 AdventureTraceHuntMarkerVisualSize = new(112.0f, 112.0f);

	private Control? _adventureTraceHuntMarkerIntro;
	private Tween? _adventureTraceHuntMarkerIntroTween;
	private int _adventureTraceHuntMarkerIntroGeneration;

	private void SetAdventureTraceHuntMarkedTableCard(TableCard? target, bool playIntro)
	{
		if (ReferenceEquals(_adventureTraceHuntMarkedTableCard, target))
		{
			return;
		}

		_adventureTraceHuntMarkedTableCard = target;
		ClearAdventureTraceHuntMarkerIntro();
		if (target != null && playIntro)
		{
			TrackBoardTask(PlayAdventureTraceHuntMarkerIntroAsync(target, _adventureTraceHuntMarkerIntroGeneration), nameof(PlayAdventureTraceHuntMarkerIntroAsync));
		}
	}

	private void ClearAdventureTraceHuntMarkerIfTableCardRemoved(TableCard removedTableCard)
	{
		if (!ReferenceEquals(_adventureTraceHuntMarkedTableCard, removedTableCard))
		{
			return;
		}

		SetAdventureTraceHuntMarkedTableCard(null, playIntro: false);
	}

	private async Task PlayAdventureTraceHuntMarkerIntroAsync(TableCard target, int introGeneration)
	{
		EnsureAdventureTraceHuntMarkerTexturesLoaded();
		if (_adventureTraceHuntMarkerFrames.Count == 0
			|| introGeneration != _adventureTraceHuntMarkerIntroGeneration
			|| !ReferenceEquals(_adventureTraceHuntMarkedTableCard, target)
			|| !TryGetTraceHuntMarkerGlobalPosition(target, out Vector2 globalPosition))
		{
			return;
		}

		Control intro = _animationPresenter.RentControl(
			this,
			AdventureTraceHuntMarkerIntroNodeName,
			AdventureTraceHuntMarkerVisualSize,
			AdventureTraceHuntMarkerIntroZIndex);
		intro.GlobalPosition = globalPosition;
		TextureRect marker = _animationPresenter.RentTextureRect(
			intro,
			"AdventureTraceHuntMarkerIntroFrames",
			_adventureTraceHuntMarkerFrames[0],
			AdventureTraceHuntMarkerVisualSize,
			0,
			CanvasItem.TextureFilterEnum.Linear);
		marker.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		_adventureTraceHuntMarkerIntro = intro;

		Tween frameTween = CreateBoardTween(marker);
		_adventureTraceHuntMarkerIntroTween = frameTween;
		frameTween.SetParallel(true);
		for (int frameIndex = 0; frameIndex < _adventureTraceHuntMarkerFrames.Count; frameIndex++)
		{
			Texture2D frame = _adventureTraceHuntMarkerFrames[frameIndex];
			frameTween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(marker))
				{
					marker.Texture = frame;
				}
			})).SetDelay(frameIndex * AdventureTraceHuntMarkerFrameSeconds);
		}
		await WaitForBoardDelayAsync(AdventureTraceHuntMarkerFrameSeconds * _adventureTraceHuntMarkerFrames.Count);

		if (introGeneration != _adventureTraceHuntMarkerIntroGeneration
			|| !ReferenceEquals(_adventureTraceHuntMarkerIntro, intro))
		{
			return;
		}

		_adventureTraceHuntMarkerIntro = null;
		_adventureTraceHuntMarkerIntroTween = null;
		if (GodotObject.IsInstanceValid(intro))
		{
			_animationPresenter.Release(intro);
		}
	}

	private void ClearAdventureTraceHuntMarkerIntro()
	{
		_adventureTraceHuntMarkerIntroGeneration++;
		_adventureTraceHuntMarkerIntroTween?.Kill();
		_adventureTraceHuntMarkerIntroTween = null;
		Control? intro = _adventureTraceHuntMarkerIntro;
		_adventureTraceHuntMarkerIntro = null;
		if (intro != null && GodotObject.IsInstanceValid(intro))
		{
			_animationPresenter.Release(intro);
		}
	}

	private void AddAdventureTraceHuntMarkerOverlay(Vector2 cardPosition, int zIndex)
	{
		EnsureAdventureTraceHuntMarkerTexturesLoaded();
		if (_adventureTraceHuntMarkerFrames.Count == 0)
		{
			return;
		}

		TextureRect overlay = RentTableRenderTextureRect(AdventureTraceHuntMarkerOverlayNodeName);
		overlay.Texture = _adventureTraceHuntMarkerFrames[^1];
		overlay.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
		overlay.Size = AdventureTraceHuntMarkerVisualSize;
		overlay.Position = cardPosition + (CardVisualSize - AdventureTraceHuntMarkerVisualSize) / 2.0f;
		overlay.PivotOffset = AdventureTraceHuntMarkerVisualSize / 2.0f;
		overlay.ZIndex = zIndex;
		_centerArena.AddChild(overlay);
	}

	private bool TryGetTraceHuntMarkerGlobalPosition(TableCard target, out Vector2 globalPosition)
	{
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (!ReferenceEquals(_tableCards[tableCardIndex], target))
			{
				continue;
			}

			if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
			{
				break;
			}

			globalPosition = _centerArena.GlobalPosition
				+ cardPosition
				+ (CardVisualSize - AdventureTraceHuntMarkerVisualSize) / 2.0f;
			return true;
		}

		globalPosition = default;
		return false;
	}

	private int GetAdventureTraceHuntMarkedTableCardIndex()
	{
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (ReferenceEquals(_tableCards[tableCardIndex], _adventureTraceHuntMarkedTableCard))
			{
				return tableCardIndex;
			}
		}

		return -1;
	}

	private TextureRect? FindAdventureTraceHuntMarkerOverlay()
	{
		for (int childIndex = 0; childIndex < _centerArena.GetChildCount(); childIndex++)
		{
			if (_centerArena.GetChild(childIndex) is TextureRect overlay
				&& overlay.Name.ToString() == AdventureTraceHuntMarkerOverlayNodeName)
			{
				return overlay;
			}
		}

		return null;
	}
}
