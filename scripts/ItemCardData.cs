using Godot;
using System.Collections.Generic;

public partial class ItemCardData : Node2D
{
	private const double ItemCardFrameTime = 0.18;

	private static readonly Dictionary<string, Texture2D[]> ItemCardFrames = new()
	{
		["QuestionMark"] = LoadFrames("question_mark"),
		["Bomb"] = LoadFrames("bomb"),
		["Magnet"] = LoadFrames("magnet"),
		["Greed"] = LoadFrames("greed"),
		["Lens"] = LoadFrames("lens"),
		["Double"] = LoadFrames("double"),
		["Overtime"] = LoadFrames("overtime"),
		["DrawCard"] = LoadFrames("draw_card"),
		["Shuffle"] = LoadFrames("shuffle"),
	};

	[Export(PropertyHint.MultilineText)]
	public string item_description = "";

	private Sprite2D? _animatedSprite;
	private Texture2D[] _animationFrames = System.Array.Empty<Texture2D>();
	private int _animationFrame;
	private Godot.Timer? _animationTimer;

	public override void _Ready()
	{
		_animatedSprite = FindAnimatedSprite();
		if (_animatedSprite == null)
		{
			return;
		}

		if (!ItemCardFrames.TryGetValue(_animatedSprite.Name.ToString(), out Texture2D[]? frames) || frames.Length == 0)
		{
			return;
		}

		_animationFrames = frames;
		_animatedSprite.Texture = _animationFrames[0];
		_animationTimer = new Godot.Timer
		{
			WaitTime = ItemCardFrameTime,
			OneShot = false,
			Autostart = false,
		};
		_animationTimer.Timeout += AdvanceAnimationFrame;
		AddChild(_animationTimer);
		_animationTimer.Start();
	}

	private static Texture2D[] LoadFrames(string folderName)
	{
		const string root = "res://art/cards/item_cards";
		return new[]
		{
			GD.Load<Texture2D>($"{root}/{folderName}/run-004/processed/frames/frame-001.png"),
			GD.Load<Texture2D>($"{root}/{folderName}/run-004/processed/frames/frame-002.png"),
			GD.Load<Texture2D>($"{root}/{folderName}/run-004/processed/frames/frame-003.png"),
			GD.Load<Texture2D>($"{root}/{folderName}/run-004/processed/frames/frame-004.png"),
		};
	}

	private Sprite2D? FindAnimatedSprite()
	{
		foreach (Node child in GetChildren())
		{
			if (child is Sprite2D sprite && ItemCardFrames.ContainsKey(sprite.Name.ToString()))
			{
				return sprite;
			}
		}

		return null;
	}

	private void AdvanceAnimationFrame()
	{
		if (_animatedSprite == null || _animationFrames.Length == 0)
		{
			return;
		}

		_animationFrame = (_animationFrame + 1) % _animationFrames.Length;
		_animatedSprite.Texture = _animationFrames[_animationFrame];
	}
}
