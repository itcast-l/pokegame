using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private void LoadCardBackTextures()
	{
		_cardBackTexture = CardBackFrames.Length > 0 ? CardBackFrames[0] : null;
	}

	private void LoadScoreFillEffectTextures()
	{
		_scoreFillEffectFrames.Clear();
		_scoreFillEffectFrames.AddRange(ScoreFillEffectFrames);
	}

	private void LoadEnemySpecialAbilityAvatarFxTextures()
	{
		_enemySpecialAbilityAvatarFxFrames.Clear();
		_enemySpecialAbilityAvatarFxFrames.AddRange(EnemySpecialAbilityAvatarFxFrames);
	}

	private void LoadMantaRayTideShiftCycloneFxTextures()
	{
		_mantaRayTideShiftCycloneFxFrames.Clear();
		_mantaRayTideShiftCycloneFxFrames.AddRange(MantaRayTideShiftCycloneFxFrames);
	}

	private void LoadLeafySeadragonBladeProjectileTextures()
	{
		_leafySeadragonBladeFlightFrames.Clear();
		_leafySeadragonBladeFlightFrames.AddRange(LeafySeadragonBladeFlightFrames);
		_leafySeadragonBladeImpactFrames.Clear();
		_leafySeadragonBladeImpactFrames.AddRange(LeafySeadragonBladeImpactFrames);
	}

	private void LoadBossKRoyalSeizureLanceTextures()
	{
		_bossKRoyalSeizureLanceFrames.Clear();
		_bossKRoyalSeizureLanceFrames.AddRange(BossKRoyalSeizureLanceFrames);
	}

	private void LoadAdventureDamageEffectTextures()
	{
		_adventureDamageEffectFrames.Clear();
		_adventureDamageEffectFrames.AddRange(CatAdventureDamageEffectFrames);
	}

	private void LoadAdventureEnemyDamageEffectTextures()
	{
		_adventureEnemyDamageEffectFrames.Clear();
		_dragonPrinceDamageHitFlightFrames.Clear();
		_dragonPrinceDamageHitImpactFrames.Clear();
		CharacterAvatarDefinition enemyDefinition = CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id);
		Texture2D[] frames = !string.IsNullOrWhiteSpace(enemyDefinition.DamageEffectFramePattern)
			? FrameAnimationHelpers.LoadTextureFrames(
				enemyDefinition.DamageEffectFramePattern,
				Mathf.Max(1, enemyDefinition.DamageEffectFrameCount),
				$"{enemyDefinition.Id} adventure damage effect",
				enemyDefinition.DamageEffectPreferImageFile)
			: DefaultAdventureDamageEffectFrames;
		_adventureEnemyDamageEffectFrames.AddRange(frames);
		CacheDragonPrinceDamageHitFrameSlices(enemyDefinition);
	}

	private void CacheDragonPrinceDamageHitFrameSlices(CharacterAvatarDefinition enemyDefinition)
	{
		if (!string.Equals(enemyDefinition.AttackEffectStyle, AdventureAttackEffectStyles.DragonPrinceHit, StringComparison.Ordinal)
			|| _adventureEnemyDamageEffectFrames.Count == 0)
		{
			return;
		}

		int flightFrameCount = Math.Min(DragonPrinceDamageHitFlightFrameCount, _adventureEnemyDamageEffectFrames.Count);
		AddFrameSlice(_adventureEnemyDamageEffectFrames, 0, flightFrameCount, _dragonPrinceDamageHitFlightFrames);
		AddFrameSlice(
			_adventureEnemyDamageEffectFrames,
			flightFrameCount,
			_adventureEnemyDamageEffectFrames.Count - flightFrameCount,
			_dragonPrinceDamageHitImpactFrames);
		if (_dragonPrinceDamageHitImpactFrames.Count == 0)
		{
			_dragonPrinceDamageHitImpactFrames.Add(_adventureEnemyDamageEffectFrames[^1]);
		}
	}

	private static void AddFrameSlice(IReadOnlyList<Texture2D> frames, int startIndex, int count, List<Texture2D> target)
	{
		for (int index = 0; index < count; index++)
		{
			int frameIndex = startIndex + index;
			if (frameIndex >= 0 && frameIndex < frames.Count)
			{
				target.Add(frames[frameIndex]);
			}
		}
	}

	private void LoadQuestionCardTransformTextures()
	{
		_questionCardTransformFrames.Clear();
		_questionCardTransformFrames.AddRange(QuestionCardTransformFrames);
	}

	private void LoadSpecialCardTriggerEffectTextures()
	{
		_specialCardTriggerEffectFrames.Clear();
		_specialCardTriggerEffectFrames.AddRange(SpecialCardTriggerEffectFrames);
		_specialCardTriggerActivationFrames.Clear();
		_specialCardTriggerActivationFrames.AddRange(SpecialCardTriggerActivationFrames);
	}

	private void LoadDragonPrinceSpecialJackTexture()
	{
		_dragonPrinceSpecialJackTexture = null;
		_dragonPrinceSpecialJackRank = string.Empty;
		if (!adventure_mode || !CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id).HasAbility(AdventureEnemyAbilities.DragonPrince))
		{
			return;
		}

		string texturePath = GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.SpecialJackTexturePath, string.Empty);
		_dragonPrinceSpecialJackRank = GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.SpecialJackRank, string.Empty);
		if (string.IsNullOrWhiteSpace(texturePath) || string.IsNullOrWhiteSpace(_dragonPrinceSpecialJackRank))
		{
			GD.PushWarning($"Missing dragon prince special card ability values for {adventure_enemy_avatar_id}.");
			return;
		}

		_dragonPrinceSpecialJackTexture = GD.Load<Texture2D>(texturePath);
		if (_dragonPrinceSpecialJackTexture == null)
		{
			GD.PushWarning($"Missing dragon prince special card texture: {texturePath}");
		}
	}

	private void LoadBombPairDestroyEffectTextures()
	{
		_bombPairDestroyEffectFrames.Clear();
		_bombPairDestroyEffectFrames.AddRange(BombPairDestroyEffectFrames);
	}

	private void LoadLensRevealWispTextures()
	{
		_lensRevealWispFrames.Clear();
		_lensRevealWispFrames.AddRange(LensRevealWispFrames);
	}

	private void LoadAttachmentCardOverlayTextures()
	{
		_itemAttachmentOverlaysById.Clear();
		foreach (string itemCardId in _itemCardCatalog.AllIds)
		{
			if (!_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
				|| string.IsNullOrWhiteSpace(definition.AttachmentOverlayTexturePath))
			{
				continue;
			}

			Texture2D? overlayTexture = GD.Load<Texture2D>(definition.AttachmentOverlayTexturePath);
			if (overlayTexture == null)
			{
				GD.PushWarning($"Missing item attachment overlay texture for {itemCardId}: {definition.AttachmentOverlayTexturePath}");
				continue;
			}

			_itemAttachmentOverlaysById[itemCardId] = new ItemAttachmentOverlay(overlayTexture, definition.AttachmentOverlayPriority);
		}

		_slimeCardOverlayTexture = GD.Load<Texture2D>(SlimeCardOverlayPath);
		if (_slimeCardOverlayTexture == null)
		{
			GD.PushWarning($"Missing slime card overlay texture: {SlimeCardOverlayPath}");
		}
	}

	private void LoadMagnetCountdownFxTextures()
	{
		_magnetCountdownFrameTextures.Clear();
		_magnetCountdownFrameTextures.AddRange(MagnetCountdownFrames);
		_tableShuffleVortexFrames.Clear();
		_tableShuffleVortexFrames.AddRange(TableShuffleVortexFrames);
		_magnetAttachmentApplyRingFrames.Clear();
		_magnetAttachmentApplyRingFrames.AddRange(MagnetAttachmentApplyRingFrames);
	}

	private void MarkAnimatedCardBack(TextureRect cardBack)
	{
		if (cardBack.HasMeta(CardBackAnimatedMeta))
		{
			SetAnimatedCardBackPlayback(cardBack, true);
			return;
		}

		cardBack.SetMeta(CardBackAnimatedMeta, true);
		if (CardBackFrames.Length <= 1)
		{
			return;
		}

		var animationPlayer = new AnimationPlayer
		{
			Name = "CardBackAnimationPlayer",
			RootNode = new NodePath(".."),
		};
		cardBack.AddChild(animationPlayer);
		FrameAnimationHelpers.ConfigureTextureAnimation(
			animationPlayer,
			new StringName("card_back_frames"),
			new NodePath(".:texture"),
			CardBackFrames,
			1.0 / CardBackAnimationFps,
			loops: true);
		animationPlayer.Play("card_back_frames");
	}

	private static void SetAnimatedCardBackPlayback(TextureRect cardBack, bool shouldPlay)
	{
		if (cardBack.GetNodeOrNull<AnimationPlayer>("CardBackAnimationPlayer") is not AnimationPlayer animationPlayer)
		{
			return;
		}

		if (shouldPlay)
		{
			if (!animationPlayer.IsPlaying())
			{
				animationPlayer.Play("card_back_frames");
			}

			return;
		}

		if (animationPlayer.IsPlaying())
		{
			animationPlayer.Stop();
		}
	}

	private void LoadItemCardTextures()
	{
		_itemCardTextures.Clear();
		foreach (string itemCardId in _itemCardCatalog.AllIds)
		{
			if (!_itemCardCatalog.TryGetFirstFrameTexture(itemCardId, out Texture2D texture))
			{
				GD.PushWarning($"Missing loaded item card texture for {itemCardId}.");
				continue;
			}

			_itemCardTextures[itemCardId] = texture;
		}
	}

	private ItemCard? CreateItemCard(string itemCardId)
	{
		if (!IsSpecialItemCardEnabled(itemCardId))
		{
			return null;
		}

		if (!_itemCardTextures.TryGetValue(itemCardId, out Texture2D? texture))
		{
			GD.PushWarning($"Missing loaded item card texture for {itemCardId}.");
			return null;
		}

		return new ItemCard(itemCardId, texture);
	}

	private bool IsSpecialItemCardEnabled(string itemCardId)
	{
		if (!item_mode)
		{
			return false;
		}

		return _itemCardCatalog.IsEnabled(itemCardId);
	}

	private static bool IsExcludedDeckRank(string rank)
	{
		return CardRankCatalog.ShouldExcludeFromStandardDeck(rank);
	}

	private void LoadItemCardEnabledStates()
	{
		_enabledSceneItemCardIds.Clear();
		_itemCardDescriptions.Clear();
		if (!item_mode)
		{
			return;
		}

		foreach (string itemCardId in _itemCardCatalog.AllIds)
		{
			if (!_itemCardCatalog.IsEnabled(itemCardId))
			{
				continue;
			}

			_enabledSceneItemCardIds.Add(itemCardId);
			_itemCardDescriptions[itemCardId] = GetItemDescription(_itemCardCatalog.GetDescriptionKey(itemCardId));
		}
	}
}
