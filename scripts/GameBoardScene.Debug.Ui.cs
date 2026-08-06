using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	public bool DebugIsAdventureUpgradeSelectionVisible()
	{
		return IsSelectionOverlayVisible(SelectionOverlayKind.AdventureUpgrade)
			&& _adventureUpgradeSelectionCompletionSource != null;
	}

	public bool DebugShowAdventureUpgradeSelectionOverlaySample()
	{
		AdventureUpgradeOverlayOptionModel[] options =
		{
			new("hearts", "common", "温流补给", "首次收取后，额外抽1"),
			new("diamonds", "rare", "潮汐回响", "获得奖励时，有 30% 概率再次获得 1 次。"),
			new("clubs", "legendary", "珊瑚冠冕", "连续收牌时，额外获得 3 分。"),
		};
		return ShowAdventureUpgradeSelectionOverlay(options);
	}

	public bool DebugShowAdventureQuestionRejudgeOverlaySample()
	{
		ItemCard? rewardItem = CreateItemCard(ItemCardCatalog.OvertimeItemCardId)
			?? CreateItemCard(ItemCardCatalog.BombItemCardId);
		if (!rewardItem.HasValue)
		{
			return false;
		}

		return ShowAdventureQuestionRejudgeOverlay(rewardItem.Value, (_, _) => DisposeActiveSelectionOverlay());
	}

	private static AdventureSuitUpgradeDefinition[] CreateDebugAdventureUpgradeAwaitOffer()
	{
		return new[]
		{
			new AdventureSuitUpgradeDefinition(
				"debug_upgrade_await_probe",
				"Debug Upgrade Await Probe",
				"clubs",
				"common",
				string.Empty,
				"Debug upgrade await probe.",
				string.Empty,
				string.Empty,
				true,
				0),
		};
	}

	private static void ResetDebugAwaitProbeResult(Godot.Collections.Dictionary result)
	{
		result.Clear();
		result["completed"] = false;
		result["completion_count"] = 0;
		result["error"] = string.Empty;
	}

	public bool DebugStartAdventureUpgradeSelectionAwaitProbe(Godot.Collections.Dictionary result)
	{
		return StartDebugAdventureUpgradeSelectionAwaitProbe(result, replaceActive: false);
	}

	public bool DebugReplaceAdventureUpgradeSelectionAwaitProbe(Godot.Collections.Dictionary result)
	{
		return StartDebugAdventureUpgradeSelectionAwaitProbe(result, replaceActive: true);
	}

	private bool StartDebugAdventureUpgradeSelectionAwaitProbe(
		Godot.Collections.Dictionary result,
		bool replaceActive)
	{
		if (result == null)
		{
			return false;
		}

		if (replaceActive)
		{
			if (GetActiveSelectionOverlaySession(SelectionOverlayKind.AdventureUpgrade) == null)
			{
				return false;
			}
		}
		else if (_adventureUpgradeSelectionCompletionSource != null
			|| _activeSelectionOverlaySession != null)
		{
			return false;
		}

		ResetDebugAwaitProbeResult(result);
		TrackBoardTask(RunDebugAdventureUpgradeSelectionAwaitProbeAsync(result), nameof(RunDebugAdventureUpgradeSelectionAwaitProbeAsync));
		return true;
	}

	private async Task RunDebugAdventureUpgradeSelectionAwaitProbeAsync(
		Godot.Collections.Dictionary result)
	{
		try
		{
			AdventureUpgradeSelectionResult selection =
				await ShowAdventureUpgradeSelectionAsync(
					AdventureUpgradeTriggerSource.None,
					CreateDebugAdventureUpgradeAwaitOffer());
			result["outcome"] = selection.Outcome.ToString();
			result["selected_index"] = selection.SelectedIndex;
		}
		catch (Exception exception) when (exception is not OperationCanceledException || !IsBoardStopping)
		{
			result["error"] = exception.ToString();
		}
		finally
		{
			result["completion_count"] = 1;
			result["completed"] = true;
		}
	}

	public bool DebugStartAdventureQuestionRejudgeAwaitProbe(Godot.Collections.Dictionary result)
	{
		return StartDebugAdventureQuestionRejudgeAwaitProbe(result, replaceActive: false);
	}

	public bool DebugReplaceAdventureQuestionRejudgeAwaitProbe(Godot.Collections.Dictionary result)
	{
		return StartDebugAdventureQuestionRejudgeAwaitProbe(result, replaceActive: true);
	}

	private bool StartDebugAdventureQuestionRejudgeAwaitProbe(
		Godot.Collections.Dictionary result,
		bool replaceActive)
	{
		if (result == null)
		{
			return false;
		}

		if (replaceActive)
		{
			if (GetActiveSelectionOverlaySession(SelectionOverlayKind.QuestionRejudge) == null)
			{
				return false;
			}
		}
		else if (_activeSelectionOverlaySession != null)
		{
			return false;
		}

		ItemCard? rewardItem = CreateItemCard(ItemCardCatalog.OvertimeItemCardId)
			?? CreateItemCard(ItemCardCatalog.BombItemCardId);
		if (!rewardItem.HasValue)
		{
			return false;
		}

		ResetDebugAwaitProbeResult(result);
		TrackBoardTask(RunDebugAdventureQuestionRejudgeAwaitProbeAsync(rewardItem.Value, result), nameof(RunDebugAdventureQuestionRejudgeAwaitProbeAsync));
		return true;
	}

	private async Task RunDebugAdventureQuestionRejudgeAwaitProbeAsync(
		ItemCard rewardItem,
		Godot.Collections.Dictionary result)
	{
		try
		{
			AdventureQuestionRejudgePromptResult prompt =
				await PromptAdventureQuestionRejudgeOverlayAsync(rewardItem);
			result["choice"] = prompt.Choice.ToString();
			result["rect_is_empty"] = prompt.PreviewGlobalRect == new Rect2();
			result["overlay_is_null"] = prompt.Overlay == null;
		}
		catch (Exception exception) when (exception is not OperationCanceledException || !IsBoardStopping)
		{
			result["error"] = exception.ToString();
		}
		finally
		{
			result["completion_count"] = 1;
			result["completed"] = true;
		}
	}

	public bool DebugDisposeActiveSelectionOverlayForAwaitProbe()
	{
		if (_activeSelectionOverlaySession == null)
		{
			return false;
		}

		DisposeActiveSelectionOverlay();
		return true;
	}

	public bool DebugCancelActiveSelectionOverlayForAwaitProbe()
	{
		if (_activeSelectionOverlaySession == null)
		{
			return false;
		}

		DismissActiveSelectionOverlay(invokeCancel: true);
		return true;
	}

	public bool DebugRestartMatchForSelectionAwaitProbe()
	{
		if (IsNetworkMultiplayerMode() || _activeSelectionOverlaySession == null)
		{
			return false;
		}

		if (!DebugPrepareAdventureBattleForDeal())
		{
			return false;
		}
		DealCardsToPlayers(playOpeningDealAnimation: false);
		return true;
	}

	public bool DebugSelectAdventureQuestionRejudgeOption(int optionIndex)
	{
		return TrySelectActiveSelectionOverlayOption(
			SelectionOverlayKind.QuestionRejudge,
			optionIndex);
	}

	public int DebugGetAdventureUpgradeOptionCount()
	{
		SelectionOverlaySession? session = GetActiveSelectionOverlaySession(SelectionOverlayKind.AdventureUpgrade);
		return _adventureUpgradeSelectionCompletionSource != null && session != null
			? session.OptionCount
			: 0;
	}

	public bool DebugSelectAdventureUpgradeOption(int optionIndex)
	{
		if (_adventureUpgradeSelectionCompletionSource == null || optionIndex < 0)
		{
			return false;
		}

		return TrySelectActiveSelectionOverlayOption(SelectionOverlayKind.AdventureUpgrade, optionIndex);
	}

	public bool DebugSetAdventureUpgradeHoverState(int optionIndex, bool hovered)
	{
		SelectionOverlaySession? session = GetActiveSelectionOverlaySession(SelectionOverlayKind.AdventureUpgrade);
		if (session == null
			|| optionIndex < 0
			|| optionIndex >= session.OptionCount)
		{
			return false;
		}

		Button button = session.Root.GetNodeOrNull<Button>($"Margin/Layout/CardsRow/Option{optionIndex + 1}Button");
		if (button == null || !button.Visible)
		{
			return false;
		}

		SetAdventureUpgradeOverlayPointerHover(button, hovered);
		return true;
	}

	public int DebugGetAdventureAcquiredUpgradeCount()
	{
		return GetAdventureUpgradeRunState()?.AcquiredUpgradeIds.Length ?? 0;
	}

	public Godot.Collections.Dictionary DebugGetAdventureRunStateSnapshot()
	{
		var result = new Godot.Collections.Dictionary
		{
			["active"] = false,
			["main_stage_index"] = 0,
			["enemy_avatar_id"] = string.Empty,
			["current_health"] = 0,
			["max_health"] = 0,
			["has_used_revive"] = false,
			["acquired_upgrade_count"] = 0,
			["defeat_resolution"] = _settledAdventureDefeatResolution?.ToString() ?? string.Empty,
		};

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return result;
		}

		result["active"] = true;
		result["main_stage_index"] = runState.MainStageIndex;
		result["enemy_avatar_id"] = runState.EnemyAvatarId;
		result["current_health"] = runState.CurrentHealth;
		result["max_health"] = runState.MaxHealth;
		result["has_used_revive"] = runState.HasUsedRevive;
		result["acquired_upgrade_count"] = runState.AcquiredUpgradeIds.Length;
		return result;
	}

	public bool DebugIsWinnerBannerVisible()
	{
		return _winnerBannerRoot != null
			&& GodotObject.IsInstanceValid(_winnerBannerRoot)
			&& _winnerBannerRoot.Visible;
	}

	public string DebugGetWinnerPrimaryButtonText()
	{
		return _winnerReplayButton != null && GodotObject.IsInstanceValid(_winnerReplayButton)
			? _winnerReplayButton.Text
			: string.Empty;
	}

	public bool DebugPressWinnerPrimaryButton()
	{
		if (!DebugIsWinnerBannerVisible()
			|| _winnerReplayButton == null
			|| !GodotObject.IsInstanceValid(_winnerReplayButton)
			|| _winnerReplayButton.Disabled)
		{
			return false;
		}

		PressWinnerPrimaryButton();
		return true;
	}

	public bool DebugPressWinnerReturnButton()
	{
		if (!DebugIsWinnerBannerVisible()
			|| _winnerBannerRoot == null
			|| !GodotObject.IsInstanceValid(_winnerBannerRoot))
		{
			return false;
		}

		PressWinnerReturnButton();
		return true;
	}

	public int DebugGetPendingAdventureUpgradeTriggerCount()
	{
		return PendingAdventureUpgradeTriggerCount;
	}

	public int DebugGetPendingAdventureWinnerPlayerIndex()
	{
		return CurrentMatchFlowState.PendingWinnerSeat;
	}

	public bool DebugIsAdventureUpgradeResolverBusy()
	{
		return _isResolvingAdventureUpgradeSelection;
	}

public int DebugGetPendingItemAttachmentRewardCount()
{
	return GetPendingAttachmentRewards().Length;
}

public int DebugGetNetworkAuthoritativeVisualSnapshotId()
{
	return _networkAuthoritativeVisualSnapshotId;
}


}
