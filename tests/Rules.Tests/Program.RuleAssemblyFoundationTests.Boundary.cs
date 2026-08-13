using System.Collections.Immutable;
using System.Reflection;
using Deckland.Rules;

internal static partial class Program
{
	private static void RulesAssemblyHasNoGodotReference()
	{
		Assembly rulesAssembly = typeof(AuthorityRuleSession).Assembly;
		Equal("Deckland.Rules", rulesAssembly.GetName().Name);
		foreach (AssemblyName reference in rulesAssembly.GetReferencedAssemblies())
		{
			False(reference.Name?.Contains("Godot", StringComparison.OrdinalIgnoreCase) == true);
		}
	}

	private static void RulesPublicSignaturesHaveNoGodotTypes()
	{
		Assembly rulesAssembly = typeof(AuthorityRuleSession).Assembly;
		foreach (Type type in rulesAssembly.GetExportedTypes())
		{
			AssertNoForbiddenGodotType(type.BaseType);
			foreach (Type implementedInterface in type.GetInterfaces())
			{
				AssertNoForbiddenGodotType(implementedInterface);
			}

			foreach (ConstructorInfo constructor in type.GetConstructors())
			{
				foreach (ParameterInfo parameter in constructor.GetParameters())
				{
					AssertNoForbiddenGodotType(parameter.ParameterType);
				}
			}

			foreach (PropertyInfo property in type.GetProperties())
			{
				AssertNoForbiddenGodotType(property.PropertyType);
			}

			foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public))
			{
				AssertNoForbiddenGodotType(method.ReturnType);
				foreach (ParameterInfo parameter in method.GetParameters())
				{
					AssertNoForbiddenGodotType(parameter.ParameterType);
				}
			}
		}
	}

	private static void RulesBehaviorIsExposedOnlyThroughSessionsAndReadModel()
	{
		foreach (Type implementationType in new[]
		{
			typeof(AiCardSelectionRules),
			typeof(CaptureFlowPlanner),
			typeof(CaptureResolver),
			typeof(CaptureRevealOrder),
			typeof(CaptureScoringRules),
			typeof(CardPlayResolver),
			typeof(EnemyAiRules),
			typeof(GameLoopPhaseRules),
			typeof(GameTurnRules),
			typeof(MatchOutcomeRules),
			typeof(NetworkAiFallbackRules),
			typeof(PotentialCaptureRules),
			typeof(SoloAiRules),
			typeof(TurnGenerationRules),
			typeof(CardTriggerDispatchState),
		})
		{
			False(implementationType.IsPublic);
		}
		True(typeof(AuthorityRuleSession).IsPublic);
		True(typeof(ReplicaRuleSession).IsPublic);
		True(typeof(MatchReadModel).IsPublic);
	}

	private static void CardTriggerDispatchStateStaysInsideAuthoritySession()
	{
		Assembly rulesAssembly = typeof(AuthorityRuleSession).Assembly;
		True(ReferenceEquals(rulesAssembly, typeof(CardGameEvent).Assembly));
		True(ReferenceEquals(rulesAssembly, typeof(CardTriggerRequest).Assembly));
		True(ReferenceEquals(rulesAssembly, typeof(ItemCardEffectRule).Assembly));
		False(typeof(CardTriggerDispatchState).IsPublic);

		string root = FindRepositoryRoot();
		string compatibilitySource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"SpecialCardSystem.cs"));
		string boardSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.cs"));
		string adapterSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.SpecialCards.cs"));
		False(compatibilitySource.Contains("class CardEventBus", StringComparison.Ordinal));
		False(compatibilitySource.Contains("Vector2", StringComparison.Ordinal));
		False(boardSource.Contains("_cardEventBus", StringComparison.Ordinal));
		True(adapterSource.Contains(
			"ruleSession.BeginCardTriggerDispatch()",
			StringComparison.Ordinal));
		True(adapterSource.Contains(
			"new CardEventPosition(",
			StringComparison.Ordinal));

		var extraIndexes = new List<int> { 2, 4 };
		var gameEvent = new CardGameEvent(
			"test",
			extraTableCardIndexes: extraIndexes,
			originGlobalCenter: new CardEventPosition(12.5f, 18.25f));
		extraIndexes.Clear();
		True(gameEvent.ExtraTableCardIndexes.SequenceEqual(new[] { 2, 4 }));
		Equal(new CardEventPosition(12.5f, 18.25f), gameEvent.OriginGlobalCenter!.Value);
	}

	private static void RulesProjectsEnforceCompileBoundary()
	{
		string root = FindRepositoryRoot();
		string mainProject = File.ReadAllText(Path.Combine(root, "pokegame.csproj"));
		string testProject = File.ReadAllText(Path.Combine(root, "tests", "Rules.Tests", "Rules.Tests.csproj"));
		string rulesProject = File.ReadAllText(Path.Combine(root, "src", "Deckland.Rules", "Deckland.Rules.csproj"));

		True(mainProject.Contains("<Compile Remove=\"src/Deckland.Rules/**/*.cs\" />", StringComparison.Ordinal));
		True(mainProject.Contains("<ProjectReference Include=\"src/Deckland.Rules/Deckland.Rules.csproj\" />", StringComparison.Ordinal));
		True(testProject.Contains("<ProjectReference Include=\"../../src/Deckland.Rules/Deckland.Rules.csproj\" />", StringComparison.Ordinal));
		False(rulesProject.Contains("Godot.NET.Sdk", StringComparison.Ordinal));
		False(rulesProject.Contains("GodotSharp", StringComparison.Ordinal));
	}

	private static void CoreStateModelsAndServicesComeFromRulesAssembly()
	{
		Assembly rulesAssembly = typeof(AuthorityRuleSession).Assembly;
		foreach (Type type in new[]
		{
			typeof(GameBoardState),
			typeof(PlayingCard),
			typeof(ItemCard),
			typeof(TableCard),
			typeof(CardPlayStateService),
			typeof(CaptureStateService),
			typeof(TurnAdvancementService),
			typeof(ScoreService),
		})
		{
			True(ReferenceEquals(rulesAssembly, type.Assembly));
		}
	}

	private static void SliceFiveAiRulesComeFromRulesAssembly()
	{
		Assembly rulesAssembly = typeof(AuthorityRuleSession).Assembly;
		foreach (Type type in new[]
		{
			typeof(PotentialCaptureRules),
			typeof(AiCardSelectionRules),
			typeof(EnemyAiRules),
			typeof(SoloAiRules),
			typeof(NetworkAiFallbackRules),
		})
		{
			True(ReferenceEquals(rulesAssembly, type.Assembly));
		}

		string root = FindRepositoryRoot();
		string testProject = File.ReadAllText(Path.Combine(
			root,
			"tests",
			"Rules.Tests",
			"Rules.Tests.csproj"));
		foreach (string sourceName in new[]
		{
			"PotentialCaptureRules.cs",
			"AiCardSelectionRules.cs",
			"EnemyAiRules.cs",
			"SoloAiRules.cs",
			"NetworkAiFallbackRules.cs",
		})
		{
			False(testProject.Contains(sourceName, StringComparison.Ordinal));
			True(File.Exists(Path.Combine(root, "src", "Deckland.Rules", sourceName)));
			False(File.Exists(Path.Combine(root, "scripts", sourceName)));
		}
	}

	private static void GameBoardSelectsAuthorityOrReplicaStateByRole()
	{
		string root = FindRepositoryRoot();
		string boardSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.cs"));
		string adapterSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.RuleSessionAdapter.cs"));
		string assemblyInfoSource = File.ReadAllText(Path.Combine(
			root,
			"src",
			"Deckland.Rules",
			"AssemblyInfo.cs"));

		False(boardSource.Contains("new AuthorityRuleSession", StringComparison.Ordinal));
		True(adapterSource.Contains("IsNetworkMultiplayerMode() && !IsNetworkHost()", StringComparison.Ordinal));
		True(adapterSource.Contains("_replicaRuleSession = new ReplicaRuleSession()", StringComparison.Ordinal));
		True(adapterSource.Contains("_authorityRuleSession = new AuthorityRuleSession(", StringComparison.Ordinal));
		True(adapterSource.Contains("_replicaRuleSession.ApplyAuthoritySnapshot(ruleSnapshot)", StringComparison.Ordinal));
		True(boardSource.Contains(
			"private IReadOnlyList<IReadOnlyList<PlayingCard>> _playerHands =>",
			StringComparison.Ordinal));
		True(boardSource.Contains(
			": CurrentRuleProjection.PlayerHands;",
			StringComparison.Ordinal));
		False(boardSource.Contains("GameBoardState _state", StringComparison.Ordinal));
		False(adapterSource.Contains("_state", StringComparison.Ordinal));
		False(typeof(AuthorityRuleSession).GetProperties(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.Any(property => property.Name == "State"));
		False(typeof(ReplicaRuleSession).GetProperties(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.Any(property => property.Name == "State"));
		False(assemblyInfoSource.Contains(
			"InternalsVisibleTo(\"pokegame\")",
			StringComparison.Ordinal));
		True(assemblyInfoSource.Contains(
			"InternalsVisibleTo(\"Rules.Tests\")",
			StringComparison.Ordinal));
		False(typeof(GameBoardState).IsPublic);
		False(typeof(CardPlayStateService).IsPublic);
		False(typeof(CaptureStateService).IsPublic);
		False(typeof(TurnAdvancementService).IsPublic);
		False(typeof(ScoreService).IsPublic);
		False(typeof(TurnStateMachine).IsPublic);
		foreach (string legacySceneFlag in new[]
		{
			"IsLeavingGame",
			"IsProcessingCardPlay",
			"IsResolvingTurnSequence",
			"IsOpeningDealAnimationPlaying",
			"IsResolvingMagnetAttachmentSelection",
			"IsAnimatingMagnetAttachment",
		})
		{
			False(typeof(GameBoardState).GetProperty(legacySceneFlag) != null);
		}

		foreach (string sourcePath in Directory.GetFiles(
			Path.Combine(root, "scripts"),
			"GameBoardScene*.cs",
			SearchOption.TopDirectoryOnly))
		{
			if (Path.GetFileName(sourcePath).Contains(".Debug.", StringComparison.Ordinal))
			{
				continue;
			}

			string source = File.ReadAllText(sourcePath);
			False(source.Contains("GameBoardState", StringComparison.Ordinal));
			False(System.Text.RegularExpressions.Regex.IsMatch(
				source,
				@"_(?:playerDecks|playerHands|playerItemHands|tableCards|playerScores)(?:\s*\[[^\]]+\])?\s*\.\s*(?:Add|AddRange|Insert|Remove|RemoveAt|RemoveRange|Clear|Sort|Reverse)\s*\("));
			False(System.Text.RegularExpressions.Regex.IsMatch(
				source,
				@"_(?:playerDecks|playerHands|playerItemHands|tableCards|playerScores)\s*\[[^\r\n;]+\]\s*="));
		}
	}

}
