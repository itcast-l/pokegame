using Godot;

public partial class GameBoardScene
{
	[Export(PropertyHint.Range, "2,4,1")]
	public int player_count
	{
		get => _playerCount;
		set
		{
			int normalizedPlayerCount = Mathf.Clamp(value, MinPlayers, MaxPlayers);
			if (normalizedPlayerCount == _playerCount)
			{
				return;
			}

			_playerCount = normalizedPlayerCount;
			if (IsInsideTree() && !_networkWaitForInitialize)
			{
				UpdatePlayerVisibility();
				MarkSeatControlIndicatorsDirty();
				MarkSeatControlIndicatorLayoutDirty();
				MarkActivePlayerStatusTooltipForRefresh();
				TrackBoardTask(DealCardsToPlayersWithAdventureGateAsync(playOpeningDealAnimation: true), nameof(DealCardsToPlayersWithAdventureGateAsync));
			}
		}
	}

	[Export]
	public bool item_mode
	{
		get => _itemMode;
		set
		{
			if (value == _itemMode)
			{
				return;
			}

			_itemMode = value;
			if (_itemMode && IsInsideTree())
			{
				EnsureItemModeResourcesLoaded();
			}

			if (IsInsideTree() && !_networkWaitForInitialize)
			{
				LoadItemCardEnabledStates();
				TrackBoardTask(DealCardsToPlayersWithAdventureGateAsync(playOpeningDealAnimation: true), nameof(DealCardsToPlayersWithAdventureGateAsync));
			}
		}
	}

	[Export(PropertyHint.Range, "50,200,50")]
	public int winning_score
	{
		get => _winningScore;
		set
		{
			int normalizedWinningScore = value switch
			{
				50 or 100 or 200 => value,
				_ => DefaultWinningScore,
			};
			if (normalizedWinningScore == _winningScore)
			{
				return;
			}

			_winningScore = normalizedWinningScore;

			if (IsInsideTree() && !_networkWaitForInitialize)
			{
				RefreshScoreTooltips();
				CheckForWinner();
			}
		}
	}

	[Export]
	public bool adventure_mode
	{
		get => _adventureMode;
		set
		{
			if (_adventureMode == value)
			{
				return;
			}

			_adventureMode = value;
			if (_adventureMode && IsInsideTree())
			{
				EnsureAdventureModeResourcesLoaded();
			}
		}
	}

	[Export]
	public bool adventure_tutorial_mode
	{
		get => _adventureTutorialMode;
		set => _adventureTutorialMode = value;
	}

	[Export]
	public string adventure_tutorial_flow
	{
		get => _adventureTutorialFlow;
		set => _adventureTutorialFlow = NormalizeAdventureTutorialFlow(value);
	}

	[Export]
	public bool solo_mode
	{
		get => _soloMode;
		set => _soloMode = value;
	}

	[Export(PropertyHint.Enum, "Low,Medium,High")]
	public int solo_ai_difficulty
	{
		get => _soloAiDifficulty;
		set => _soloAiDifficulty = Mathf.Clamp(value, (int)SoloAiDifficulty.Low, (int)SoloAiDifficulty.High);
	}

	[Export(PropertyHint.Range, "0,3,1")]
	public int local_player_index
	{
		get => _localPlayerIndex;
		set => _localPlayerIndex = Mathf.Clamp(value, 0, MaxPlayers - 1);
	}

	[Export]
	public string network_session_id
	{
		get => _networkSessionId;
		set => _networkSessionId = value?.Trim() ?? string.Empty;
	}

	[Export]
	public string network_match_id
	{
		get => _networkMatchId;
		set => _networkMatchId = value?.Trim() ?? string.Empty;
	}

	[Export]
	public long match_seed
	{
		get => _matchSeed;
		set => _matchSeed = value;
	}

	[Export]
	public string player_names_csv
	{
		get => _playerNamesCsv;
		set => _playerNamesCsv = value ?? string.Empty;
	}

	[Export]
	public string player_avatar_ids_csv
	{
		get => _playerAvatarIdsCsv;
		set => _playerAvatarIdsCsv = value ?? string.Empty;
	}

	[Export]
	public bool network_wait_for_initialize
	{
		get => _networkWaitForInitialize;
		set => _networkWaitForInitialize = value;
	}

	[Export]
	public string adventure_enemy_avatar_id
	{
		get => _adventureEnemyAvatarId;
		set
		{
			string normalized = NormalizeAdventureEnemyAvatarId(value);
			if (_adventureEnemyAvatarId == normalized)
			{
				return;
			}

			_adventureEnemyAvatarId = normalized;
			if (IsInsideTree() && adventure_mode)
			{
				LoadAdventureEnemyDamageEffectTextures();
				LoadDragonPrinceSpecialJackTexture();
			}
		}
	}

	[Export]
	public bool debug_opponent_has_one_card = false;

	[Export]
	public bool debug_main_player_has_one_card = false;

	[Export]
	public string debug_recording_setup_id = string.Empty;

	[Export(PropertyHint.Range, "-1,200,1")]
	public int debug_main_player_starting_score = -1;

	[Export]
	public bool debug_performance_trace = false;

	[Export(PropertyHint.Range, "0,50,0.5")]
	public float debug_performance_trace_min_ms = 4.0f;
}
