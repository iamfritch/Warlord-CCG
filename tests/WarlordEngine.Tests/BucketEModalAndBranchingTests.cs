using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public class BucketEModalAndBranchingTests
{
	private static GameState CreateTwoPlayerState(CardInstance p0Card, CardInstance p1Card)
	{
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			NextInstanceId = 100,
			AutoPassWhenNoReactsAvailable = true
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0,
			DisplayName = "Player 1"
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = p0Card.Rank ?? 1;
		int key = num2;
		int num3 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num3);
		CollectionsMarshal.SetCount(list2, num3);
		CollectionsMarshal.AsSpan(list2)[0] = p0Card;
		sortedDictionary[key] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Player 2"
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = p1Card.Rank ?? 1;
		int key2 = num2;
		num3 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num3);
		CollectionsMarshal.SetCount(list3, num3);
		CollectionsMarshal.AsSpan(list3)[0] = p1Card;
		sortedDictionary2[key2] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		return gameState;
	}

	[Fact]
	public void Test_SunforgeSmith_ModalParsing_HasTwoAbilities()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "sunforge_smith",
			Name = "Sunforge Smith",
			Type = CardType.Character,
			Faction = "Dwarven Forges",
			Level = 2,
			RulesText = "Spend Order: Unstun a wounded Dwarven Forges character in your first or second rank, or the target performs a +2 melee strike."
		};
		List<AbilityDefinition> list = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name);
		Assert.Equal(2, list.Count);
		Assert.True(list[0].RequiresSpend);
		Assert.Contains(list[0].Effects, (IEffect e) => e is ChangeReadyStateEffect changeReadyStateEffect && changeReadyStateEffect.OnlyIfStunned);
		Assert.True(list[1].RequiresSpend);
		Assert.Contains(list[1].Effects, (IEffect e) => e is TargetPerformsStrikeEffect);
	}

	[Fact]
	public void Test_SunforgeSmith_Choice0_UnstunsWoundedDwarf()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "sunforge_smith",
			Name = "Sunforge Smith",
			Type = CardType.Character,
			Faction = "Dwarven Forges",
			Level = 2,
			RulesText = "Spend Order: Unstun a wounded Dwarven Forges character in your first or second rank, or the target performs a +2 melee strike."
		};
		CardDefinition cardDefinition2 = cardDefinition with
		{
			Abilities = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name)
		};
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "dwarf",
			Name = "Dwarf Fighter",
			Type = CardType.Character,
			Faction = "Dwarven Forges",
			Level = 1,
			BaseHitPoints = 5
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3
			}
		};
		CardInstance p0Card = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "sunforge_smith",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board,
			ReadyState = ReadyState.Ready
		};
		CardInstance item = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "dwarf",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board,
			ReadyState = ReadyState.Stunned,
			CurrentHitPoints = 3
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "dwarf",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board
		};
		GameState gameState = CreateTwoPlayerState(p0Card, p1Card);
		gameState.Players[0].Ranks[1].Add(item);
		GameEngine gameEngine = new GameEngine(gameState, cardDb);
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 1,
			AbilityIndex = 0,
			ChoiceIndex = 0,
			TargetInstanceIds = new int[1] { 2 }
		};
		ActionResult actionResult = gameEngine.ExecuteAction(action);
		Assert.True(actionResult.IsSuccess);
		Assert.Equal(ReadyState.Spent, gameState.Players[0].Ranks[1].First((CardInstance c) => c.InstanceId == 1).ReadyState);
		Assert.Equal(ReadyState.Spent, gameState.Players[0].Ranks[1].First((CardInstance c) => c.InstanceId == 2).ReadyState);
	}

	[Fact]
	public void Test_HolyMight_ModalChoice_ATK_vs_AC()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "holy_might",
			Name = "Holy Might",
			Type = CardType.Action,
			RulesText = "Order: Heal 1 wound on target character. Target character gains either +3 ATK or +3 AC this turn."
		};
		CardDefinition cardDefinition2 = cardDefinition with
		{
			Abilities = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name)
		};
		Assert.Equal(2, cardDefinition2.Abilities.Count);
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "cleric",
			Name = "Cleric",
			Type = CardType.Character,
			Level = 1,
			BaseHitPoints = 5,
			ArmorClass = 10
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3
			}
		};
		CardInstance p0Card = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "cleric",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 3
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "cleric",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board
		};
		GameState gameState = CreateTwoPlayerState(p0Card, p1Card);
		CardInstance item = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "holy_might",
			OwnerPlayerIndex = 0,
			Zone = CardZone.Hand
		};
		gameState.Players[0].Hand.Add(item);
		GameEngine gameEngine = new GameEngine(gameState, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			TargetCharacterId = 1,
			ChoiceIndex = 1
		};
		ActionResult actionResult = gameEngine.ExecuteAction(action);
		Assert.True(actionResult.IsSuccess);
		Assert.Equal(4, gameState.Players[0].Ranks[1].First((CardInstance c) => c.InstanceId == 1).CurrentHitPoints);
		Assert.Equal(3, GameEngine.GetTurnBuffValue(gameState, 1, "AC"));
		Assert.Equal(0, GameEngine.GetTurnBuffValue(gameState, 1, "ATK"));
	}

	[Fact]
	public void Test_WarHorse_ModalChoice_Move_vs_ATK()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "war_horse",
			Name = "War Horse",
			Type = CardType.Item,
			RulesText = "Order: Spend War Horse: Move this character forward one rank.\\nOrder: Spend War Horse: This character gains +2 ATK this turn."
		};
		CardDefinition cardDefinition2 = cardDefinition with
		{
			Abilities = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name)
		};
		Assert.Equal(2, cardDefinition2.Abilities.Count);
		Assert.Single(cardDefinition2.Abilities[0].Effects);
		Assert.IsType<MoveRankEffect>(cardDefinition2.Abilities[0].Effects[0]);
		Assert.IsType<MoveRankEffect>(cardDefinition2.Abilities[0].Effects[0]);
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "rider",
			Name = "Knight Rider",
			Type = CardType.Character,
			Level = 2,
			BaseHitPoints = 5
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "rider",
			OwnerPlayerIndex = 0,
			Rank = 2,
			Zone = CardZone.Board
		};
		int num = 1;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = 5;
		cardInstance.AttachedItemIds = list;
		CardInstance cardInstance2 = cardInstance;
		CardInstance item = new CardInstance
		{
			InstanceId = 5,
			DefinitionId = "war_horse",
			OwnerPlayerIndex = 0,
			Zone = CardZone.Board,
			AttachedToInstanceId = 1,
			ReadyState = ReadyState.Ready
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "rider",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board
		};
		GameState gameState = CreateTwoPlayerState(cardInstance2, p1Card);
		SortedDictionary<int, List<CardInstance>> ranks = gameState.Players[0].Ranks;
		num = 1;
		List<CardInstance> list2 = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list2, num);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance2;
		ranks[2] = list2;
		gameState.Players[0].Ranks.Remove(1);
		gameState.Players[0].DiscardPile.Add(item);
		GameEngine gameEngine = new GameEngine(gameState, cardDb);
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 5,
			AbilityIndex = 0,
			ChoiceIndex = 0,
			TargetInstanceIds = Array.Empty<int>()
		};
		ActionResult actionResult = gameEngine.ExecuteAction(action);
		Assert.True(actionResult.IsSuccess, "Execute failed: " + string.Join(", ", actionResult.Errors));
		Assert.False(gameState.Priority.IsReactWindowOpen, "React window is open! Pending events: " + string.Join(", ", gameState.PendingEvents.Select((GameEvent e) => e.GetType().Name)));
		Assert.True(gameState.Players[0].Ranks.ContainsKey(1) && gameState.Players[0].Ranks[1].Any((CardInstance c) => c.InstanceId == 1), "Rider not in rank 1! Current ranks: " + string.Join(", ", gameState.Players[0].Ranks.Select((KeyValuePair<int, List<CardInstance>> kv) => $"Rank {kv.Key}: [{string.Join(", ", kv.Value.Select((CardInstance c) => c.InstanceId))}]")));
		Assert.Equal(ReadyState.Spent, gameState.Players[0].DiscardPile.First((CardInstance c) => c.InstanceId == 5).ReadyState);
	}

	[Fact]
	public void Test_ArcaneApproach_WizardBranch_MovesForward()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "arcane_approach",
			Name = "Arcane Approach",
			Type = CardType.Action,
			RulesText = "Order: If the performer is a wizard, your target character moves forward one rank. If the performer is a fighter, perform a +3 melee strike."
		};
		CardDefinition cardDefinition2 = cardDefinition with
		{
			Abilities = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name)
		};
		Assert.Equal(2, cardDefinition2.Abilities.Count);
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "wiz",
			Name = "Wizard Performer",
			Type = CardType.Character,
			Class = CharacterClass.Wizard,
			Level = 2,
			BaseHitPoints = 3
		};
		CardDefinition cardDefinition4 = new CardDefinition
		{
			DefinitionId = "ally",
			Name = "Ally",
			Type = CardType.Character,
			Level = 1,
			BaseHitPoints = 3
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3,
				[cardDefinition4.DefinitionId] = cardDefinition4
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "wiz",
			OwnerPlayerIndex = 0,
			Rank = 2,
			Zone = CardZone.Board
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "ally",
			OwnerPlayerIndex = 0,
			Rank = 2,
			Zone = CardZone.Board
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "ally",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board
		};
		GameState gameState = CreateTwoPlayerState(cardInstance, p1Card);
		SortedDictionary<int, List<CardInstance>> ranks = gameState.Players[0].Ranks;
		int num = 2;
		List<CardInstance> list = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<CardInstance> span = CollectionsMarshal.AsSpan(list);
		span[0] = cardInstance;
		span[1] = cardInstance2;
		ranks[2] = list;
		gameState.Players[0].Ranks.Remove(1);
		CardInstance item = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "arcane_approach",
			OwnerPlayerIndex = 0,
			Zone = CardZone.Hand
		};
		gameState.Players[0].Hand.Add(item);
		GameEngine gameEngine = new GameEngine(gameState, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			PerformerInstanceId = 1,
			TargetCharacterId = 2,
			AbilityIndex = 0
		};
		ActionResult actionResult = gameEngine.ExecuteAction(action);
		Assert.True(actionResult.IsSuccess, "Execute failed: " + string.Join(", ", actionResult.Errors));
		Assert.False(gameState.Priority.IsReactWindowOpen, "React window is open! Pending events: " + string.Join(", ", gameState.PendingEvents.Select((GameEvent e) => e.GetType().Name)));
		Assert.True(gameState.Players[0].Ranks.ContainsKey(1) && gameState.Players[0].Ranks[1].Any((CardInstance c) => c.InstanceId == 2), "Ally not in rank 1! Current ranks: " + string.Join(", ", gameState.Players[0].Ranks.Select((KeyValuePair<int, List<CardInstance>> kv) => $"Rank {kv.Key}: [{string.Join(", ", kv.Value.Select((CardInstance c) => c.InstanceId))}]")));
	}

	[Fact]
	public void Test_ShadowBolts_RogueBranch_WoundsSpentDefender()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "shadow_bolts",
			Name = "Shadow Bolts",
			Type = CardType.Action,
			RulesText = "Order: Perform a +4 ranged strike. If the performer is a rogue, the strike inflicts an additional wound if the defender is spent. If the performer is a wizard, perform an additional +4 ranged strike."
		};
		CardDefinition cardDefinition2 = cardDefinition with
		{
			Abilities = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name)
		};
		Assert.Single(cardDefinition2.Abilities);
		Assert.IsType<ShadowBoltsEffect>(cardDefinition2.Abilities[0].Effects[0]);
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "rogue",
			Name = "Rogue Performer",
			Type = CardType.Character,
			Class = CharacterClass.Rogue,
			Level = 2,
			ArmorClass = 10,
			BaseHitPoints = 5
		};
		CardDefinition cardDefinition4 = new CardDefinition
		{
			DefinitionId = "victim",
			Name = "Victim",
			Type = CardType.Character,
			Level = 1,
			ArmorClass = 10,
			BaseHitPoints = 5
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3,
				[cardDefinition4.DefinitionId] = cardDefinition4
			}
		};
		CardInstance p0Card = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "rogue",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "victim",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board,
			ReadyState = ReadyState.Spent
		};
		GameState state = CreateTwoPlayerState(p0Card, p1Card);
		EffectContext context = new EffectContext
		{
			SourceInstanceId = 1,
			TargetInstanceId = 2,
			State = state,
			CardDb = cardDb,
			RollDice = (int n, int s) => 15
		};
		ShadowBoltsEffect shadowBoltsEffect = (ShadowBoltsEffect)cardDefinition2.Abilities[0].Effects[0];
		List<GameEvent> source = shadowBoltsEffect.Evaluate(context).ToList();
		DealDamageEvent dealDamageEvent = source.OfType<DealDamageEvent>().FirstOrDefault();
		Assert.NotNull(dealDamageEvent);
		Assert.Equal(2, dealDamageEvent.Amount);
	}

	[Fact]
	public void Test_ShamblingHorror_KillsTarget_BanishesInsteadOfDiscard()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "shambling_horror",
			Name = "Shambling Horror",
			Type = CardType.Character,
			Level = 3,
			ArmorClass = 10,
			BaseHitPoints = 5,
			RulesText = "Characters killed by Shambling Horror are banished instead of being sent to their owner's discard pile."
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "victim",
			Name = "Defender",
			Type = CardType.Character,
			Level = 1,
			BaseHitPoints = 1,
			ArmorClass = 5
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition.DefinitionId] = cardDefinition,
				[cardDefinition2.DefinitionId] = cardDefinition2
			}
		};
		CardInstance p0Card = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "shambling_horror",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 5
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "victim",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 1
		};
		GameState gameState = CreateTwoPlayerState(p0Card, p1Card);
		PerformStrikeEffect performStrikeEffect = new PerformStrikeEffect(5);
		EffectContext context = new EffectContext
		{
			SourceInstanceId = 1,
			TargetInstanceId = 2,
			State = gameState,
			CardDb = cardDb,
			RollDice = (int n, int s) => 20
		};
		List<GameEvent> source = performStrikeEffect.Evaluate(context).ToList();
		DealDamageEvent dealDamageEvent = source.OfType<DealDamageEvent>().First();
		Assert.True(dealDamageEvent.BanishOnDeath);
		dealDamageEvent.ApplyToState(gameState);
		Assert.Equal(0, gameState.Players[1].Ranks[1].First((CardInstance c) => c.InstanceId == 2).CurrentHitPoints);
		DeathCheckHandler deathCheckHandler = new DeathCheckHandler();
		List<GameEvent> list = deathCheckHandler.AfterResolve(dealDamageEvent, gameState).ToList();
		Assert.Single(list);
		CharacterDefeatedEvent characterDefeatedEvent = Assert.IsType<CharacterDefeatedEvent>(list[0]);
		Assert.True(characterDefeatedEvent.BanishInsteadOfDiscard);
		characterDefeatedEvent.ApplyToState(gameState);
		Assert.Empty(gameState.Players[1].DiscardPile);
		Assert.Single(gameState.Players[1].BanishPile);
		Assert.Equal(2, gameState.Players[1].BanishPile[0].InstanceId);
		Assert.Equal(CardZone.Banished, gameState.Players[1].BanishPile[0].Zone);
	}

	[Fact]
	public void Test_GraveyardLibrarian_BanishClause()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "graveyard_librarian",
			Name = "Graveyard Librarian",
			Type = CardType.Character,
			Level = 2,
			RulesText = "Spend Order: Perform a +3 ranged strike. If the strike would kill the target, banish it instead."
		};
		List<AbilityDefinition> list = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name);
		Assert.Single(list);
		PerformStrikeEffect performStrikeEffect = Assert.IsType<PerformStrikeEffect>(list[0].Effects[0]);
		Assert.True(performStrikeEffect.BanishOnKill);
	}

	[Fact]
	public void Test_PackTactics_BothTargetsStrike()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "pack_tactics",
			Name = "Pack Tactics",
			Type = CardType.Action,
			RulesText = "Spend Order: Your two target characters attack as normal."
		};
		List<AbilityDefinition> list = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name);
		Assert.Single(list);
		Assert.IsType<PackTacticsEffect>(list[0].Effects[0]);
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "a1",
			Name = "Ally 1",
			Type = CardType.Character,
			Level = 1,
			BaseHitPoints = 3
		};
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "a2",
			Name = "Ally 2",
			Type = CardType.Character,
			Level = 1,
			BaseHitPoints = 3
		};
		CardDefinition cardDefinition4 = new CardDefinition
		{
			DefinitionId = "e1",
			Name = "Enemy 1",
			Type = CardType.Character,
			Level = 1,
			BaseHitPoints = 3
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3,
				[cardDefinition4.DefinitionId] = cardDefinition4
			}
		};
		CardInstance p0Card = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "a1",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 3
		};
		CardInstance item = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "a2",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 3
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "e1",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 3
		};
		GameState gameState = CreateTwoPlayerState(p0Card, p1Card);
		gameState.Players[0].Ranks[1].Add(item);
		PackTacticsEffect packTacticsEffect = new PackTacticsEffect();
		EffectContext context = new EffectContext
		{
			SourceInstanceId = 1,
			TargetInstanceId = 1,
			State = gameState,
			CardDb = cardDb,
			RollDice = (int n, int s) => 15
		};
		EffectContext context2 = new EffectContext
		{
			SourceInstanceId = 1,
			TargetInstanceId = 2,
			State = gameState,
			CardDb = cardDb,
			RollDice = (int n, int s) => 15
		};
		List<GameEvent> source = packTacticsEffect.Evaluate(context).ToList();
		List<GameEvent> source2 = packTacticsEffect.Evaluate(context2).ToList();
		Assert.Single(source.OfType<DealDamageEvent>());
		Assert.Single(source2.OfType<DealDamageEvent>());
	}

	[Fact]
	public void Test_AnotherTavernBrawl_Sequence()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "another_tavern_brawl",
			Name = "Another Tavern Brawl",
			Type = CardType.Action,
			RulesText = "Order: Target opposing player chooses one of their characters to perform a melee strike against your target character. Your target character then performs a melee strike against that enemy character."
		};
		List<AbilityDefinition> list = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name);
		Assert.Single(list);
		Assert.IsType<AnotherTavernBrawlEffect>(list[0].Effects[0]);
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "f1",
			Name = "Fighter 1",
			Type = CardType.Character,
			Level = 1,
			BaseHitPoints = 3
		};
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "e1",
			Name = "Enemy 1",
			Type = CardType.Character,
			Level = 1,
			BaseHitPoints = 3
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3
			}
		};
		CardInstance p0Card = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "f1",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 3
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "e1",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 3
		};
		GameState state = CreateTwoPlayerState(p0Card, p1Card);
		EffectContext context = new EffectContext
		{
			SourceInstanceId = 1,
			TargetInstanceId = 1,
			State = state,
			CardDb = cardDb,
			RollDice = (int n, int s) => 15
		};
		AnotherTavernBrawlEffect anotherTavernBrawlEffect = new AnotherTavernBrawlEffect();
		List<GameEvent> source = anotherTavernBrawlEffect.Evaluate(context).ToList();
		Assert.Equal(2, source.OfType<DealDamageEvent>().Count());
	}

	[Fact]
	public void Test_Flamelance_UnpreventableWounds()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "flamelance",
			Name = "Flamelance",
			Type = CardType.Action,
			RulesText = "Spend Order: Perform a +6 ranged strike. Wounds from this strike may not be prevented or reduced."
		};
		List<AbilityDefinition> list = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name);
		Assert.Single(list);
		PerformStrikeEffect performStrikeEffect = Assert.IsType<PerformStrikeEffect>(list[0].Effects[0]);
		Assert.True(performStrikeEffect.CannotReduceWounds);
	}

	[Fact]
	public void Test_GriffonOfAndover_SplitSecond()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "griffon_of_andover",
			Name = "Griffon of Andover",
			Type = CardType.Item,
			RulesText = "Spend React: Before this item's bearer would be stunned or killed, banish Griffon of Andover instead. Other players cannot react to this React."
		};
		List<AbilityDefinition> list = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name);
		Assert.Single(list);
		Assert.True(list[0].SplitSecond);
	}

	[Fact]
	public void Test_MeetAtTheInn_BothPlayersDraw()
	{
		CardInstance p0Card = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "c1",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "c2",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board
		};
		GameState state = CreateTwoPlayerState(p0Card, p1Card);
		EffectContext context = new EffectContext
		{
			SourceInstanceId = 1,
			State = state,
			CardDb = new CardDatabase()
		};
		MeetAtTheInnEffect meetAtTheInnEffect = new MeetAtTheInnEffect();
		List<GameEvent> source = meetAtTheInnEffect.Evaluate(context).ToList();
		List<DrawCardEvent> list = source.OfType<DrawCardEvent>().ToList();
		Assert.Equal(3, list.Count);
		Assert.Equal(2, list.Count((DrawCardEvent d) => d.PlayerIndex == 0));
		Assert.Equal(1, list.Count((DrawCardEvent d) => d.PlayerIndex == 1));
	}

	[Fact]
	public void Test_PunitiveStrike_ParsedSeparateAbilities()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "punitive_strike",
			Name = "Punitive Strike",
			Type = CardType.Action,
			RulesText = "Order: Perform a melee strike that may only target a stunned character.\\nReact: Before your character performs a melee strike: The strike gets +3."
		};
		List<AbilityDefinition> list = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name);
		Assert.Equal(2, list.Count);
		Assert.False(list[0].IsReact);
		Assert.True(list[1].IsReact);
	}
}
