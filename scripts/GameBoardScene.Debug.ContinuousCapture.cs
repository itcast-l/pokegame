using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	public bool DebugSetBasePlayCaptureThreshold(int threshold)
	{
		if (!ContinuousCaptureRules.IsValidThreshold(threshold)
			|| _authorityRuleSession == null
			|| !TryCaptureAuthorityRuleSnapshot(out MatchStateSnapshot snapshot, out _))
		{
			return false;
		}

		if (!TryRestoreAuthorityRuleSnapshot(
			snapshot with { BasePlayCaptureThreshold = threshold },
			out _))
		{
			return false;
		}

		MarkActivePlayerStatusTooltipForRefresh();
		return true;
	}

	public int DebugGetBasePlayCaptureThreshold() => GetBasePlayCaptureThreshold();

	public void DebugShowCaptureThresholdFeedback(
		int playerIndex,
		bool bigTide,
		int count)
	{
		ShowCaptureThresholdFeedback(playerIndex, bigTide, count);
	}

	public bool DebugShowPlayerStatusTooltipForTest(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= player_count)
		{
			return false;
		}

		ShowPlayerStatusTooltip(playerIndex);
		return true;
	}

	public Godot.Collections.Dictionary DebugGetCaptureThresholdUiState(
		int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= MaxPlayers)
		{
			return new Godot.Collections.Dictionary();
		}

		Panel? feedback = _captureThresholdFeedbackPanels[playerIndex];
		Label? feedbackLabel = _captureThresholdFeedbackLabels[playerIndex];
		Rect2 avatarRect = _playerAvatarAreas[playerIndex]?.GetGlobalRect() ?? default;
		Rect2 scoreMeterRect = _playerScoreMeters[playerIndex]?.GetGlobalRect() ?? default;
		Rect2 handRect = playerIndex < _playerHandAreas.Count
			? _playerHandAreas[playerIndex].GetGlobalRect()
			: default;
		bool tooltipMatchesPlayer = _activeStatusTooltipPlayerIndex == playerIndex
			&& _playerStatusTooltipPanel?.Visible == true;
		return new Godot.Collections.Dictionary
		{
			["is_local_player"] = playerIndex == GetLocalHandPlayerIndex(),
			["local_player_index"] = GetLocalHandPlayerIndex(),
			["view_slot_index"] = GetPlayerViewSlotIndex(playerIndex),
			["avatar_rect"] = avatarRect,
			["score_meter_rect"] = scoreMeterRect,
			["hand_rect"] = handRect,
			["feedback_visible"] = feedback?.Visible ?? false,
			["feedback_text"] = feedbackLabel?.Text ?? string.Empty,
			["feedback_rect"] = feedback?.GetGlobalRect() ?? default,
			["tooltip_visible"] = tooltipMatchesPlayer,
			["tooltip_rect"] = tooltipMatchesPlayer
				? _playerStatusTooltipPanel?.GetGlobalRect() ?? default
				: default,
			["tooltip_capture_threshold_visible"] = tooltipMatchesPlayer
				&& _playerStatusTooltipCaptureThresholdLabel?.Visible == true,
			["tooltip_capture_threshold_text"] = tooltipMatchesPlayer
				? _playerStatusTooltipCaptureThresholdLabel?.Text ?? string.Empty
				: string.Empty,
		};
	}
}
