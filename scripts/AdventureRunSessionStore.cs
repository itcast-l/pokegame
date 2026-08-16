using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Deckland.Rules;

public sealed partial class AdventureRunSessionStore : Node
{
	private static readonly HashSet<string> CanonicalCurrentPropertyNames = new(
		StringComparer.Ordinal)
	{
		nameof(AdventureUpgradeRunSnapshot.SchemaVersion),
		nameof(AdventureUpgradeRunSnapshot.CampaignId),
		nameof(AdventureUpgradeRunSnapshot.MainStageIndex),
		nameof(AdventureUpgradeRunSnapshot.EnemyAvatarId),
		nameof(AdventureUpgradeRunSnapshot.ShouldGrantRunStartUpgrade),
		nameof(AdventureUpgradeRunSnapshot.ShouldGrantVictoryUpgrade),
		nameof(AdventureUpgradeRunSnapshot.AcquiredUpgradeIds),
		nameof(AdventureUpgradeRunSnapshot.RetiredUpgradeIds),
		nameof(AdventureUpgradeRunSnapshot.CurrentHealth),
		nameof(AdventureUpgradeRunSnapshot.MaxHealth),
		nameof(AdventureUpgradeRunSnapshot.PermanentCaptureDamageBonus),
		nameof(AdventureUpgradeRunSnapshot.QuestionRewardCount),
		nameof(AdventureUpgradeRunSnapshot.HealingOverflowRemainder),
		nameof(AdventureUpgradeRunSnapshot.HasUsedRevive),
	};

	private AuthorityRuleSession _authoritySession = CreateDetachedAuthoritySession();
	private AuthorityRuleSession? _boundBoardAuthoritySession;
	private AdventureUpgradeManifest? _manifest;
	private long _nextAdventureActionId;
	private GameModePolicy _gameModePolicy = GameModePolicy.AdventureMain;
	private string? _deferredPayload;
	private string? _preservedFailurePayload;
	private string? _lastObservedPayload;
	private string? _lastWarnedInvalidPayload;
	private int _debugPersistFailuresRemaining;
	private int _debugPersistAttemptCount;
	private AdventureRunWriteResult? _debugNextTransactionalWriteResult;

	public bool HasActiveRun => _gameModePolicy.CanReadAdventureRun
		&& _authoritySession.AdventureReadModel.IsActive;
	public bool RequiresRunRecovery => !string.IsNullOrEmpty(_preservedFailurePayload);
	internal GameModePolicy ModePolicy => _gameModePolicy;
	internal AdventureUpgradeRestoreResult LastRestoreResult { get; private set; } =
		AdventureUpgradeRestoreResult.NoSnapshot;

	public override void _Ready()
	{
		Node? pandora = GetNodeOrNull<Node>("/root/Pandora");
		if (pandora != null)
		{
			if (pandora.Call("is_loaded").AsBool())
			{
				_ = AdventureSuitUpgradeCatalog.Load(this);
			}
			else
			{
				Callable dataLoadedCallback = Callable.From(OnPandoraDataLoaded);
				if (!pandora.IsConnected("data_loaded", dataLoadedCallback))
				{
					pandora.Connect("data_loaded", dataLoadedCallback);
				}
			}
		}

	}

	internal void ConfigureForPolicy(GameModePolicy policy)
	{
		_gameModePolicy = policy ?? GameModePolicy.LocalStandard;
		if (!_gameModePolicy.CanReadAdventureRun)
		{
			_deferredPayload = null;
			_preservedFailurePayload = null;
			LastRestoreResult = AdventureUpgradeRestoreResult.NoSnapshot;
		}
	}

	private void OnPandoraDataLoaded()
	{
		_ = AdventureSuitUpgradeCatalog.Load(this);
	}

	internal AdventureRunReadModel GetReadModel()
	{
		return _authoritySession.AdventureReadModel;
	}

