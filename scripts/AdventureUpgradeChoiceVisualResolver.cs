using System;
using System.Collections.Generic;
using Godot;

public static class AdventureUpgradeChoiceVisualResolver
{
	public const string CardBaseTexturePath =
		"res://art/ui/adventure_upgrade_choice_table/run-001/base/processed/raw-sheet-clean.png";

	private static readonly Dictionary<string, string> SuitTexturePaths = new(StringComparer.Ordinal)
	{
		["hearts"] = "res://art/ui/adventure_upgrade_choice_table/run-001/suits/processed/suit-1.png",
		["diamonds"] = "res://art/ui/adventure_upgrade_choice_table/run-001/suits/processed/suit-2.png",
		["clubs"] = "res://art/ui/adventure_upgrade_choice_table/run-001/suits/processed/suit-3.png",
		["spades"] = "res://art/ui/adventure_upgrade_choice_table/run-001/suits/processed/suit-4.png",
	};

	private static Texture2D? _cardBaseTexture;
	private static readonly Dictionary<string, Texture2D> SuitTextureCache = new(StringComparer.Ordinal);

	public static Texture2D? LoadCardBaseTexture()
	{
		_cardBaseTexture ??= LoadTexture(CardBaseTexturePath);
		return _cardBaseTexture;
	}

	public static Texture2D? LoadSuitTexture(string suit)
	{
		string normalizedSuit = NormalizeSuit(suit);
		if (SuitTextureCache.TryGetValue(normalizedSuit, out Texture2D? cachedTexture))
		{
			return cachedTexture;
		}

		if (!SuitTexturePaths.TryGetValue(normalizedSuit, out string? texturePath))
		{
			GD.PushWarning($"Missing adventure upgrade choice suit mapping: {suit}");
			return null;
		}

		Texture2D? texture = LoadTexture(texturePath);
		if (texture != null)
		{
			SuitTextureCache[normalizedSuit] = texture;
		}
		return texture;
	}

	public static string ResolveSuitTexturePath(string suit)
	{
		return SuitTexturePaths.TryGetValue(NormalizeSuit(suit), out string? texturePath)
			? texturePath
			: string.Empty;
	}

	private static Texture2D? LoadTexture(string texturePath)
	{
		Texture2D? texture = GD.Load<Texture2D>(texturePath);
		if (texture == null)
		{
			GD.PushWarning($"Missing adventure upgrade choice texture: {texturePath}");
		}
		return texture;
	}

	private static string NormalizeSuit(string suit)
	{
		return suit?.Trim().ToLowerInvariant() switch
		{
			null => string.Empty,
			"heart" => "hearts",
			"diamond" => "diamonds",
			"club" => "clubs",
			"spade" => "spades",
			string normalized => normalized,
		};
	}
}
