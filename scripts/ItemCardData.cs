using Godot;

public partial class ItemCardData : Node2D
{
	private const double ItemCardFrameTime = 0.18;
	private static readonly StringName ItemCardAnimationName = "item_card_frames";

	[Export(PropertyHint.MultilineText)]
	public string item_description = "";

	[Export]
	public bool enabled_in_game = true;

	private Sprite2D? _animatedSprite;
	private AnimationPlayer? _animationPlayer;
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

		_animatedSprite.Texture = frames[0];
		_animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer") ?? CreateAnimationPlayer();
		FrameAnimationHelpers.ConfigureTextureAnimation(
			_animationPlayer,
			ItemCardAnimationName,
			new NodePath($"{_animatedSprite.Name}:texture"),
			frames,
			ItemCardFrameTime,
			loops: true);
		_animationPlayer.Play(ItemCardAnimationName);
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
		for (int childIndex = 0; childIndex < GetChildCount(); childIndex++)
		{
			Node child = GetChild(childIndex);
			if (child is Sprite2D sprite && _catalog.TryGetIdForSpriteName(sprite.Name.ToString(), out _))
			{
				return sprite;
			}
		}

		return null;
	}

	private AnimationPlayer CreateAnimationPlayer()
	{
		var animationPlayer = new AnimationPlayer
		{
			Name = "AnimationPlayer",
			RootNode = new NodePath(".."),
		};
		AddChild(animationPlayer);
		return animationPlayer;
	}
}
