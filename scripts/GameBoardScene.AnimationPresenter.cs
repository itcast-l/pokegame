using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private Sprite2D RentSpriteFx(
		string name,
		Texture2D? texture,
		int zIndex,
		Vector2 globalPosition,
		Vector2 scale,
		Color modulate,
		float rotation = 0.0f)
	{
		Sprite2D sprite = _animationPresenter.RentSprite(
			this,
			name,
			texture,
			zIndex,
			CanvasItem.TextureFilterEnum.Linear);
		sprite.GlobalPosition = globalPosition;
		sprite.Rotation = rotation;
		sprite.Scale = scale;
		sprite.Modulate = modulate;
		return sprite;
	}

	private interface IGameBoardAnimationPresenter
	{
		Control RentControl(Node parent, string name, Vector2 size, int zIndex, bool zAsRelative = false);
		TextureRect RentTextureRect(Node parent, string name, Texture2D? texture, Vector2 size, int zIndex, CanvasItem.TextureFilterEnum textureFilter, bool zAsRelative = true);
		Label RentLabel(Node parent, string name, Vector2 size, int zIndex);
		Sprite2D RentSprite(Node parent, string name, Texture2D? texture, int zIndex, CanvasItem.TextureFilterEnum textureFilter, bool zAsRelative = false);
		void Release(Node? node);
		void ClearPools();
	}

	private sealed class GameBoardAnimationPresenter : IGameBoardAnimationPresenter
	{
		private const int MaxPooledControls = 32;
		private const int MaxPooledTextureRects = 64;
		private const int MaxPooledLabels = 24;
		private const int MaxPooledSprites = 32;
		private static readonly StringName PooledMeta = new("game_board_animation_pooled");
		private static readonly StringName InPoolMeta = new("game_board_animation_in_pool");

		private readonly Stack<Control> _controlPool = new();
		private readonly Stack<TextureRect> _textureRectPool = new();
		private readonly Stack<Label> _labelPool = new();
		private readonly Stack<Sprite2D> _spritePool = new();

		public Control RentControl(Node parent, string name, Vector2 size, int zIndex, bool zAsRelative = false)
		{
			Control control = Rent(_controlPool) ?? new Control();
			control.SetMeta(PooledMeta, true);
			control.SetMeta(InPoolMeta, false);
			control.Name = name;
			control.MouseFilter = Control.MouseFilterEnum.Ignore;
			control.Size = size;
			control.PivotOffset = size / 2.0f;
			control.Position = Vector2.Zero;
			control.Rotation = 0.0f;
			control.RotationDegrees = 0.0f;
			control.Scale = Vector2.One;
			control.Modulate = Colors.White;
			control.Visible = true;
			control.ZIndex = zIndex;
			control.ZAsRelative = zAsRelative;
			parent.AddChild(control);
			return control;
		}

		public TextureRect RentTextureRect(
			Node parent,
			string name,
			Texture2D? texture,
			Vector2 size,
			int zIndex,
			CanvasItem.TextureFilterEnum textureFilter,
			bool zAsRelative = true)
		{
			TextureRect textureRect = Rent(_textureRectPool) ?? new TextureRect();
			textureRect.SetMeta(PooledMeta, true);
			textureRect.SetMeta(InPoolMeta, false);
			textureRect.Name = name;
			textureRect.Texture = texture;
			textureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			textureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			textureRect.TextureFilter = textureFilter;
			textureRect.MouseFilter = Control.MouseFilterEnum.Ignore;
			textureRect.Position = Vector2.Zero;
			textureRect.Size = size;
			textureRect.PivotOffset = size / 2.0f;
			textureRect.Rotation = 0.0f;
			textureRect.RotationDegrees = 0.0f;
			textureRect.Scale = Vector2.One;
			textureRect.Modulate = Colors.White;
			textureRect.Visible = true;
			textureRect.ZIndex = zIndex;
			textureRect.ZAsRelative = zAsRelative;
			parent.AddChild(textureRect);
			return textureRect;
		}

		public Label RentLabel(Node parent, string name, Vector2 size, int zIndex)
		{
			Label label = Rent(_labelPool) ?? new Label();
			label.SetMeta(PooledMeta, true);
			label.SetMeta(InPoolMeta, false);
			label.Name = name;
			label.Text = string.Empty;
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.VerticalAlignment = VerticalAlignment.Center;
			label.MouseFilter = Control.MouseFilterEnum.Ignore;
			label.Position = Vector2.Zero;
			label.Size = size;
			label.PivotOffset = size / 2.0f;
			label.Rotation = 0.0f;
			label.RotationDegrees = 0.0f;
			label.Scale = Vector2.One;
			label.Modulate = Colors.White;
			label.Visible = true;
			label.ZIndex = zIndex;
			parent.AddChild(label);
			return label;
		}

		public Sprite2D RentSprite(
			Node parent,
			string name,
			Texture2D? texture,
			int zIndex,
			CanvasItem.TextureFilterEnum textureFilter,
			bool zAsRelative = false)
		{
			Sprite2D sprite = Rent(_spritePool) ?? new Sprite2D();
			sprite.SetMeta(PooledMeta, true);
			sprite.SetMeta(InPoolMeta, false);
			sprite.Name = name;
			sprite.Texture = texture;
			sprite.TextureFilter = textureFilter;
			sprite.Centered = true;
			sprite.Position = Vector2.Zero;
			sprite.Rotation = 0.0f;
			sprite.Scale = Vector2.One;
			sprite.Modulate = Colors.White;
			sprite.Visible = true;
			sprite.ZIndex = zIndex;
			sprite.ZAsRelative = zAsRelative;
			parent.AddChild(sprite);
			return sprite;
		}

		public void Release(Node? node)
		{
			if (node == null || !GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion())
			{
				return;
			}

			if (node.HasMeta(InPoolMeta) && node.GetMeta(InPoolMeta).AsBool())
			{
				return;
			}

			foreach (Node child in new List<Node>(node.GetChildren()))
			{
				Release(child);
			}

			node.GetParent()?.RemoveChild(node);
			if (node is TextureRect textureRect && textureRect.HasMeta(PooledMeta))
			{
				ResetTextureRect(textureRect);
				PushOrFree(_textureRectPool, textureRect, MaxPooledTextureRects);
				return;
			}

			if (node is Label label && label.HasMeta(PooledMeta))
			{
				ResetLabel(label);
				PushOrFree(_labelPool, label, MaxPooledLabels);
				return;
			}

			if (node is Sprite2D sprite && sprite.HasMeta(PooledMeta))
			{
				ResetSprite(sprite);
				PushOrFree(_spritePool, sprite, MaxPooledSprites);
				return;
			}

			if (node is Control control && control.HasMeta(PooledMeta))
			{
				ResetControl(control);
				PushOrFree(_controlPool, control, MaxPooledControls);
				return;
			}

			node.Free();
		}

		public void ClearPools()
		{
			FreePool(_controlPool);
			FreePool(_textureRectPool);
			FreePool(_labelPool);
			FreePool(_spritePool);
		}

		private static T? Rent<T>(Stack<T> pool) where T : Node
		{
			while (pool.Count > 0)
			{
				T node = pool.Pop();
				if (GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
				{
					return node;
				}
			}

			return null;
		}

		private static void PushOrFree<T>(Stack<T> pool, T node, int maxCount) where T : Node
		{
			node.SetMeta(InPoolMeta, true);
			if (pool.Count >= maxCount)
			{
				node.Free();
				return;
			}

			pool.Push(node);
		}

		private static void FreePool<T>(Stack<T> pool) where T : Node
		{
			while (pool.Count > 0)
			{
				T node = pool.Pop();
				if (GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
				{
					node.GetParent()?.RemoveChild(node);
					node.Free();
				}
			}
		}

		private static void ResetTextureRect(TextureRect textureRect)
		{
			ResetControl(textureRect);
			textureRect.Name = "PooledAnimationTexture";
			textureRect.Texture = null;
			if (textureRect.HasMeta(CardBackAnimatedMeta))
			{
				textureRect.RemoveMeta(CardBackAnimatedMeta);
			}

			textureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			textureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			textureRect.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
		}

		private static void ResetLabel(Label label)
		{
			ResetControl(label);
			label.Name = "PooledAnimationLabel";
			label.Text = string.Empty;
			label.RemoveThemeFontSizeOverride("font_size");
			label.RemoveThemeColorOverride("font_color");
			label.RemoveThemeColorOverride("font_shadow_color");
			label.RemoveThemeConstantOverride("shadow_offset_x");
			label.RemoveThemeConstantOverride("shadow_offset_y");
		}

		private static void ResetSprite(Sprite2D sprite)
		{
			sprite.Name = "PooledAnimationSprite";
			sprite.Texture = null;
			sprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			sprite.Centered = true;
			sprite.Position = Vector2.Zero;
			sprite.GlobalPosition = Vector2.Zero;
			sprite.Rotation = 0.0f;
			sprite.Scale = Vector2.One;
			sprite.Modulate = Colors.White;
			sprite.Visible = true;
			sprite.ZIndex = 0;
			sprite.ZAsRelative = true;
		}

		private static void ResetControl(Control control)
		{
			control.Name = "PooledAnimationControl";
			control.MouseFilter = Control.MouseFilterEnum.Ignore;
			control.Position = Vector2.Zero;
			control.GlobalPosition = Vector2.Zero;
			control.Size = Vector2.Zero;
			control.PivotOffset = Vector2.Zero;
			control.Rotation = 0.0f;
			control.RotationDegrees = 0.0f;
			control.Scale = Vector2.One;
			control.Modulate = Colors.White;
			control.Visible = true;
			control.ZIndex = 0;
			control.ZAsRelative = true;
		}
	}
}
