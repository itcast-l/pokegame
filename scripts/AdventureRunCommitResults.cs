internal enum AdventureRunWriteResult
{
	Succeeded,
	WriteNotAllowed,
	RecoveryRequired,
	InvalidStage,
	SaveFailed,
}

internal enum AdventureVictoryCommitResult
{
	Succeeded,
	AlreadyCommitted,
	InvalidStage,
	OutOfSequence,
	SaveFailed,
}