	internal bool BindAuthorityRuleSession(AuthorityRuleSession authoritySession)
	{
		ArgumentNullException.ThrowIfNull(authoritySession);
		if (_boundBoardAuthoritySession == authoritySession)
		{
			return true;
		}
		if (_manifest?.IsReady != true
			|| !authoritySession.ConfigureAdventureManifest(_manifest))
		{
			return false;
		}

		AdventureAuthoritySnapshot snapshot = _authoritySession.CaptureAdventureAuthority();
		AdventureUpgradeRestoreResult restoreResult =
			authoritySession.RestoreAdventureAuthority(snapshot);
		if (restoreResult is AdventureUpgradeRestoreResult.InvalidSnapshotPreserved
			or AdventureUpgradeRestoreResult.DeferredManifestUnavailable)
		{
			return false;
		}

		_authoritySession = authoritySession;
		_boundBoardAuthoritySession = authoritySession;
		SynchronizeNextActionId(snapshot);
		return true;
	}

	internal void UnbindAuthorityRuleSession(AuthorityRuleSession authoritySession)
	{
		if (_boundBoardAuthoritySession != authoritySession)
		{
			return;
		}

		AdventureAuthoritySnapshot snapshot = authoritySession.CaptureAdventureAuthority();
		AuthorityRuleSession detached = CreateDetachedAuthoritySession();
		if (_manifest?.IsReady == true)
		{
			_ = detached.ConfigureAdventureManifest(_manifest);
			_ = detached.RestoreAdventureAuthority(snapshot);
		}
		_authoritySession = detached;
		_boundBoardAuthoritySession = null;
		SynchronizeNextActionId(snapshot);
	}


	internal bool TryGetSnapshot(out AdventureUpgradeRunSnapshot snapshot)
	{
		AdventureRunReadModel readModel = _authoritySession.AdventureReadModel;
		if (!_gameModePolicy.CanReadAdventureRun || !readModel.IsActive)
		{
			snapshot = default;
			return false;
		}

		snapshot = _authoritySession.CaptureAdventureAuthority().RunSnapshot!.Value;
		return true;
	}

	internal AdventureUpgradeRestoreResult SetManifest(AdventureUpgradeManifest manifest)
	{
		if (manifest == null || !manifest.IsReady)
		{
			return AdventureUpgradeRestoreResult.DeferredManifestUnavailable;
		}

		_manifest = manifest;
		if (!_authoritySession.ConfigureAdventureManifest(manifest))
		{
			return AdventureUpgradeRestoreResult.DeferredManifestUnavailable;
		}
		if (!_gameModePolicy.CanReadAdventureRun)
		{
			return AdventureUpgradeRestoreResult.NoSnapshot;
		}
		if (string.IsNullOrEmpty(_deferredPayload))
		{
			return LastRestoreResult;
		}

		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		string currentPayload = profile.PendingAdventureRunJson;
		if (!string.Equals(currentPayload, _deferredPayload, StringComparison.Ordinal))
		{
			return ReloadFromActiveProfile();
		}

		return RestorePayload(profile, _deferredPayload);
	}

	private static AuthorityRuleSession CreateDetachedAuthoritySession()
	{
		return new AuthorityRuleSession(
			new DetachedAdventureRandomSource(),
			MatchRuleProfile.Empty);
	}

	private long NextAdventureActionId()
	{
		_nextAdventureActionId = checked(_nextAdventureActionId + 1);
		return _nextAdventureActionId;
	}

	private void SynchronizeNextActionId(AdventureAuthoritySnapshot snapshot)
	{
		foreach (long actionId in snapshot.NormalizedAppliedActionIds)
		{
			_nextAdventureActionId = Math.Max(_nextAdventureActionId, actionId);
		}
	}

	private sealed class DetachedAdventureRandomSource : IRuleRandomSource
	{
		private RuleRandomState _state = new(1, 1);

		public RuleRandomState CaptureState() => _state;

		public void RestoreState(RuleRandomState state)
		{
			_state = state.State == 0 ? new RuleRandomState(1, 1) : state;
		}

		public int NextIntInclusive(int minimum, int maximum)
		{
			if (minimum > maximum)
			{
				throw new ArgumentOutOfRangeException(nameof(minimum));
			}
			return minimum;
		}
	}

}
