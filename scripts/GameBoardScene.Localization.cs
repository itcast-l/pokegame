using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private const string GameSettingsConfigPath = "user://settings.cfg";

	private readonly List<(Button Button, string Key)> _localizedGameButtons = new();

	public override void _Notification(int what)
	{
		if (what == NotificationTranslationChanged && IsNodeReady())
		{
			RefreshLocalizedGameText();
		}
	}

	private void ApplySavedLanguagePreference()
	{
		var config = new ConfigFile();
		Error loadResult = config.Load(GameSettingsConfigPath);
		string preference = loadResult == Error.Ok
			? config.GetValue("general", "language", Localization.DefaultLanguagePreference).AsString()
			: Localization.DefaultLanguagePreference;

		TranslationServer.SetLocale(Localization.ResolveLocale(this, preference));
	}

	private string Localize(string key)
	{
		return Tr(key).ToString();
	}

	private string LocalizeFormat(string key, params object[] args)
	{
		return string.Format(Localize(key), args);
	}

	private void TrackLocalizedGameButton(Button button, string key)
	{
		_localizedGameButtons.Add((button, key));
	}

	private string GetPlayerLabel(int playerIndex)
	{
		return LocalizeFormat("GAME_PLAYER", playerIndex + 1);
	}

	private string GetPlayerScoreTooltip(int playerIndex)
	{
		return LocalizeFormat("GAME_PLAYER_SCORE", playerIndex + 1, _playerScores[playerIndex], GetScoreLimit());
	}

	private static bool LooksLikeLocalizationKey(string value)
	{
		return value.StartsWith("ITEM_DESC_", System.StringComparison.Ordinal);
	}

	private string GetItemDescription(string fallbackText = "")
	{
		string key = LooksLikeLocalizationKey(fallbackText)
			? fallbackText
			: string.Empty;
		if (string.IsNullOrEmpty(key))
		{
			return fallbackText;
		}

		string localizedText = Localize(key);
		return localizedText == key ? fallbackText : localizedText;
	}

	private string GetMagnetAttachmentCountdownHint()
	{
		if (!string.IsNullOrEmpty(_pendingItemCardId))
		{
			string description = GetItemDescription(_itemCardCatalog.GetDescriptionKey(_pendingItemCardId));
			if (!string.IsNullOrWhiteSpace(description) && description != _pendingItemCardId)
			{
				return description;
			}
		}

		return Localize("GAME_SELECT_BASE_CARD");
	}

	private void RefreshItemCardDescriptions()
	{
		foreach (string itemCardId in _enabledSceneItemCardIds)
		{
			_itemCardDescriptions[itemCardId] = GetItemDescription(_itemCardCatalog.GetDescriptionKey(itemCardId));
		}
	}

	private void RefreshLocalizedGameText()
	{
		for (int index = _localizedGameButtons.Count - 1; index >= 0; index--)
		{
			(Button button, string key) = _localizedGameButtons[index];
			if (!GodotObject.IsInstanceValid(button))
			{
				_localizedGameButtons.RemoveAt(index);
				continue;
			}

			button.Text = Localize(key);
		}

		if (_winnerLabel != null && GodotObject.IsInstanceValid(_winnerLabel) && _winnerPlayerIndex >= 0)
		{
			_winnerLabel.Text = _showingDefeatResult
				? Localize("GAME_DEFEAT")
				: LocalizeFormat("GAME_WINNER", _winnerPlayerIndex + 1);
		}

		if (_magnetAttachmentCountdownHintLabel != null && GodotObject.IsInstanceValid(_magnetAttachmentCountdownHintLabel))
		{
			_magnetAttachmentCountdownHintLabel.Text = GetMagnetAttachmentCountdownHint();
		}

		RefreshItemCardDescriptions();
		RefreshScoreTooltips();
		RefreshSelectionOverlayText();
		RefreshRenderedCardDescriptions();
	}

	private void RefreshScoreTooltips()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			UpdatePlayerScoreMeter(playerIndex, false);
		}
	}

	private void RefreshSelectionOverlayText()
	{
		if (_selectionOverlay == null || !GodotObject.IsInstanceValid(_selectionOverlay))
		{
			return;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.TargetPlayer)
		{
			ShowTargetPlayerSelection();
		}
		else if (_pendingItemSelectionMode == PendingItemSelectionMode.TargetHandCard)
		{
			ShowTargetHandSelection(_revealedPlayerHandIndex);
		}
		else if (_pendingItemSelectionMode == PendingItemSelectionMode.HandLimitDiscard)
		{
			ShowHandLimitDiscardSelection();
		}
	}

	private void RefreshRenderedCardDescriptions()
	{
		if (_playerHandAreas.Count == MaxPlayers)
		{
			RenderPlayerHands();
		}

		if (_centerArena != null && GodotObject.IsInstanceValid(_centerArena))
		{
			RenderTableCards();
		}
	}
}
