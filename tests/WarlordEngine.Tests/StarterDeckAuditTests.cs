using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using WarlordEngine.AI;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using Xunit;
using Xunit.Abstractions;

namespace WarlordEngine.Tests;

public class StarterDeckAuditTests
{
	public class StarterDeckJson
	{
		public string DeckName { get; set; } = "";

		public string Faction { get; set; } = "";

		public string WarlordName { get; set; } = "";

		public List<CardEntryJson> StartingArmy { get; set; } = new List<CardEntryJson>();

		public List<CardEntryJson> MainDeck { get; set; } = new List<CardEntryJson>();
	}

	public class CardEntryJson
	{
		public string CardName { get; set; } = "";

		public int Count { get; set; }
	}

	public enum CardCompletenessCategory
	{
		FullyImplemented = 1,
		PartiallyImplemented,
		CompletelyUnimplemented
	}

	public record AuditResult(string CardName, CardType Type, string RulesText, CardCompletenessCategory Category, string MissingOrIgnored, string MechanicBucket);

	private readonly ITestOutputHelper _output;

	private static readonly HashSet<string> KnownPassiveAndEngineHandledCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Bruntor's Helm", "Bardiche", "Gapfinder", "Scale Mail", "'Mad' Pat Carrik", "Ac'Vuk", "Ambitious Squire", "Ardian Greathawk", "Balian", "Blitzer",
		"Breastplate of Power", "Dress", "Ghed Bane", "Grimfey Prowler", "Hazahk of Shadow", "Joleg", "Makusog Steelhorn", "Revenger", "Shambling Horror", "Shannon the Daring",
		"Shillelagh", "Squire Argos", "Taumar the Protector", "TFK Brigand", "Tinker", "Tunnel Vanguard", "Unicorn of Thun", "Graveyard Librarian"
	};

	private static readonly HashSet<string> KnownEngineCustomActionCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Crownland Strategist", "Kraig'dem", "Quick Strength", "Quick Shielding", "Gift From Above", "Even the Odds", "Squire Fendric", "Adathan, Celestial Messenger", "Leonora" };

	public StarterDeckAuditTests(ITestOutputHelper output)
	{
		_output = output;
	}

	/// <summary>
	/// Walks up from <see cref="AppContext.BaseDirectory"/> looking for the solution file.
	/// Only needed for source-tree concerns (duplicate checks, generated report); returns null
	/// when running from a layout that has no source tree (e.g. a published test bundle).
	/// </summary>
	private static string? TryFindRepoRoot()
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory);
		while (directoryInfo != null && !File.Exists(Path.Combine(directoryInfo.FullName, "WarlordCCG.sln")))
		{
			directoryInfo = directoryInfo.Parent;
		}
		return directoryInfo?.FullName;
	}

	[Fact]
	public void Generate_Starter_Deck_Gap_Report()
	{
		string path2 = DataPaths.StarterDecksJsonPath;
		string jsonFilePath = DataPaths.CardsJsonPath;
		CardDatabase cardDatabase = DatabaseLoader.LoadFromFile(jsonFilePath);
		string json = File.ReadAllText(path2);
		List<StarterDeckJson> list = JsonSerializer.Deserialize<List<StarterDeckJson>>(json);
		SortedSet<string> sortedSet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (StarterDeckJson item2 in list)
		{
			if (!string.IsNullOrWhiteSpace(item2.WarlordName))
			{
				sortedSet.Add(item2.WarlordName);
			}
			foreach (CardEntryJson item3 in item2.StartingArmy)
			{
				sortedSet.Add(item3.CardName);
			}
			foreach (CardEntryJson item4 in item2.MainDeck)
			{
				sortedSet.Add(item4.CardName);
			}
		}
		List<AuditResult> list2 = new List<AuditResult>();
		foreach (string name in sortedSet)
		{
			CardDefinition cardDefinition = cardDatabase.Definitions.Values.FirstOrDefault((CardDefinition d) => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (cardDefinition == null)
			{
				string definitionId = name.ToLowerInvariant().Replace(" ", "_").Replace("'", "")
					.Replace(",", "");
				cardDefinition = cardDatabase.GetDefinition(definitionId);
			}
			Assert.NotNull(cardDefinition);
			AuditResult item = AuditCard(cardDefinition);
			list2.Add(item);
		}
		string contents = GenerateMarkdownReport(list2);
		string text = Path.Combine(TryFindRepoRoot() ?? AppContext.BaseDirectory, "STARTER_DECK_GAP_REPORT.md");
		File.WriteAllText(text, contents);
		_output.WriteLine("Report generated at " + text);
		_output.WriteLine($"Category 1 (Fully Implemented): {list2.Count((AuditResult r) => r.Category == CardCompletenessCategory.FullyImplemented)}");
		_output.WriteLine($"Category 2 (Partially Implemented): {list2.Count((AuditResult r) => r.Category == CardCompletenessCategory.PartiallyImplemented)}");
		_output.WriteLine($"Category 3 (Completely Unimplemented): {list2.Count((AuditResult r) => r.Category == CardCompletenessCategory.CompletelyUnimplemented)}");
		Assert.DoesNotContain(list2, (AuditResult r) => r.Category != CardCompletenessCategory.FullyImplemented);
	}

	private static AuditResult AuditCard(CardDefinition def)
	{
		string text = def.RulesText.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return new AuditResult(def.Name, def.Type, text, CardCompletenessCategory.FullyImplemented, "None (Vanilla card stats and strikes fully modeled in combat engine)", "None");
		}
		foreach (AbilityDefinition ability in def.Abilities)
		{
			if (ability.Effects.Any((IEffect e) => (e is FortitudeSaveEffect || e is WillSaveEffect || e is FireballEffect || e is TheMightyMuggEffect) ? true : false) && ability.Effects.Any((IEffect e) => e is DealDamageEffect))
			{
				return new AuditResult(def.Name, def.Type, text, CardCompletenessCategory.PartiallyImplemented, "Bogus DealDamageEffect attached alongside specialized effect in ability '" + ability.Description + "'", "Regex Collision Bug");
			}
			if (ability.Effects.Any((IEffect e) => (e is HrunInstigatorEffect || e is MagicMissilesEffect || e is TargetPerformsStrikeEffect || e is BanishFromDiscardAndStrikeEffect || e is FireballEffect) ? true : false) && ability.Effects.Any((IEffect e) => e is PerformStrikeEffect))
			{
				return new AuditResult(def.Name, def.Type, text, CardCompletenessCategory.PartiallyImplemented, "Bogus PerformStrikeEffect attached alongside specialized strike effect in ability '" + ability.Description + "'", "Regex Collision Bug");
			}
		}
		bool flag = def.Abilities.Count > 0 && def.Abilities.All((AbilityDefinition a) => a.Effects.Count > 0 || KnownEngineCustomActionCards.Contains(def.Name));
		bool flag2 = KnownPassiveAndEngineHandledCards.Contains(def.Name);
		bool flag3 = KnownEngineCustomActionCards.Contains(def.Name);
		if (flag | flag2 | flag3)
		{
			string missingOrIgnored = (flag ? $"None ({def.Abilities.Count} ability/abilities parsed: {string.Join("; ", def.Abilities.Select((AbilityDefinition a) => $"{a.Trigger}[{string.Join(",", a.Effects.Select((IEffect e) => e.GetType().Name))}]"))})" : "None (Handled via engine passive / custom card rules)");
			return new AuditResult(def.Name, def.Type, text, CardCompletenessCategory.FullyImplemented, missingOrIgnored, "None");
		}
		if (def.Abilities.Any((AbilityDefinition a) => a.Effects.Count > 0))
		{
			return new AuditResult(def.Name, def.Type, text, CardCompletenessCategory.PartiallyImplemented, "Some abilities have 0 parsed effects", "Bucket E: Modal / Conditional Branching Actions");
		}
		return new AuditResult(def.Name, def.Type, text, CardCompletenessCategory.CompletelyUnimplemented, "Effects.Count == 0 and not registered in passive/custom handlers", "Bucket C: Character Aura & Conditional Stat Passives");
	}

	[Fact]
	public void MctsAndHeuristicAi_Simulations_NeverFireStaticOrEngineCallbacks()
	{
		CardDatabase cardDb = DatabaseLoader.LoadFromFile(DataPaths.CardsJsonPath);
		GameState gameState = new GameState
		{
			CurrentPhase = TurnPhase.Main,
			ActivePlayerIndex = 0
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 2;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "brine_fiend",
			CurrentHitPoints = 3,
			CurrentArmorClass = 14,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		span2[1] = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "brine_fiend",
			CurrentHitPoints = 3,
			CurrentArmorClass = 14,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = new CardInstance
		{
			InstanceId = 50,
			DefinitionId = "jigoral",
			CurrentHitPoints = 1,
			CurrentArmorClass = 8,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		sortedDictionary2[1] = list3;
		num2 = 2;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		Span<CardInstance> span3 = CollectionsMarshal.AsSpan(list4);
		span3[0] = new CardInstance
		{
			InstanceId = 51,
			DefinitionId = "graveyard_librarian",
			CurrentHitPoints = 2,
			CurrentArmorClass = 11,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 2
		};
		span3[1] = new CardInstance
		{
			InstanceId = 52,
			DefinitionId = "graveyard_librarian",
			CurrentHitPoints = 2,
			CurrentArmorClass = 11,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 2
		};
		sortedDictionary2[2] = list4;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb, (int _, int _) => 18);
		int formationCallbackCount = 0;
		int globalAttackCallbackCount = 0;
		int engineAttackCallbackCount = 0;
		Action<string> action = (string _) =>
		{
			formationCallbackCount++;
		};
		Action<AttackResolutionDetails> action2 = (AttackResolutionDetails _) =>
		{
			globalAttackCallbackCount++;
		};
		Action<AttackResolutionDetails> action3 = (AttackResolutionDetails _) =>
		{
			engineAttackCallbackCount++;
		};
		BoardGeometry.OnFormationNotice = (Action<string>)Delegate.Combine(BoardGeometry.OnFormationNotice, action);
		ResolveAttackEffect.GlobalOnAttackResolved = (Action<AttackResolutionDetails>)Delegate.Combine(ResolveAttackEffect.GlobalOnAttackResolved, action2);
		gameEngine.OnAttackResolved = (Action<AttackResolutionDetails>)Delegate.Combine(gameEngine.OnAttackResolved, action3);
		try
		{
			MctsAiAgent mctsAiAgent = new MctsAiAgent(null, 40, 1.414, 8, "MctsAgent", new Random(42));
			IPlayerAction playerAction = mctsAiAgent.SelectAction(gameState2, gameEngine, 0);
			Assert.NotNull(playerAction);
			HeuristicAiAgent heuristicAiAgent = new HeuristicAiAgent();
			IPlayerAction playerAction2 = heuristicAiAgent.SelectAction(gameState2, gameEngine, 0);
			Assert.NotNull(playerAction2);
			Assert.Equal(0, formationCallbackCount);
			Assert.Equal(0, globalAttackCallbackCount);
			Assert.Equal(0, engineAttackCallbackCount);
			Assert.Single(gameState2.Players[1].Ranks[1]);
			Assert.Equal(2, gameState2.Players[1].Ranks[2].Count);
			Assert.False(gameState2.IsSimulation);
		}
		finally
		{
			BoardGeometry.OnFormationNotice = (Action<string>)Delegate.Remove(BoardGeometry.OnFormationNotice, action);
			ResolveAttackEffect.GlobalOnAttackResolved = (Action<AttackResolutionDetails>)Delegate.Remove(ResolveAttackEffect.GlobalOnAttackResolved, action2);
			gameEngine.OnAttackResolved = (Action<AttackResolutionDetails>)Delegate.Remove(gameEngine.OnAttackResolved, action3);
		}
	}

	[Fact]
	public void FormationCollapse_FollowsTabletopRules_AndPosturePenalties()
	{
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num = 3;
		List<CardInstance> list = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<CardInstance> span = CollectionsMarshal.AsSpan(list);
		span[0] = new CardInstance
		{
			InstanceId = 53,
			DefinitionId = "jigoral",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		span[1] = new CardInstance
		{
			InstanceId = 54,
			DefinitionId = "jigoral",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		span[2] = new CardInstance
		{
			InstanceId = 55,
			DefinitionId = "jigoral",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		sortedDictionary[1] = list;
		num = 2;
		List<CardInstance> list2 = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = new CardInstance
		{
			InstanceId = 51,
			DefinitionId = "graveyard_librarian",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 2
		};
		span2[1] = new CardInstance
		{
			InstanceId = 52,
			DefinitionId = "graveyard_librarian",
			ReadyState = ReadyState.Spent,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 2
		};
		sortedDictionary[2] = list2;
		playerState.Ranks = sortedDictionary;
		PlayerState playerState2 = playerState;
		playerState2.Ranks[1].RemoveAll((CardInstance c) => c.InstanceId == 54);
		BoardGeometry.EnforceFormation(playerState2);
		Assert.Equal(2, playerState2.Ranks[1].Count);
		Assert.Equal(2, playerState2.Ranks[2].Count);
		Assert.Equal(ReadyState.Ready, playerState2.Ranks[2][0].ReadyState);
		Assert.Equal(ReadyState.Spent, playerState2.Ranks[2][1].ReadyState);
		playerState2.Ranks[1].RemoveAll((CardInstance c) => c.InstanceId == 53);
		BoardGeometry.EnforceFormation(playerState2);
		Assert.Equal(2, playerState2.Ranks[1].Count);
		Assert.Single(playerState2.Ranks[2]);
		CardInstance cardInstance = playerState2.Ranks[1].First((CardInstance c) => c.InstanceId == 51);
		Assert.Equal(1, cardInstance.Rank);
		Assert.Equal(ReadyState.Spent, cardInstance.ReadyState);
		PlayerState playerState3 = new PlayerState
		{
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num = 2;
		List<CardInstance> list3 = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list3, num);
		Span<CardInstance> span3 = CollectionsMarshal.AsSpan(list3);
		span3[0] = new CardInstance
		{
			InstanceId = 61,
			DefinitionId = "graveyard_librarian",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 2
		};
		span3[1] = new CardInstance
		{
			InstanceId = 62,
			DefinitionId = "graveyard_librarian",
			ReadyState = ReadyState.Spent,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 2
		};
		sortedDictionary2[2] = list3;
		playerState3.Ranks = sortedDictionary2;
		PlayerState playerState4 = playerState3;
		BoardGeometry.EnforceFormation(playerState4);
		Assert.Single(playerState4.Ranks[1]);
		Assert.Single(playerState4.Ranks[2]);
		Assert.Equal(61, playerState4.Ranks[1][0].InstanceId);
		Assert.Equal(ReadyState.Spent, playerState4.Ranks[1][0].ReadyState);
		Assert.Equal(62, playerState4.Ranks[2][0].InstanceId);
		Assert.Equal(ReadyState.Spent, playerState4.Ranks[2][0].ReadyState);
	}

	[Fact]
	public void SingleSourceOfTruth_CardsJson_OnlyExistsInDataParser()
	{
		string? path = TryFindRepoRoot();
		if (path != null)
		{
			Assert.False(File.Exists(Path.Combine(path, "cards.json")), "Duplicate ./cards.json in repo root must not exist.");
			Assert.False(File.Exists(Path.Combine(path, "src", "cards.json")), "Duplicate src/cards.json must not exist.");
			Assert.True(File.Exists(Path.Combine(path, "DataParser", "cards.json")), "DataParser/cards.json must exist as the single source of truth.");
		}
		Assert.True(File.Exists(DataPaths.CardsJsonPath), "DataParser/cards.json must be copied to the output directory.");
	}

	[Fact]
	public void StarterDeckCards_TruthCheck_BehavioralVerification()
	{
		CardDatabase cardDatabase = DatabaseLoader.LoadFromFile(DataPaths.CardsJsonPath);
		CardDefinition cardDefinition = cardDatabase.Definitions.Values.First((CardDefinition d) => d.Name.Equals("Fireball", StringComparison.OrdinalIgnoreCase));
		Assert.Single(cardDefinition.Abilities);
		Assert.Equal(AbilityTrigger.SpendOrder, cardDefinition.Abilities[0].Trigger);
		Assert.Single(cardDefinition.Abilities[0].Effects);
		Assert.IsType<FireballEffect>(cardDefinition.Abilities[0].Effects[0]);
		CardDefinition cardDefinition2 = cardDatabase.Definitions.Values.First((CardDefinition d) => d.Name.Equals("Sunforge Smith", StringComparison.OrdinalIgnoreCase));
		Assert.Equal(2, cardDefinition2.Abilities.Count);
		Assert.All(cardDefinition2.Abilities, (AbilityDefinition a) =>
		{
			Assert.Equal(AbilityTrigger.SpendOrder, a.Trigger);
		});
		Assert.Single(cardDefinition2.Abilities[0].Effects);
		Assert.IsType<ChangeReadyStateEffect>(cardDefinition2.Abilities[0].Effects[0]);
		Assert.Single(cardDefinition2.Abilities[1].Effects);
		Assert.IsType<TargetPerformsStrikeEffect>(cardDefinition2.Abilities[1].Effects[0]);
		CardDefinition cardDefinition3 = cardDatabase.Definitions.Values.First((CardDefinition d) => d.Name.Equals("Memoriam", StringComparison.OrdinalIgnoreCase));
		Assert.Single(cardDefinition3.Abilities);
		Assert.Contains(cardDefinition3.Abilities[0].Effects, (IEffect e) => e is HealEffect);
		Assert.Contains(cardDefinition3.Abilities[0].Effects, (IEffect e) => e is ChangeReadyStateEffect changeReadyStateEffect && changeReadyStateEffect.TargetState == ReadyState.Ready);
		CardDefinition cardDefinition4 = cardDatabase.Definitions.Values.First((CardDefinition d) => d.Name.Equals("Salvage", StringComparison.OrdinalIgnoreCase));
		Assert.Equal(ReactTiming.BeforeDies, cardDefinition4.Abilities[0].Timing);
		CardDefinition cardDefinition5 = cardDatabase.Definitions.Values.First((CardDefinition d) => d.Name.Equals("Callow Rowan", StringComparison.OrdinalIgnoreCase));
		Assert.Equal(ReactTiming.BeforeDies, cardDefinition5.Abilities[0].Timing);
		CardDefinition cardDefinition6 = cardDatabase.Definitions.Values.First((CardDefinition d) => d.Name.Equals("Lord's Assailant", StringComparison.OrdinalIgnoreCase));
		Assert.Equal(ReactTiming.AfterWizardCastsSpell, cardDefinition6.Abilities[0].Timing);
		CardDefinition cardDefinition7 = cardDatabase.Definitions.Values.First((CardDefinition d) => d.Name.Equals("Leveled!", StringComparison.OrdinalIgnoreCase));
		Assert.Equal(ReactTiming.Kill, cardDefinition7.Abilities[0].Timing);
	}

	private static string GenerateMarkdownReport(List<AuditResult> results)
	{
		List<AuditResult> list = results.Where((AuditResult r) => r.Category == CardCompletenessCategory.FullyImplemented).ToList();
		List<AuditResult> list2 = results.Where((AuditResult r) => r.Category == CardCompletenessCategory.PartiallyImplemented).ToList();
		List<AuditResult> list3 = results.Where((AuditResult r) => r.Category == CardCompletenessCategory.CompletelyUnimplemented).ToList();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("# Starter Deck Completeness Audit Report");
		stringBuilder.AppendLine();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(32, 1, stringBuilder2);
		handler.AppendLiteral("**Total Unique Cards Audited:** ");
		handler.AppendFormatted(results.Count);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(53, 2, stringBuilder2);
		handler.AppendLiteral("- **Category 1 (100% Fully Implemented):** ");
		handler.AppendFormatted(list.Count);
		handler.AppendLiteral(" cards (");
		handler.AppendFormatted((double)list.Count * 100.0 / (double)results.Count, "F1");
		handler.AppendLiteral("%)");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(52, 2, stringBuilder2);
		handler.AppendLiteral("- **Category 2 (Partially Implemented):** ");
		handler.AppendFormatted(list2.Count);
		handler.AppendLiteral(" cards (");
		handler.AppendFormatted((double)list2.Count * 100.0 / (double)results.Count, "F1");
		handler.AppendLiteral("%)");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(55, 2, stringBuilder2);
		handler.AppendLiteral("- **Category 3 (Completely Unimplemented):** ");
		handler.AppendFormatted(list3.Count);
		handler.AppendLiteral(" cards (");
		handler.AppendFormatted((double)list3.Count * 100.0 / (double)results.Count, "F1");
		handler.AppendLiteral("%)");
		stringBuilder6.AppendLine(ref handler);
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("---");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("## Category 1: 100% Fully Implemented Cards");
		stringBuilder.AppendLine();
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder2);
		handler.AppendLiteral("**Count:** ");
		handler.AppendFormatted(list.Count);
		stringBuilder7.AppendLine(ref handler);
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("| Card Name | Type | Rules Text Summary / Status |");
		stringBuilder.AppendLine("| :--- | :--- | :--- |");
		foreach (AuditResult item in list)
		{
			string value = (string.IsNullOrWhiteSpace(item.RulesText) ? "Vanilla (Combat stats & strikes modeled)" : item.RulesText.Replace("\n", " ").Replace("\\n", " "));
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder8 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(14, 3, stringBuilder2);
			handler.AppendLiteral("| **");
			handler.AppendFormatted(item.CardName);
			handler.AppendLiteral("** | ");
			handler.AppendFormatted(item.Type);
			handler.AppendLiteral(" | ");
			handler.AppendFormatted(value);
			handler.AppendLiteral(" |");
			stringBuilder8.AppendLine(ref handler);
		}
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("---");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("## Category 2: Partially Implemented Cards");
		stringBuilder.AppendLine();
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder9 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder2);
		handler.AppendLiteral("**Count:** ");
		handler.AppendFormatted(list2.Count);
		stringBuilder9.AppendLine(ref handler);
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("These cards have base triggers or primary strikes parsed into concrete `IEffect`s, but secondary clauses (stun/spend on hit, discard/spend costs, dynamic stat formulas, retaliatory strikes, or sub-actions) are silently ignored or omitted.");
		stringBuilder.AppendLine();
		foreach (AuditResult item2 in list2)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder10 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(7, 2, stringBuilder2);
			handler.AppendLiteral("### ");
			handler.AppendFormatted(item2.CardName);
			handler.AppendLiteral(" (");
			handler.AppendFormatted(item2.Type);
			handler.AppendLiteral(")");
			stringBuilder10.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder11 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(24, 1, stringBuilder2);
			handler.AppendLiteral("- **Full RulesText:** `");
			handler.AppendFormatted(item2.RulesText.Replace("\\n", "\n"));
			handler.AppendLiteral("`");
			stringBuilder11.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder12 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(32, 1, stringBuilder2);
			handler.AppendLiteral("- **Missing / Ignored Clause:** ");
			handler.AppendFormatted(item2.MissingOrIgnored);
			stringBuilder12.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder13 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder2);
			handler.AppendLiteral("- **Mechanic Bucket:** `");
			handler.AppendFormatted(item2.MechanicBucket);
			handler.AppendLiteral("`");
			stringBuilder13.AppendLine(ref handler);
			stringBuilder.AppendLine();
		}
		stringBuilder.AppendLine("---");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("## Category 3: Completely Unimplemented Cards");
		stringBuilder.AppendLine();
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder14 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder2);
		handler.AppendLiteral("**Count:** ");
		handler.AppendFormatted(list3.Count);
		stringBuilder14.AppendLine(ref handler);
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("These cards have `Effects.Count == 0`, are skipped due to unrecognized trigger formats (e.g. `Roll Order:`, `Kill React:`, literal escaped newlines `\\n`), or feature continuous passive aura rules that are entirely unhandled by `GameEngine` or `GetItemPassiveStats`.");
		stringBuilder.AppendLine();
		foreach (AuditResult item3 in list3)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder15 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(7, 2, stringBuilder2);
			handler.AppendLiteral("### ");
			handler.AppendFormatted(item3.CardName);
			handler.AppendLiteral(" (");
			handler.AppendFormatted(item3.Type);
			handler.AppendLiteral(")");
			stringBuilder15.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder16 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(24, 1, stringBuilder2);
			handler.AppendLiteral("- **Full RulesText:** `");
			handler.AppendFormatted(item3.RulesText.Replace("\\n", "\n"));
			handler.AppendLiteral("`");
			stringBuilder16.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder17 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder2);
			handler.AppendLiteral("- **Why Unimplemented:** ");
			handler.AppendFormatted(item3.MissingOrIgnored);
			stringBuilder17.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder18 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder2);
			handler.AppendLiteral("- **Mechanic Bucket:** `");
			handler.AppendFormatted(item3.MechanicBucket);
			handler.AppendLiteral("`");
			stringBuilder18.AppendLine(ref handler);
			stringBuilder.AppendLine();
		}
		stringBuilder.AppendLine("---");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("## Grouped Mechanic Buckets (The Action Plan)");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("To efficiently implement all 94 missing and partial cards across starter decks without ad-hoc single-card patching, the remaining gaps are grouped into 5 concrete mechanic buckets:");
		stringBuilder.AppendLine();
		List<AuditResult> source = list2.Concat(list3).ToList();
		IOrderedEnumerable<IGrouping<string, AuditResult>> orderedEnumerable = from c in source
			group c by c.MechanicBucket into g
			orderby g.Key
			select g;
		foreach (IGrouping<string, AuditResult> item4 in orderedEnumerable)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder19 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(13, 2, stringBuilder2);
			handler.AppendLiteral("### ");
			handler.AppendFormatted(item4.Key);
			handler.AppendLiteral(" (");
			handler.AppendFormatted(item4.Count());
			handler.AppendLiteral(" Cards)");
			stringBuilder19.AppendLine(ref handler);
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("| Card Name | Type | Category | Core Missing Mechanic |");
			stringBuilder.AppendLine("| :--- | :--- | :--- | :--- |");
			foreach (AuditResult item5 in item4.OrderBy((AuditResult x) => x.CardName))
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder20 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(21, 4, stringBuilder2);
				handler.AppendLiteral("| **");
				handler.AppendFormatted(item5.CardName);
				handler.AppendLiteral("** | ");
				handler.AppendFormatted(item5.Type);
				handler.AppendLiteral(" | Cat ");
				handler.AppendFormatted((int)item5.Category);
				handler.AppendLiteral(" | ");
				handler.AppendFormatted(item5.MissingOrIgnored);
				handler.AppendLiteral(" |");
				stringBuilder20.AppendLine(ref handler);
			}
			stringBuilder.AppendLine();
		}
		return stringBuilder.ToString();
	}
}
