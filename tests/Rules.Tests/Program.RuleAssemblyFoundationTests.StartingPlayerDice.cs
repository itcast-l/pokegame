using Deckland.Rules;

internal static partial class Program
{
	private static void StartingPlayerDiceFollowsClockwiseSeatOrder()
	{
		AssertStartingPlayerDiceMapping(
			playerCount: 3,
			referenceSeat: 0,
			expectedSeats: new[] { 0, 2, 1, 0, 2, 1 });
		AssertStartingPlayerDiceMapping(
			playerCount: 4,
			referenceSeat: 0,
			expectedSeats: new[] { 0, 2, 1, 3, 0, 2 });
		AssertStartingPlayerDiceMapping(
			playerCount: 3,
			referenceSeat: 2,
			expectedSeats: new[] { 2, 1, 0, 2, 1, 0 });
		AssertStartingPlayerDiceMapping(
			playerCount: 4,
			referenceSeat: 1,
			expectedSeats: new[] { 1, 3, 0, 2, 1, 3 });

		False(StartingPlayerDiceRules.TryResolveStartingTurnSeat(2, 0, 1, out _));
		False(StartingPlayerDiceRules.TryResolveStartingTurnSeat(3, -1, 1, out _));
		False(StartingPlayerDiceRules.TryResolveStartingTurnSeat(3, 3, 1, out _));
		False(StartingPlayerDiceRules.TryResolveStartingTurnSeat(3, 0, 0, out _));
		False(StartingPlayerDiceRules.TryResolveStartingTurnSeat(4, 0, 7, out _));
	}

	private static void MatchInitializationValidatesAndAppliesStartingTurnSeat()
	{
		var rejectedSession = new AuthorityRuleSession(
			new TestRuleRandomSource(1201),
			MatchRuleProfile.Empty);
		RuleCommandResult rejectedInitialize = rejectedSession.Submit(
			CreateInitializeCommand(1201) with
			{
				PlayerCount = 3,
				StartingTurnSeat = 3,
			});
		False(rejectedInitialize.Accepted);
		Equal(RuleRejectionCode.InvalidConfiguration, rejectedInitialize.Rejection.Code);
		False(rejectedSession.ReadModel.IsInitialized);

		var session = new AuthorityRuleSession(
			new TestRuleRandomSource(1202),
			MatchRuleProfile.Empty);
		InitializeMatchCommand initialize = CreateInitializeCommand(1202) with
		{
			PlayerCount = 3,
			StartingTurnSeat = 2,
		};
		RuleCommandResult initialized = session.Submit(initialize);
		True(initialized.Accepted);
		Equal(2, initialized.ReadModel.CurrentTurnSeat);

		MatchReadModel beforeRejectedRestart = initialized.ReadModel;
		RuleCommandResult rejectedRestart = session.Submit(new RestartMatchCommand(
			1203,
			beforeRejectedRestart.Revision,
			2,
			beforeRejectedRestart.TurnGeneration,
			3,
			100,
			3,
			true,
			initialize.Deck,
			StartingTurnSeat: 3));
		False(rejectedRestart.Accepted);
		Equal(RuleRejectionCode.InvalidConfiguration, rejectedRestart.Rejection.Code);
		AssertReadModelsEquivalent(beforeRejectedRestart, rejectedRestart.ReadModel);

		RuleCommandResult restarted = session.Submit(new RestartMatchCommand(
			1204,
			beforeRejectedRestart.Revision,
			2,
			beforeRejectedRestart.TurnGeneration,
			3,
			100,
			3,
			true,
			initialize.Deck,
			StartingTurnSeat: 1));
		True(restarted.Accepted);
		Equal(1, restarted.ReadModel.CurrentTurnSeat);
	}

	private static void AssertStartingPlayerDiceMapping(
		int playerCount,
		int referenceSeat,
		IReadOnlyList<int> expectedSeats)
	{
		Equal(6, expectedSeats.Count);
		for (int diceResult = StartingPlayerDiceRules.MinDiceResult;
			diceResult <= StartingPlayerDiceRules.MaxDiceResult;
			diceResult++)
		{
			True(StartingPlayerDiceRules.TryResolveStartingTurnSeat(
				playerCount,
				referenceSeat,
				diceResult,
				out int actualSeat));
			Equal(expectedSeats[diceResult - 1], actualSeat);
		}
	}
}
