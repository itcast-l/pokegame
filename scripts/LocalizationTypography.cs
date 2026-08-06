using System;
using System.Collections.Generic;
using Godot;

public static class LocalizationTypography
{
	private const string OriginalAlignmentMeta = "localization_original_alignment";
	private static readonly Dictionary<string, Font> Fonts = new(StringComparer.Ordinal);

	public static Font GetCurrentFont()
	{
		string locale = Localization.NormalizeLocale(TranslationServer.GetLocale());
		string resourcePath = locale switch
		{
			"ar" => "res://resources/fonts/noto_arabic.tres",
			"th" => "res://resources/fonts/noto_thai.tres",
			"zh_CN" => "res://resources/fonts/noto_sc.tres",
			"zh_TW" => "res://resources/fonts/noto_tc.tres",
			"ja" => "res://resources/fonts/noto_jp.tres",
			"ko" => "res://resources/fonts/noto_kr.tres",
			_ => "res://resources/fonts/noto_latin.tres",
		};

		if (!Fonts.TryGetValue(resourcePath, out Font? font))
		{
			font = GD.Load<Font>(resourcePath)
				?? throw new InvalidOperationException($"Missing project localization font: {resourcePath}");
			Fonts[resourcePath] = font;
		}

		return font;
	}

	public static void Apply(Control root, bool useLocaleLayoutDirection)
	{
		Font font = GetCurrentFont();
		Theme theme = root.Theme ?? new Theme();
		theme.DefaultFont = font;
		root.Theme = theme;
		root.LayoutDirection = useLocaleLayoutDirection
			? Control.LayoutDirectionEnum.ApplicationLocale
			: Control.LayoutDirectionEnum.Ltr;
		ApplyTextControls(root, IsRtlLocale());
	}

	public static void ApplyDefaultFont(Theme theme)
	{
		theme.DefaultFont = GetCurrentFont();
	}

	public static FontVariation CreateCurrentFontVariation(float weight, float embolden)
	{
		Font localizedFont = GetCurrentFont();
		Font baseFont = localizedFont;
		Godot.Collections.Array<Font>? fallbacks = null;
		if (localizedFont is FontVariation localizationVariation && localizationVariation.BaseFont != null)
		{
			baseFont = localizationVariation.BaseFont;
			fallbacks = localizationVariation.Fallbacks;
		}

		var variation = new FontVariation
		{
			BaseFont = baseFont,
			VariationEmbolden = embolden,
			VariationOpentype = new Godot.Collections.Dictionary
			{
				{ "wght", weight },
			},
		};
		if (fallbacks != null)
		{
			variation.Fallbacks = fallbacks;
		}

		return variation;
	}

	private static bool IsRtlLocale()
	{
		return TranslationServer.GetLocale().StartsWith("ar", StringComparison.OrdinalIgnoreCase);
	}

	private static void ApplyTextControls(Node node, bool rtl)
	{
		switch (node)
		{
			case Label label:
				label.TextDirection = Control.TextDirection.Auto;
				ApplyLocaleAlignment(label, rtl);
				break;
			case LineEdit lineEdit:
				lineEdit.TextDirection = Control.TextDirection.Auto;
				ApplyLocaleAlignment(lineEdit, rtl);
				break;
			case TextEdit textEdit:
				textEdit.TextDirection = Control.TextDirection.Auto;
				break;
			case RichTextLabel richTextLabel:
				richTextLabel.TextDirection = Control.TextDirection.Auto;
				break;
			case Button button:
				button.TextDirection = Control.TextDirection.Auto;
				break;
		}

		foreach (Node child in node.GetChildren())
		{
			ApplyTextControls(child, rtl);
		}
	}

	private static void ApplyLocaleAlignment(Label label, bool rtl)
	{
		HorizontalAlignment original = GetOriginalAlignment(label, label.HorizontalAlignment);
		label.HorizontalAlignment = rtl && original == HorizontalAlignment.Left
			? HorizontalAlignment.Right
			: original;
	}

	private static void ApplyLocaleAlignment(LineEdit lineEdit, bool rtl)
	{
		HorizontalAlignment original = GetOriginalAlignment(lineEdit, lineEdit.Alignment);
		lineEdit.Alignment = rtl && original == HorizontalAlignment.Left
			? HorizontalAlignment.Right
			: original;
	}

	private static HorizontalAlignment GetOriginalAlignment(Control control, HorizontalAlignment current)
	{
		if (!control.HasMeta(OriginalAlignmentMeta))
		{
			control.SetMeta(OriginalAlignmentMeta, (int)current);
			return current;
		}

		return (HorizontalAlignment)control.GetMeta(OriginalAlignmentMeta).AsInt32();
	}
}
