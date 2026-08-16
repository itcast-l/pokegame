using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed class ReplicaRuleSession
{
	private MatchReadModel _readModel = EmptyReadModel;
	private bool _hasSnapshot;

	public MatchReadModel ReadModel => _readModel;

	public SnapshotApplyResult ApplyAuthoritySnapshot(MatchStateSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		try
		{
			SnapshotValidation.ValidateOrThrow(snapshot, nameof(snapshot));
		}
		catch (ArgumentException exception)
		{
			return Reject(RuleRejectionCode.InvalidConfiguration, exception.Message);
		}

		if (_hasSnapshot && snapshot.LifecycleGeneration < _readModel.LifecycleGeneration)
		{
			return Reject(RuleRejectionCode.StaleLifecycleGeneration, snapshot.LifecycleGeneration.ToString());
		}

		if (_hasSnapshot
			&& snapshot.LifecycleGeneration == _readModel.LifecycleGeneration
			&& snapshot.Revision <= _readModel.Revision)
		{
			return Reject(RuleRejectionCode.StaleRevision, snapshot.Revision.ToString());
		}

		_readModel = CreateReadModel(snapshot);
		_hasSnapshot = true;
		return new SnapshotApplyResult(true, RuleRejection.None, _readModel);
	}

	private SnapshotApplyResult Reject(RuleRejectionCode code, string detail)
	{
		return new SnapshotApplyResult(false, new RuleRejection(code, detail), _readModel);
	}

	private static MatchReadModel CreateReadModel(MatchStateSnapshot snapshot)
	{
		return new MatchReadModel(
			snapshot.Revision,
			snapshot.LifecycleGeneration,
			snapshot.TurnGeneration,
			snapshot.PlayerCount,
			snapshot.WinningScore,
			snapshot.CurrentTurnSeat,
			snapshot.Outcome,
			snapshot.WinnerSeat,
			snapshot.Players,
			snapshot.TableCards,
			snapshot.PendingPresentationBarrierId,
				snapshot.PendingTableOrderRuntimeInstanceIds,
				snapshot.MandatorySelection,
				snapshot.PendingAttachmentRewards,
				snapshot.ExtraActions,
				snapshot.CompletedTurnsSinceQuestionRefresh,
				snapshot.QuestionRefreshAnchorSeat,
				snapshot.MatchFlow,
				snapshot.BasePlayCaptureThreshold);
	}

	private static MatchReadModel EmptyReadModel { get; } = new(
		0,
		0,
		0,
		0,
		0,
		-1,
		MatchOutcome.InProgress,
		-1,
		ImmutableArray<PlayerStateSnapshot>.Empty,
		ImmutableArray<TableCardState>.Empty,
		0);
}
