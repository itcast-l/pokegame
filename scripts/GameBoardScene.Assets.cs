using Godot;

public partial class GameBoardScene
{
	private void LoadCardBackTextures()
	{
		_cardBackTexture = GD.Load<Texture2D>(CardBackPath);
		_cardBackFrames.Clear();
		for (int frameIndex = 1; frameIndex <= CardBackFrameCount; frameIndex++)
		{
			string path = string.Format(CardBackFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing card back animation frame: {path}");
				continue;
			}

			_cardBackFrames.Add(texture);
		}

		if (_cardBackTexture == null && _cardBackFrames.Count > 0)
		{
			_cardBackTexture = _cardBackFrames[0];
		}
	}

	private void LoadScoreFillEffectTextures()
	{
		_scoreFillEffectFrames.Clear();
		for (int frameIndex = 1; frameIndex <= ScoreFillEffectFrameCount; frameIndex++)
		{
			string path = string.Format(ScoreFillEffectFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing score fill effect frame: {path}");
				continue;
			}

			_scoreFillEffectFrames.Add(texture);
		}
	}

	private void LoadQuestionCardTransformTextures()
	{
		_questionCardTransformFrames.Clear();
		for (int frameIndex = 1; frameIndex <= QuestionCardTransformFrameCount; frameIndex++)
		{
			string path = string.Format(QuestionCardTransformFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing question card transform frame: {path}");
				continue;
			}

			_questionCardTransformFrames.Add(texture);
		}
	}

	private void LoadSpecialCardTriggerEffectTextures()
	{
		_specialCardTriggerEffectFrames.Clear();
		for (int frameIndex = 1; frameIndex <= SpecialCardTriggerEffectFrameCount; frameIndex++)
		{
			string path = string.Format(SpecialCardTriggerEffectFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing special card trigger effect frame: {path}");
				continue;
			}

			_specialCardTriggerEffectFrames.Add(texture);
		}
	}

	private void LoadBombPairDestroyEffectTextures()
	{
		_bombPairDestroyEffectFrames.Clear();
		for (int frameIndex = 1; frameIndex <= BombPairDestroyEffectFrameCount; frameIndex++)
		{
			string path = string.Format(BombPairDestroyEffectFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing bomb pair destroy effect frame: {path}");
				continue;
			}

			_bombPairDestroyEffectFrames.Add(texture);
		}
	}

	private void LoadLensRevealWispTextures()
	{
		_lensRevealWispFrames.Clear();
		for (int frameIndex = 1; frameIndex <= LensRevealWispFrameCount; frameIndex++)
		{
			string path = string.Format(LensRevealWispFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing lens reveal wisp frame: {path}");
				continue;
			}

			_lensRevealWispFrames.Add(texture);
		}
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
		for (int frameIndex = 1; frameIndex <= MagnetCountdownFrameCount; frameIndex++)
		{
			string path = string.Format(MagnetCountdownFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing magnet countdown frame: {path}");
				continue;
			}

			_magnetCountdownFrameTextures.Add(texture);
		}
	}

	private void MarkAnimatedCardBack(TextureRect cardBack)
	{
		cardBack.SetMeta(CardBackAnimatedMeta, true);
	}

	private void UpdateAnimatedCardBacks(Node root, Texture2D frame)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is TextureRect { } textureRect && textureRect.HasMeta(CardBackAnimatedMeta))
			{
				textureRect.Texture = frame;
			}

			UpdateAnimatedCardBacks(child, frame);
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
