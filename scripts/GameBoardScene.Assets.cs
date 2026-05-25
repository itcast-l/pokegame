using Godot;

public partial class GameBoardScene
{
	private void LoadCardBackTextures()
	{
		_cardBackTexture = CardBackFrames.Length > 0 ? CardBackFrames[0] : GD.Load<Texture2D>(CardBackPath);
	}

	private void LoadScoreFillEffectTextures()
	{
		_scoreFillEffectFrames.Clear();
		_scoreFillEffectFrames.AddRange(ScoreFillEffectFrames);
	}

	private void LoadAdventureDamageEffectTextures()
	{
		_adventureDamageEffectFrames.Clear();
		_adventureDamageEffectFrames.AddRange(CatAdventureDamageEffectFrames);
	}

	private void LoadAdventureEnemyDamageEffectTextures()
	{
		_adventureEnemyDamageEffectFrames.Clear();
		Texture2D[] frames = adventure_enemy_avatar_id switch
		{
			FishAdventureEnemyAvatarId => FishAdventureDamageEffectFrames,
			LanternfishAdventureEnemyAvatarId => LanternfishAdventureDamageEffectFrames,
			_ => DefaultAdventureDamageEffectFrames,
		};
		_adventureEnemyDamageEffectFrames.AddRange(frames);
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
		_magnetCardOverlayTexture = GD.Load<Texture2D>(MagnetCardOverlayPath);
		if (_magnetCardOverlayTexture == null)
		{
			GD.PushWarning($"Missing magnet card overlay texture: {MagnetCardOverlayPath}");
		}

		_doubleCardOverlayTexture = GD.Load<Texture2D>(DoubleCardOverlayPath);
		if (_doubleCardOverlayTexture == null)
		{
			GD.PushWarning($"Missing double card overlay texture: {DoubleCardOverlayPath}");
		}

		_overtimeCardOverlayTexture = GD.Load<Texture2D>(OvertimeCardOverlayPath);
		if (_overtimeCardOverlayTexture == null)
		{
			GD.PushWarning($"Missing overtime card overlay texture: {OvertimeCardOverlayPath}");
		}
	}

	private void LoadMagnetCountdownFxTextures()
	{
		_magnetCountdownFrameTextures.Clear();
		_magnetCountdownFrameTextures.AddRange(MagnetCountdownFrames);
	}

	private void MarkAnimatedCardBack(TextureRect cardBack)
	{
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

		return itemCardId switch
		{
			JackItemCardId or QueenItemCardId or KingItemCardId => false,
			_ => _itemCardCatalog.IsEnabled(itemCardId),
		};
	}

	private static bool IsExcludedDeckRank(string rank)
	{
		if (ExcludedDeckRanks.Contains(rank))
		{
			return true;
		}

		int suffixStartIndex = rank.LastIndexOf('_') + 1;
		return suffixStartIndex > 0
			&& suffixStartIndex < rank.Length
			&& ExcludedDeckRanks.Contains(rank[suffixStartIndex..]);
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
			_itemCardDescriptions[itemCardId] = GetItemDescription(itemCardId, _itemCardCatalog.GetDescriptionKey(itemCardId));
		}
	}
}
