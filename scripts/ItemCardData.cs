using Godot;

public partial class ItemCardData : Node2D
{
	private const double ItemCardFrameTime = 0.18;

	[Export(PropertyHint.MultilineText)]
	public string item_description = "";

	[Export]
	public bool enabled_in_game = true;

	private Sprite2D? _animatedSprite;
	private Texture2D[] _animationFrames = System.Array.Empty<Texture2D>();
	private int _animationFrame;
	private Godot.Timer? _animationTimer;
	private ItemCardCatalog _catalog = default!;

	public override void _Ready()
	{
		_catalog = ItemCardCatalog.Load(this);
		_animatedSprite = FindAnimatedSprite();
		if (_animatedSprite == null)
		{
			return;
		}

		if (!TryGetItemCardId(out string itemCardId) || !_catalog.TryGetFrameTextures(itemCardId, out Texture2D[] frames) || frames.Length == 0)
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

	public bool TryGetItemCardId(out string itemCardId)
	{
		itemCardId = "";
		_catalog ??= ItemCardCatalog.Load(this);
		Sprite2D? sprite = FindAnimatedSprite();
		if (sprite == null)
		{
			return false;
		}

		if (!_catalog.TryGetIdForSpriteName(sprite.Name.ToString(), out string mappedItemCardId))
		{
			return false;
		}

		itemCardId = mappedItemCardId;
		return true;
	}

	private Sprite2D? FindAnimatedSprite()
	{
		_catalog ??= ItemCardCatalog.Load(this);
		foreach (Node child in GetChildren())
		{
			if (child is Sprite2D sprite && _catalog.TryGetIdForSpriteName(sprite.Name.ToString(), out _))
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
