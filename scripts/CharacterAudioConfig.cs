using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class CharacterAudioConfig : Resource
{
	[Export] public CharacterAudioConfig? Fallback { get; set; }

	[ExportGroup("Actions")]
	[Export] public AudioStream? Score { get; set; }
	[Export] public AudioStream? Attack { get; set; }
	[Export] public AudioStream? Hit { get; set; }
	[Export] public AudioStream? Death { get; set; }
	[Export] public AudioStream? Victory { get; set; }
	[Export] public AudioStream? Defeat { get; set; }

	[ExportGroup("Adventure Attack")]
	[Export] public AudioStream? AttackFlight { get; set; }
	[Export] public AudioStream? AttackImpact { get; set; }
	[Export] public AudioStream? FishBeam { get; set; }
	[Export] public AudioStream? CrabPincer { get; set; }
	[Export] public AudioStream? JellyfishTentacle { get; set; }
	[Export] public AudioStream? MantaRayTideShift { get; set; }
	[Export] public AudioStream? SeaSlugMarker { get; set; }
	[Export] public AudioStream? GardenEelAmbush { get; set; }
	[Export] public AudioStream? LeafySeadragonBlade { get; set; }
	[Export] public AudioStream? DragonPrinceHit { get; set; }
	[Export] public AudioStream? DragonQueenJaw { get; set; }
	[Export] public AudioStream? BossKRoyalSeizure { get; set; }

	[ExportGroup("Adventure Ability")]
	[Export] public AudioStream? TideGuard { get; set; }
	[Export] public AudioStream? JellyfishBubbleCover { get; set; }
	[Export] public AudioStream? ColorCurrent { get; set; }
	[Export] public AudioStream? NautilusSpiralShell { get; set; }
	[Export] public AudioStream? NarwhalTuskBreak { get; set; }
	[Export] public AudioStream? DragonPrinceAbility { get; set; }
	[Export] public AudioStream? DragonQueenAbility { get; set; }
	[Export] public AudioStream? DragonKingAbility { get; set; }

	public AudioStream? GetActionStream(CharacterAvatar.AvatarAction action)
	{
		return action switch
		{
			CharacterAvatar.AvatarAction.Score => WithFallback(Score, config => config.GetActionStream(action)),
			CharacterAvatar.AvatarAction.Attack => WithFallback(Attack, config => config.GetActionStream(action)),
			CharacterAvatar.AvatarAction.Hit => WithFallback(Hit, config => config.GetActionStream(action)),
			CharacterAvatar.AvatarAction.Victory => WithFallback(Victory, config => config.GetActionStream(action)),
			CharacterAvatar.AvatarAction.Defeat => WithFallback(Defeat ?? Death, config => config.GetActionStream(action)),
			_ => null,
		};
	}

	public AudioStream? GetAttackFlightStream()
	{
		return WithFallback(AttackFlight, config => config.GetAttackFlightStream());
	}

	public AudioStream? GetAttackImpactStream()
	{
		return WithFallback(AttackImpact, config => config.GetAttackImpactStream());
	}

	public AudioStream? GetAttackStyleStream(string attackEffectStyle)
	{
		return attackEffectStyle switch
		{
			AdventureAttackEffectStyles.FishBeam => WithFallback(FishBeam, config => config.GetAttackStyleStream(attackEffectStyle)),
			AdventureAttackEffectStyles.CrabPincer => WithFallback(CrabPincer, config => config.GetAttackStyleStream(attackEffectStyle)),
			AdventureAttackEffectStyles.JellyfishTentacle => WithFallback(JellyfishTentacle, config => config.GetAttackStyleStream(attackEffectStyle)),
			AdventureAttackEffectStyles.MantaRayCyclone => WithFallback(MantaRayTideShift, config => config.GetAttackStyleStream(attackEffectStyle)),
			AdventureAttackEffectStyles.SeaSlugMarker => WithFallback(SeaSlugMarker, config => config.GetAttackStyleStream(attackEffectStyle)),
			AdventureAttackEffectStyles.GardenEelBurrow => WithFallback(GardenEelAmbush, config => config.GetAttackStyleStream(attackEffectStyle)),
			AdventureAttackEffectStyles.LeafySeadragonSlash => WithFallback(LeafySeadragonBlade, config => config.GetAttackStyleStream(attackEffectStyle)),
			AdventureAttackEffectStyles.DragonPrinceHit => WithFallback(DragonPrinceHit, config => config.GetAttackStyleStream(attackEffectStyle)),
			AdventureAttackEffectStyles.DragonQueenJaw => WithFallback(DragonQueenJaw, config => config.GetAttackStyleStream(attackEffectStyle)),
			AdventureAttackEffectStyles.BossKRoyalSeizure => WithFallback(BossKRoyalSeizure, config => config.GetAttackStyleStream(attackEffectStyle)),
			_ => null,
		};
	}

	public AudioStream? GetAbilityStream(string abilityId)
	{
		return abilityId switch
		{
			AdventureEnemyAbilities.TideGuard => WithFallback(TideGuard, config => config.GetAbilityStream(abilityId)),
			AdventureEnemyAbilities.BubbleCover => WithFallback(JellyfishBubbleCover, config => config.GetAbilityStream(abilityId)),
			AdventureEnemyAbilities.SandAmbush => WithFallback(GardenEelAmbush, config => config.GetAbilityStream(abilityId)),
			AdventureEnemyAbilities.ColorCurrent => WithFallback(ColorCurrent, config => config.GetAbilityStream(abilityId)),
			AdventureEnemyAbilities.SpiralShell => WithFallback(NautilusSpiralShell, config => config.GetAbilityStream(abilityId)),
			AdventureEnemyAbilities.TuskBreak => WithFallback(NarwhalTuskBreak, config => config.GetAbilityStream(abilityId)),
			AdventureEnemyAbilities.TideShift => WithFallback(MantaRayTideShift, config => config.GetAbilityStream(abilityId)),
			AdventureEnemyAbilities.LeafyMirage => WithFallback(LeafySeadragonBlade, config => config.GetAbilityStream(abilityId)),
			AdventureEnemyAbilities.DragonPrince => WithFallback(DragonPrinceAbility, config => config.GetAbilityStream(abilityId)),
			AdventureEnemyAbilities.DragonQueen => WithFallback(DragonQueenAbility, config => config.GetAbilityStream(abilityId)),
			AdventureEnemyAbilities.DragonKing => WithFallback(DragonKingAbility, config => config.GetAbilityStream(abilityId)),
			_ => null,
		};
	}

	public IEnumerable<AudioStream> GetWarmupStreams()
	{
		foreach (AudioStream stream in GetLocalWarmupStreams())
		{
			yield return stream;
		}

		if (Fallback == null)
		{
			yield break;
		}

		foreach (AudioStream stream in Fallback.GetWarmupStreams())
		{
			yield return stream;
		}
	}

	private IEnumerable<AudioStream> GetLocalWarmupStreams()
	{
		AudioStream?[] streams =
		{
			Score,
			Attack,
			Hit,
			Death,
			Victory,
			Defeat,
			AttackFlight,
			AttackImpact,
			FishBeam,
			CrabPincer,
			JellyfishTentacle,
			MantaRayTideShift,
			SeaSlugMarker,
			GardenEelAmbush,
			LeafySeadragonBlade,
			DragonPrinceHit,
			DragonQueenJaw,
			BossKRoyalSeizure,
			TideGuard,
			JellyfishBubbleCover,
			ColorCurrent,
			NautilusSpiralShell,
			NarwhalTuskBreak,
			DragonPrinceAbility,
			DragonQueenAbility,
			DragonKingAbility,
		};

		foreach (AudioStream? stream in streams)
		{
			if (stream != null)
			{
				yield return stream;
			}
		}
	}

	private AudioStream? WithFallback(AudioStream? stream, Func<CharacterAudioConfig, AudioStream?> fallbackSelector)
	{
		return stream ?? (Fallback == null ? null : fallbackSelector(Fallback));
	}
}
