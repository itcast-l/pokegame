using System.Collections.Generic;
using Godot;

public partial class MainMenuPlayerCountDialogPanelArt : PanelContainer
{
	private const string ReferenceTexturePath = "res://assets/ui/player_count_dialog_reference_panel.png";
	private const float BackgroundLuminanceThreshold = 0.16f;
	private const float BackgroundSaturationThreshold = 0.20f;

	private Texture2D? _referenceTexture;

	public override void _Ready()
	{
		byte[] bytes = Godot.FileAccess.GetFileAsBytes(ReferenceTexturePath);
		if (bytes.Length > 0)
		{
			Image image = new();
			if (image.LoadPngFromBuffer(bytes) == Error.Ok)
			{
				TrimEdgeBackground(image);
				_referenceTexture = ImageTexture.CreateFromImage(image);
			}
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_referenceTexture == null)
		{
			return;
		}

		DrawTextureRect(_referenceTexture, new Rect2(Vector2.Zero, Size), false);
	}

	private static void TrimEdgeBackground(Image image)
	{
		int width = image.GetWidth();
		int height = image.GetHeight();
		if (width <= 0 || height <= 0)
		{
			return;
		}

		bool[,] visited = new bool[width, height];
		Queue<Vector2I> open = new();

		void EnqueueIfBackground(int x, int y)
		{
			if (x < 0 || x >= width || y < 0 || y >= height || visited[x, y])
			{
				return;
			}

			Color color = image.GetPixel(x, y);
			if (!IsEdgeBackground(color))
			{
				return;
			}

			visited[x, y] = true;
			open.Enqueue(new Vector2I(x, y));
		}

		for (int x = 0; x < width; x++)
		{
			EnqueueIfBackground(x, 0);
			EnqueueIfBackground(x, height - 1);
		}

		for (int y = 0; y < height; y++)
		{
			EnqueueIfBackground(0, y);
			EnqueueIfBackground(width - 1, y);
		}

		while (open.Count > 0)
		{
			Vector2I point = open.Dequeue();
			Color color = image.GetPixel(point.X, point.Y);
			color.A = 0.0f;
			image.SetPixel(point.X, point.Y, color);

			EnqueueIfBackground(point.X - 1, point.Y);
			EnqueueIfBackground(point.X + 1, point.Y);
			EnqueueIfBackground(point.X, point.Y - 1);
			EnqueueIfBackground(point.X, point.Y + 1);
		}
	}

	private static bool IsEdgeBackground(Color color)
	{
		if (color.A <= 0.0f)
		{
			return false;
		}

		float maxChannel = Mathf.Max(color.R, Mathf.Max(color.G, color.B));
		float minChannel = Mathf.Min(color.R, Mathf.Min(color.G, color.B));
		float saturation = maxChannel - minChannel;
		float luminance = color.R * 0.2126f + color.G * 0.7152f + color.B * 0.0722f;
		return luminance <= BackgroundLuminanceThreshold && saturation <= BackgroundSaturationThreshold;
	}
}
