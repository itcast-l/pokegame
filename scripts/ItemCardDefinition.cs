using System;
using System.Collections.Generic;

public readonly struct ItemCardDefinition
{
	public ItemCardDefinition(
		string id,
		string displayName,
		string spriteName,
		string descriptionKey,
		bool enabledInGame,
		bool playableFromHand,
		bool canBeQuestionReward,
		string artRunName,
		string framePathPattern,
		int frameCount,
		int sortOrder,
		string rarity,
		string tagsCsv,
		string attachmentOverlayTexturePath,
		int attachmentOverlayPriority,
		string playPresentation,
		string trigger,
		string effectType,
		string effectValuesRaw,
		int priority,
		string attachedTrigger,
		string attachedEffectType,
		string attachedEffectValuesRaw,
		int attachedPriority,
		string icon,
		int weight,
		string description = "")
	{
		Id = id;
		DisplayName = displayName;
		SpriteName = spriteName;
		DescriptionKey = descriptionKey;
		EnabledInGame = enabledInGame;
		PlayableFromHand = playableFromHand;
		CanBeQuestionReward = canBeQuestionReward;
		ArtRunName = artRunName;
		FramePathPattern = string.IsNullOrWhiteSpace(framePathPattern) ? string.Empty : framePathPattern;
		FrameCount = frameCount;
		SortOrder = sortOrder;
		Rarity = rarity;
		TagsCsv = tagsCsv;
		AttachmentOverlayTexturePath = attachmentOverlayTexturePath;
		AttachmentOverlayPriority = attachmentOverlayPriority;
		PlayPresentation = string.IsNullOrWhiteSpace(playPresentation)
			? CardPlayPresentationTypes.DirectTrigger
			: playPresentation;
		Trigger = trigger;
		EffectType = effectType;
		EffectValuesRaw = effectValuesRaw;
		EffectValues = CardEffectValues.Parse(effectValuesRaw);
		Priority = priority;
		AttachedTrigger = attachedTrigger;
		AttachedEffectType = attachedEffectType;
		AttachedEffectValuesRaw = attachedEffectValuesRaw;
		AttachedEffectValues = CardEffectValues.Parse(attachedEffectValuesRaw);
		AttachedPriority = attachedPriority;
		Icon = icon;
		Weight = weight;
		Description = description;
		Tags = ParseTags(tagsCsv);
	}

	public string Id { get; }
	public string DisplayName { get; }
	public string SpriteName { get; }
	public string DescriptionKey { get; }
	public bool EnabledInGame { get; }
	public bool PlayableFromHand { get; }
	public bool CanBeQuestionReward { get; }
	public string ArtRunName { get; }
	public string FramePathPattern { get; }
	public int FrameCount { get; }
	public int SortOrder { get; }
	public string Rarity { get; }
	public IReadOnlyList<string> Tags { get; }
	public string TagsCsv { get; }
	public string AttachmentOverlayTexturePath { get; }
	public int AttachmentOverlayPriority { get; }
	public string PlayPresentation { get; }
	public string Trigger { get; }
	public string EffectType { get; }
	public string EffectValuesRaw { get; }
	public CardEffectValues EffectValues { get; }
	public int Priority { get; }
	public string AttachedTrigger { get; }
	public string AttachedEffectType { get; }
	public string AttachedEffectValuesRaw { get; }
	public CardEffectValues AttachedEffectValues { get; }
	public int AttachedPriority { get; }
	public string Icon { get; }
	public int Weight { get; }
	public string Description { get; }

	public ItemCardEffectRule EffectRule => new(Trigger, EffectType, EffectValues, Priority);
	public ItemCardEffectRule AttachedEffectRule => new(AttachedTrigger, AttachedEffectType, AttachedEffectValues, AttachedPriority);

	public bool HasTag(string tag)
	{
		foreach (string existingTag in Tags)
		{
			if (string.Equals(existingTag, tag, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static IReadOnlyList<string> ParseTags(string tagsCsv)
	{
		if (string.IsNullOrWhiteSpace(tagsCsv))
		{
			return Array.Empty<string>();
		}

		string[] tags = tagsCsv.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return tags.Length == 0 ? Array.Empty<string>() : tags;
	}
}
