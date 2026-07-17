using Godot;
using System;

public static class AdventureUpgradeSuitRarityVisual
{
	private const string ShaderPath = "res://shaders/adventure_upgrade_suit_rarity.gdshader";
	private const string CardCleanupShaderPath = "res://shaders/adventure_upgrade_card_cleanup.gdshader";

	public static ShaderMaterial? CreateCardCleanupMaterial()
	{
		Shader? shader = GD.Load<Shader>(CardCleanupShaderPath);
		if (shader == null)
		{
			GD.PushWarning($"Missing adventure upgrade card cleanup shader: {CardCleanupShaderPath}");
			return null;
		}

		return new ShaderMaterial { Shader = shader };
	}

	public static ShaderMaterial? CreateMaterial(string tier = "common")
	{
		Shader? shader = GD.Load<Shader>(ShaderPath);
		if (shader == null)
		{
			GD.PushWarning($"Missing adventure upgrade suit rarity shader: {ShaderPath}");
			return null;
		}

		var material = new ShaderMaterial { Shader = shader };
		ApplyTier(material, tier);
		return material;
	}

	public static void ApplyTier(ShaderMaterial? material, string tier)
	{
		material?.SetShaderParameter("rarity_color", ResolveTierColor(tier));
	}

	public static Color ResolveTierColor(string tier)
	{
		return Normalize(tier) switch
		{
			"uncommon" => new Color(0.18f, 0.78f, 0.38f, 1.0f),
			"rare" => new Color(0.62f, 0.26f, 0.96f, 1.0f),
			_ => new Color(0.94f, 0.96f, 1.0f, 1.0f),
		};
	}

	private static string Normalize(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? string.Empty
			: value.Trim().ToLowerInvariant();
	}
}
