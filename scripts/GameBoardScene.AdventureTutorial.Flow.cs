using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private void StartAdventureTutorial()
	{
		if (!IsAdventureTutorialEnabled())
		{
			return;
		}

		EnsureAdventureTutorialOverlay();
		if (IsBombItemTutorialFlow())
		{
			_adventureTutorialPhase = AdventureTutorialPhase.BombIntro;
			ShowAdventureTutorialCurrentCallout();
			return;
		}

		_adventureTutorialPhase = AdventureTutorialPhase.BasicIntro;
		_adventureTutorialTourIndex = 0;
		ShowAdventureTutorialCurrentCallout();
	}

	private void ShowAdventureTutorialCurrentCallout()
	{
		if (!IsAdventureTutorialEnabled())
		{
			return;
		}

		EnsureAdventureTutorialOverlay();
		_canAdvanceTutorialCallout = false;
		_adventureTutorialCalloutGeneration++;
		RefreshAdventureTutorialOverlayText();
		RefreshAdventureTutorialOverlayLayout();

		if (_adventureTutorialContinueButton != null && GodotObject.IsInstanceValid(_adventureTutorialContinueButton))
		{
			_adventureTutorialContinueButton.Disabled = true;
			_adventureTutorialContinueButton.FocusMode = Control.FocusModeEnum.None;
		}

		if (IsAdventureTutorialContinueCalloutActive())
		{
			TrackBoardTask(EnableAdventureTutorialContinueAfterDelayAsync(_adventureTutorialCalloutGeneration), nameof(EnableAdventureTutorialContinueAfterDelayAsync));
		}
	}

	private async Task EnableAdventureTutorialContinueAfterDelayAsync(int generation)
	{
		await WaitForBoardDelayAsync(AdventureTutorialCalloutReadSeconds);
		if (generation != _adventureTutorialCalloutGeneration
			|| !IsAdventureTutorialContinueCalloutActive()
			|| _adventureTutorialContinueButton == null
			|| !GodotObject.IsInstanceValid(_adventureTutorialContinueButton))
		{
			return;
		}

		_canAdvanceTutorialCallout = true;
		_adventureTutorialContinueButton.Disabled = false;
		_adventureTutorialContinueButton.FocusMode = Control.FocusModeEnum.None;
	}

	private void PressAdventureTutorialContinue()
	{
		if (!IsAdventureTutorialContinueCalloutActive())
		{
			return;
		}

		if (!_canAdvanceTutorialCallout)
		{
			PulseAdventureTutorialCallout();
			return;
		}

		_canAdvanceTutorialCallout = false;
		if (_adventureTutorialContinueButton != null && GodotObject.IsInstanceValid(_adventureTutorialContinueButton))
		{
			_adventureTutorialContinueButton.Disabled = true;
		}

		if (IsAdventureTutorialBombIntroActive())
		{
			BeginAdventureTutorialStep(AdventureTutorialPhase.BombCollectReward);
			return;
		}

		if (IsAdventureTutorialBasicIntroActive())
		{
			_adventureTutorialPhase = AdventureTutorialPhase.Tour;
			_adventureTutorialTourIndex = 0;
			ShowAdventureTutorialCurrentCallout();
			return;
		}

		if (_adventureTutorialTourIndex < AdventureTutorialTourTargets.Length - 1)
		{
			_adventureTutorialTourIndex++;
			ShowAdventureTutorialCurrentCallout();
			return;
		}

		BeginAdventureTutorialCardStep(AdventureTutorialPhase.MatchSeven);
	}

	private void BeginAdventureTutorialCardStep(AdventureTutorialPhase phase)
	{
		BeginAdventureTutorialStep(phase);
	}

	private void BeginAdventureTutorialStep(AdventureTutorialPhase phase)
	{
		_adventureTutorialPhase = phase;
		_canAdvanceTutorialCallout = false;
		_adventureTutorialCalloutGeneration++;
		RefreshAdventureTutorialOverlayText();
		RefreshAdventureTutorialOverlayLayout();
		InvalidateRenderCaches();
		RenderPlayerHands();
		RenderTableCards();
	}

	private bool IsAdventureTutorialBlockingMainPlayerAction()
	{
		return IsAdventureTutorialContinueCalloutActive() || IsAdventureTutorialEnemyStepActive();
	}

	private bool CanPlayAdventureTutorialHandCard(int handIndex)
	{
		if (!IsAdventureTutorialEnabled())
		{
			return true;
		}

		if (!IsAdventureTutorialCardStepActive())
		{
			return false;
		}

		return TryGetAdventureTutorialAllowedHandIndex(out int allowedHandIndex) && handIndex == allowedHandIndex;
	}

	private bool IsAdventureTutorialHandCardBlocked(int playerIndex, int handIndex)
	{
		if (!IsAdventureTutorialEnabled() || playerIndex != MainPlayerIndex)
		{
			return false;
		}

		if (IsAdventureTutorialCardStepActive())
		{
			return !TryGetAdventureTutorialAllowedHandIndex(out int allowedHandIndex) || handIndex != allowedHandIndex;
		}

		return IsAdventureTutorialItemStepActive();
	}

	private bool IsAdventureTutorialHandCardHighlighted(int playerIndex, int handIndex)
	{
		return IsAdventureTutorialCardStepActive()
			&& playerIndex == MainPlayerIndex
			&& TryGetAdventureTutorialAllowedHandIndex(out int allowedHandIndex)
			&& handIndex == allowedHandIndex;
	}

	private bool TryGetAdventureTutorialAllowedHandIndex(out int handIndex)
	{
		handIndex = -1;
		if (!IsAdventureTutorialCardStepActive() || _playerHands.Count <= MainPlayerIndex)
		{
			return false;
		}

		if (!TryGetAdventureTutorialAllowedHandCard(out string suit, out string rank))
		{
			return false;
		}

		List<PlayingCard> hand = _playerHands[MainPlayerIndex];
		for (int index = 0; index < hand.Count; index++)
		{
			if (IsAdventureTutorialCard(hand[index], suit, rank))
			{
				handIndex = index;
				return true;
			}
		}

		return false;
	}

	private bool TryGetAdventureTutorialAllowedHandCard(out string suit, out string rank)
	{
		suit = string.Empty;
		rank = string.Empty;
		switch (_adventureTutorialPhase)
		{
			case AdventureTutorialPhase.MatchSeven:
				suit = "Hearts";
				rank = "7";
				return true;
			case AdventureTutorialPhase.MatchTwo:
				suit = "Hearts";
				rank = "2";
				return true;
			case AdventureTutorialPhase.BombCollectReward:
				suit = "Hearts";
				rank = "4";
				return true;
			default:
				return false;
		}
	}

	private bool CanPlayAdventureTutorialItemCard(int itemHandIndex)
	{
		if (!IsAdventureTutorialEnabled())
		{
			return true;
		}

		if (!IsAdventureTutorialItemStepActive())
		{
			return false;
		}

		return TryGetAdventureTutorialAllowedItemIndex(out int allowedItemHandIndex)
			&& itemHandIndex == allowedItemHandIndex;
	}

	private bool IsAdventureTutorialItemCardBlocked(int playerIndex, int itemHandIndex)
	{
		return IsAdventureTutorialItemStepActive()
			&& playerIndex == MainPlayerIndex
			&& (!TryGetAdventureTutorialAllowedItemIndex(out int allowedItemHandIndex)
				|| itemHandIndex != allowedItemHandIndex);
	}

	private bool IsAdventureTutorialItemCardHighlighted(int playerIndex, int itemHandIndex)
	{
		return IsAdventureTutorialItemStepActive()
			&& playerIndex == MainPlayerIndex
			&& TryGetAdventureTutorialAllowedItemIndex(out int allowedItemHandIndex)
			&& itemHandIndex == allowedItemHandIndex;
	}

	private bool TryGetAdventureTutorialAllowedItemIndex(out int itemHandIndex)
	{
		itemHandIndex = -1;
		if (!IsAdventureTutorialItemStepActive() || _playerItemHands.Count <= MainPlayerIndex)
		{
			return false;
		}

		List<ItemCard> itemHand = _playerItemHands[MainPlayerIndex];
		for (int index = 0; index < itemHand.Count; index++)
		{
			if (string.Equals(itemHand[index].Id, BombItemCardId, StringComparison.Ordinal))
			{
				itemHandIndex = index;
				return true;
			}
		}

		return false;
	}

	private bool TryGetAdventureTutorialAllowedItemDisplayIndex(out int displayIndex)
	{
		displayIndex = -1;
		if (!TryGetAdventureTutorialAllowedItemIndex(out int itemHandIndex)
			|| _playerHands.Count <= MainPlayerIndex)
		{
			return false;
		}

		displayIndex = _playerHands[MainPlayerIndex].Count + itemHandIndex;
		return true;
	}

	private void TryRejectAdventureTutorialCard(InputEvent inputEvent)
	{
		ClearMainPlayerHandSelectionInputOnMouseClick(inputEvent);
		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
		PulseAdventureTutorialCallout();
	}

	private bool HandleAdventureTutorialMainPlayerCardResult(PlayCardResult result)
	{
		if (!IsAdventureTutorialCardStepActive() || !result.Played)
		{
			return false;
		}

		if (_adventureTutorialPhase == AdventureTutorialPhase.MatchSeven)
		{
			BeginAdventureTutorialStep(AdventureTutorialPhase.MatchTwo);
			return false;
		}

		if (_adventureTutorialPhase == AdventureTutorialPhase.BombCollectReward)
		{
			BeginAdventureTutorialStep(AdventureTutorialPhase.BombPlay);
			return false;
		}

		_adventureTutorialPhase = AdventureTutorialPhase.Complete;
		ResetAdventureTutorialState();
		DeclareWinner(MainPlayerIndex);
		return true;
	}

	private bool HandleAdventureTutorialMainPlayerItemResult(PlayCardResult result)
	{
		if (!IsAdventureTutorialItemStepActive() || !result.Played)
		{
			return false;
		}

		if (_adventureTutorialPhase == AdventureTutorialPhase.BombPlay)
		{
			BeginAdventureTutorialStep(AdventureTutorialPhase.BombEnemyTrigger);
		}

		return false;
	}

	private bool HandleAdventureTutorialOpponentResult(int playerIndex, PlayCardResult result)
	{
		if (!IsBombItemTutorialFlow()
			|| _adventureTutorialPhase != AdventureTutorialPhase.BombEnemyTrigger
			|| playerIndex == MainPlayerIndex
			|| !result.Played
			|| !result.Captured)
		{
			return false;
		}

		_adventureTutorialPhase = AdventureTutorialPhase.Complete;
		ResetAdventureTutorialState();
		if (!IsGameOver())
		{
			DeclareWinner(MainPlayerIndex);
		}

		return true;
	}

	private bool ShouldForceBombQuestionReward()
	{
		return IsBombItemTutorialFlow()
			&& _adventureTutorialPhase == AdventureTutorialPhase.BombCollectReward;
	}

	private void AddAdventureTutorialHandRenderSignature(ref HashCode signature, int playerIndex)
	{
		signature.Add(_adventureTutorialFlow);
		signature.Add((int)_adventureTutorialPhase);
		signature.Add(_adventureTutorialTourIndex);
		signature.Add(playerIndex == MainPlayerIndex && TryGetAdventureTutorialAllowedHandIndex(out int handIndex) ? handIndex : -1);
		signature.Add(playerIndex == MainPlayerIndex && TryGetAdventureTutorialAllowedItemIndex(out int itemIndex) ? itemIndex : -1);
	}

	private void AddAdventureTutorialTableRenderSignature(ref HashCode signature)
	{
		signature.Add(_adventureTutorialFlow);
		signature.Add((int)_adventureTutorialPhase);
	}

	private void AddAdventureTutorialTableHighlights(Rect2 tableLayoutRect)
	{
		if (!IsBasicCaptureTutorialCardStepActive())
		{
			return;
		}

		StyleBoxFlat style = CreateAdventureTutorialTableHighlightStyle();
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (!ShouldHighlightAdventureTutorialTableCard(_tableCards[tableCardIndex]))
			{
				continue;
			}

			Vector2 position = GetTableCardPosition(tableCardIndex, tableLayoutRect) - new Vector2(6.0f, 7.0f);
			var highlight = new Panel
			{
				Name = AdventureTutorialTableHighlightNodeName,
				MouseFilter = MouseFilterEnum.Ignore,
				Position = position,
				Size = CardVisualSize + new Vector2(12.0f, 14.0f),
				ZIndex = tableCardIndex * 2 + 20,
			};
			highlight.AddThemeStyleboxOverride("panel", style);
			_centerArena.AddChild(highlight);
		}
	}

	private bool ShouldHighlightAdventureTutorialTableCard(TableCard tableCard)
	{
		if (!tableCard.Card.HasValue)
		{
			return false;
		}

		PlayingCard card = tableCard.Card.Value;
		if (_adventureTutorialPhase == AdventureTutorialPhase.MatchSeven)
		{
			return IsAdventureTutorialCard(card, "Spades", "7")
				|| IsAdventureTutorialCard(card, "Clubs", "A")
				|| IsAdventureTutorialCard(card, "Diamonds", "4");
		}

		if (_adventureTutorialPhase == AdventureTutorialPhase.MatchTwo)
		{
			return IsAdventureTutorialCard(card, "Clubs", "2")
				|| IsAdventureTutorialCard(card, "Diamonds", "3");
		}

		return false;
	}

	private static bool IsAdventureTutorialCard(PlayingCard card, string suit, string rank)
	{
		return string.Equals(card.Suit, suit, StringComparison.Ordinal)
			&& string.Equals(card.Rank, rank, StringComparison.Ordinal);
	}

	private static bool IsAdventureTutorialTableHighlight(Node child)
	{
		return child is Panel && child.Name.ToString() == AdventureTutorialTableHighlightNodeName;
	}
}
