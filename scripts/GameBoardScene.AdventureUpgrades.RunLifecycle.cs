using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private const int FirstMainAdventureStageIndex = 1;

	private void ClearAdventureUpgradeRun()
	{
		SetAdventureTraceHuntMarkedTableCard(null, playIntro: false);
		ClearAdventureTraceHuntMarkerIntro();
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("clear the adventure upgrade run");
			return;
		}

		AdventureRunWriteResult result = store.ClearRun();
		if (result != AdventureRunWriteResult.Succeeded)
		{
			GD.PushWarning($"Failed to clear adventure upgrade run: {result}");
		}
	}

	private async Task<bool> InitializeAdventureUpgradeRunForCurrentBoardAsync()
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return true;
		}

		if (!AdventureCampaignCatalog.TryGetMainStageIndex(adventure_enemy_avatar_id, out int mainStageIndex))
		{
			return false;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			return false;
		}
		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			AdventureSaveGateResult<AdventureRunWriteResult> gate =
				await ExecuteAdventureSaveGateAsync(
					() => store.BeginNewMainRun(mainStageIndex, adventure_enemy_avatar_id),
					result => result == AdventureRunWriteResult.Succeeded,
					result => result == AdventureRunWriteResult.SaveFailed);
			if (!gate.Succeeded)
			{
				GD.PushWarning($"Failed to initialize adventure run for current board: {gate.Result}");
				return false;
			}
			return true;
		}

		if (runState.MainStageIndex == mainStageIndex)
		{
			return true;
		}

		AdventureSaveGateResult<AdventureRunWriteResult> advanceGate =
			await ExecuteAdventureSaveGateAsync(
				() => store.AdvanceToMainStage(mainStageIndex, adventure_enemy_avatar_id),
				result => result == AdventureRunWriteResult.Succeeded,
				result => result == AdventureRunWriteResult.SaveFailed);
		if (!advanceGate.Succeeded)
		{
			GD.PushWarning($"Failed to align adventure run with current board: {advanceGate.Result}");
			return false;
		}
		return true;
	}

	private bool ShouldUseAdventureUpgradeSystem()
	{
		if (!_gameModePolicy.IsAdventureMain)
		{
			return false;
		}

		return AdventureCampaignCatalog.TryGetMainStageIndex(adventure_enemy_avatar_id, out _);
	}

	private bool ShouldDelayWinnerForAdventureVictoryUpgrade(int winnerPlayerIndex)
	{
		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (!ShouldUseAdventureUpgradeSystem()
			|| winnerPlayerIndex != MainPlayerIndex
			|| runState?.ShouldGrantVictoryUpgrade != true)
		{
			return false;
		}

		if (CurrentMatchFlowState.PendingWinnerSeat == winnerPlayerIndex)
		{
			return true;
		}

		return TryUpdateMatchFlowState(state => state with
		{
			PendingWinnerSeat = winnerPlayerIndex,
		});
	}

	private bool ShouldQueueAdventureBigCaptureUpgrade(int playerIndex, int totalCapturedCardCount)
	{
		if (!ShouldUseAdventureUpgradeSystem()
			|| playerIndex != MainPlayerIndex
			|| GetAdventureUpgradeRunState()
				?.ShouldGrantBigCaptureUpgrade(totalCapturedCardCount) != true)
		{
			return false;
		}

		return true;
	}

	private static string GetFirstMainAdventureAvatarId()
	{
		return AdventureCampaignCatalog.GetMainAvatarId(FirstMainAdventureStageIndex);
	}
}
