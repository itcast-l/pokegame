using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private bool _scoreFillEffectResourcesLoaded;
	private bool _specialCardTriggerEffectResourcesLoaded;
	private bool _enemySpecialAbilityAvatarFxResourcesLoaded;
	private bool _mantaRayTideShiftCycloneFxResourcesLoaded;
	private bool _leafySeadragonBladeProjectileResourcesLoaded;
	private bool _bossKRoyalSeizureLanceResourcesLoaded;
	private bool _adventureDamageEffectResourcesLoaded;
	private bool _adventureEnemyDamageEffectResourcesLoaded;
	private bool _questionCardTransformResourcesLoaded;
	private bool _bombPairDestroyEffectResourcesLoaded;
	private bool _lensRevealWispResourcesLoaded;
	private bool _magnetCountdownFxResourcesLoaded;
	private bool _adventureTraceHuntMarkerResourcesLoaded;

	private void EnsureCommonGameplayFxResourcesLoaded()
	{
		if (_commonGameplayFxResourcesLoaded)
		{
			return;
		}

		EnsureScoreFillEffectTexturesLoaded();
		EnsureSpecialCardTriggerEffectTexturesLoaded();
		_commonGameplayFxResourcesLoaded = true;
	}

	private void EnsureAdventureModeResourcesLoaded()
	{
		if (_adventureModeResourcesLoaded)
		{
			return;
		}

		EnsureEnemySpecialAbilityAvatarFxTexturesLoaded();
		EnsureMantaRayTideShiftCycloneFxTexturesLoaded();
		EnsureLeafySeadragonBladeProjectileTexturesLoaded();
		EnsureBossKRoyalSeizureLanceTexturesLoaded();
		EnsureAdventureDamageEffectTexturesLoaded();
		EnsureAdventureEnemyDamageEffectTexturesLoaded();
		EnsureAdventureTraceHuntMarkerTexturesLoaded();
		LoadDragonPrinceSpecialJackTexture();
		_adventureModeResourcesLoaded = true;
	}

	private void EnsureItemModeResourcesLoaded()
	{
		if (_itemModeResourcesLoaded)
		{
			return;
		}

		LoadItemCardTextures();
		LoadAttachmentCardOverlayTextures();
		_itemModeResourcesLoaded = true;
	}

	private void EnsureItemModeOptionalFxResourcesLoaded()
	{
		if (_itemModeOptionalFxResourcesLoaded)
		{
			return;
		}

		EnsureQuestionCardTransformTexturesLoaded();
		EnsureBombPairDestroyEffectTexturesLoaded();
		EnsureLensRevealWispTexturesLoaded();
		EnsureMagnetCountdownFxTexturesLoaded();
		_itemModeOptionalFxResourcesLoaded = true;
	}

	private void EnsureScoreFillEffectTexturesLoaded()
	{
		if (_scoreFillEffectResourcesLoaded)
		{
			return;
		}

		LoadScoreFillEffectTextures();
		_scoreFillEffectResourcesLoaded = true;
	}

	private void EnsureSpecialCardTriggerEffectTexturesLoaded()
	{
		if (_specialCardTriggerEffectResourcesLoaded)
		{
			return;
		}

		LoadSpecialCardTriggerEffectTextures();
		_specialCardTriggerEffectResourcesLoaded = true;
	}

	private void EnsureEnemySpecialAbilityAvatarFxTexturesLoaded()
	{
		if (_enemySpecialAbilityAvatarFxResourcesLoaded)
		{
			return;
		}

		LoadEnemySpecialAbilityAvatarFxTextures();
		_enemySpecialAbilityAvatarFxResourcesLoaded = true;
	}

	private void EnsureMantaRayTideShiftCycloneFxTexturesLoaded()
	{
		if (_mantaRayTideShiftCycloneFxResourcesLoaded)
		{
			return;
		}

		LoadMantaRayTideShiftCycloneFxTextures();
		_mantaRayTideShiftCycloneFxResourcesLoaded = true;
	}

	private void EnsureLeafySeadragonBladeProjectileTexturesLoaded()
	{
		if (_leafySeadragonBladeProjectileResourcesLoaded)
		{
			return;
		}

		LoadLeafySeadragonBladeProjectileTextures();
		_leafySeadragonBladeProjectileResourcesLoaded = true;
	}

	private void EnsureBossKRoyalSeizureLanceTexturesLoaded()
	{
		if (_bossKRoyalSeizureLanceResourcesLoaded)
		{
			return;
		}

		LoadBossKRoyalSeizureLanceTextures();
		_bossKRoyalSeizureLanceResourcesLoaded = true;
	}

	private void EnsureAdventureDamageEffectTexturesLoaded()
	{
		if (_adventureDamageEffectResourcesLoaded)
		{
			return;
		}

		LoadAdventureDamageEffectTextures();
		_adventureDamageEffectResourcesLoaded = true;
	}

	private void EnsureAdventureEnemyDamageEffectTexturesLoaded()
	{
		if (_adventureEnemyDamageEffectResourcesLoaded)
		{
			return;
		}

		LoadAdventureEnemyDamageEffectTextures();
		_adventureEnemyDamageEffectResourcesLoaded = true;
	}

	private void EnsureQuestionCardTransformTexturesLoaded()
	{
		if (_questionCardTransformResourcesLoaded)
		{
			return;
		}

		LoadQuestionCardTransformTextures();
		_questionCardTransformResourcesLoaded = true;
	}

	private void EnsureBombPairDestroyEffectTexturesLoaded()
	{
		if (_bombPairDestroyEffectResourcesLoaded)
		{
			return;
		}

		LoadBombPairDestroyEffectTextures();
		_bombPairDestroyEffectResourcesLoaded = true;
	}

	private void EnsureLensRevealWispTexturesLoaded()
	{
		if (_lensRevealWispResourcesLoaded)
		{
			return;
		}

		LoadLensRevealWispTextures();
		_lensRevealWispResourcesLoaded = true;
	}

	private void EnsureMagnetCountdownFxTexturesLoaded()
	{
		if (_magnetCountdownFxResourcesLoaded)
		{
			return;
		}

		LoadMagnetCountdownFxTextures();
		_magnetCountdownFxResourcesLoaded = true;
	}

	private void EnsureAdventureTraceHuntMarkerTexturesLoaded()
	{
		if (_adventureTraceHuntMarkerResourcesLoaded)
		{
			return;
		}

		LoadAdventureTraceHuntMarkerTextures();
		_adventureTraceHuntMarkerResourcesLoaded = true;
	}

	private static Texture2D[] LoadEnemySpecialAbilityAvatarFxFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			EnemySpecialAbilityAvatarFxFramePattern,
			EnemySpecialAbilityAvatarFxFrameCount,
			"enemy special ability avatar effect",
			preferImageFile: true);
	}

	private static Texture2D[] LoadScoreFillEffectFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			ScoreFillEffectFramePattern,
			ScoreFillEffectFrameCount,
			"score fill effect");
	}

	private static Texture2D[] LoadMantaRayTideShiftCycloneFxFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			MantaRayTideShiftCycloneFxFramePattern,
			MantaRayTideShiftCycloneFxFrameCount,
			"manta ray tide shift cyclone",
			preferImageFile: true);
	}

	private static Texture2D[] LoadDefaultAdventureDamageEffectFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			AdventureDamageEffectFramePattern,
			AdventureDamageEffectFrameCount,
			"adventure damage effect");
	}

	private static Texture2D[] LoadCatAdventureDamageEffectFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			CatAdventureDamageEffectFramePattern,
			AdventureDamageEffectFrameCount,
			"cat adventure damage effect");
	}

	private static Texture2D[] LoadLeafySeadragonBladeFlightFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			LeafySeadragonBladeFlightFramePattern,
			LeafySeadragonBladeFlightFrameCount,
			"leafy seadragon blade flight",
			preferImageFile: true);
	}

	private static Texture2D[] LoadLeafySeadragonBladeImpactFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			LeafySeadragonBladeImpactFramePattern,
			LeafySeadragonBladeImpactFrameCount,
			"leafy seadragon blade impact",
			preferImageFile: true);
	}

	private static Texture2D[] LoadBossKRoyalSeizureLanceFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			BossKRoyalSeizureLanceFramePattern,
			BossKRoyalSeizureLanceFrameCount,
			"boss k royal seizure lance",
			preferImageFile: true);
	}

	private static Texture2D[] LoadQuestionCardTransformFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			QuestionCardTransformFramePattern,
			QuestionCardTransformFrameCount,
			"question card transform");
	}

	private static Texture2D[] LoadSpecialCardTriggerEffectFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			SpecialCardTriggerEffectFramePattern,
			SpecialCardTriggerEffectFrameCount,
			"special card trigger effect");
	}

	private static Texture2D[] LoadSpecialCardTriggerActivationFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			SpecialCardTriggerActivationFramePattern,
			SpecialCardTriggerActivationFrameCount,
			"special card trigger activation",
			preferImageFile: true);
	}

	private static Texture2D[] LoadBombPairDestroyEffectFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			BombPairDestroyEffectFramePattern,
			BombPairDestroyEffectFrameCount,
			"bomb pair destroy effect");
	}

	private static Texture2D[] LoadLensRevealWispFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			LensRevealWispFramePattern,
			LensRevealWispFrameCount,
			"lens reveal wisp");
	}

	private static Texture2D[] LoadMagnetCountdownFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			MagnetCountdownFramePattern,
			MagnetCountdownFrameCount,
			"magnet countdown");
	}

	private static Texture2D[] LoadTableShuffleVortexFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			TableShuffleVortexFramePattern,
			TableShuffleVortexFrameCount,
			"table shuffle vortex",
			preferImageFile: true);
	}

	private static Texture2D[] LoadMagnetAttachmentApplyRingFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			MagnetAttachmentApplyRingFramePattern,
			MagnetAttachmentApplyRingFrameCount,
			"magnet attachment apply ring",
			preferImageFile: true);
	}

	private static Texture2D[] LoadAdventureTraceHuntMarkerFrames()
	{
		return FrameAnimationHelpers.LoadTextureFrames(
			AdventureTraceHuntMarkerFramePattern,
			AdventureTraceHuntMarkerFrameCount,
			"adventure trace hunt marker",
			preferImageFile: true);
	}

	private void LoadCardBackTextures()
	{
		_cardBackTexture = CardBackFrames.Length > 0 ? CardBackFrames[0] : null;
	}

	private void LoadScoreFillEffectTextures()
	{
		_scoreFillEffectFrames.Clear();
		_scoreFillEffectFrames.AddRange(LoadScoreFillEffectFrames());
		_scoreFillEffectResourcesLoaded = true;
	}

	private void LoadEnemySpecialAbilityAvatarFxTextures()
	{
		_enemySpecialAbilityAvatarFxFrames.Clear();
		_enemySpecialAbilityAvatarFxFrames.AddRange(LoadEnemySpecialAbilityAvatarFxFrames());
		_enemySpecialAbilityAvatarFxResourcesLoaded = true;
	}

	private void LoadMantaRayTideShiftCycloneFxTextures()
	{
		_mantaRayTideShiftCycloneFxFrames.Clear();
		_mantaRayTideShiftCycloneFxFrames.AddRange(LoadMantaRayTideShiftCycloneFxFrames());
		_mantaRayTideShiftCycloneFxResourcesLoaded = true;
	}

	private void LoadLeafySeadragonBladeProjectileTextures()
	{
		_leafySeadragonBladeFlightFrames.Clear();
		_leafySeadragonBladeFlightFrames.AddRange(LoadLeafySeadragonBladeFlightFrames());
		_leafySeadragonBladeImpactFrames.Clear();
		_leafySeadragonBladeImpactFrames.AddRange(LoadLeafySeadragonBladeImpactFrames());
		_leafySeadragonBladeProjectileResourcesLoaded = true;
	}

	private void LoadBossKRoyalSeizureLanceTextures()
	{
		_bossKRoyalSeizureLanceFrames.Clear();
		_bossKRoyalSeizureLanceFrames.AddRange(LoadBossKRoyalSeizureLanceFrames());
		_bossKRoyalSeizureLanceResourcesLoaded = true;
	}

	private void LoadAdventureDamageEffectTextures()
	{
		_adventureDamageEffectFrames.Clear();
		_adventureDamageEffectFrames.AddRange(LoadCatAdventureDamageEffectFrames());
		_adventureDamageEffectResourcesLoaded = true;
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
			: LoadDefaultAdventureDamageEffectFrames();
		_adventureEnemyDamageEffectFrames.AddRange(frames);
		CacheDragonPrinceDamageHitFrameSlices(enemyDefinition);
		_adventureEnemyDamageEffectResourcesLoaded = true;
	}

	private void LoadAdventureTraceHuntMarkerTextures()
	{
		_adventureTraceHuntMarkerFrames.Clear();
		_adventureTraceHuntMarkerFrames.AddRange(LoadAdventureTraceHuntMarkerFrames());
		_adventureTraceHuntMarkerResourcesLoaded = true;
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
		_questionCardTransformFrames.AddRange(LoadQuestionCardTransformFrames());
		_questionCardTransformResourcesLoaded = true;
	}

	private void LoadSpecialCardTriggerEffectTextures()
	{
		_specialCardTriggerEffectFrames.Clear();
		_specialCardTriggerEffectFrames.AddRange(LoadSpecialCardTriggerEffectFrames());
		_specialCardTriggerActivationFrames.Clear();
		_specialCardTriggerActivationFrames.AddRange(LoadSpecialCardTriggerActivationFrames());
		_specialCardTriggerEffectResourcesLoaded = true;
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
		_bombPairDestroyEffectFrames.AddRange(LoadBombPairDestroyEffectFrames());
		_bombPairDestroyEffectResourcesLoaded = true;
	}

	private void LoadLensRevealWispTextures()
	{
		_lensRevealWispFrames.Clear();
		_lensRevealWispFrames.AddRange(LoadLensRevealWispFrames());
		_lensRevealWispResourcesLoaded = true;
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
		_magnetCountdownFrameTextures.AddRange(LoadMagnetCountdownFrames());
		_tableShuffleVortexFrames.Clear();
		_tableShuffleVortexFrames.AddRange(LoadTableShuffleVortexFrames());
		_magnetAttachmentApplyRingFrames.Clear();
		_magnetAttachmentApplyRingFrames.AddRange(LoadMagnetAttachmentApplyRingFrames());
		_magnetCountdownFxResourcesLoaded = true;
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
