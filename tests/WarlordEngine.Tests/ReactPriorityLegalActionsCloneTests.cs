using System;
using System.Collections.Frozen;
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

public class ReactPriorityLegalActionsCloneTests
{
	private static CardDatabase CreateTestDatabase()
	{
		List<CardDefinition> source = new List<CardDefinition>
		{
			new CardDefinition
			{
				DefinitionId = "warlord-p0",
				Name = "Lord Gareth",
				Type = CardType.Character,
				Faction = "Deverenian",
				IsWarlord = true,
				Level = 4,
				Skill = 6,
				ArmorClass = 16,
				BaseHitPoints = 4,
				Attack = new AttackValue
				{
					Strikes = new _003C_003Ez__ReadOnlySingleElementList<StrikeDescriptor>(new StrikeDescriptor
					{
						Modifier = 6,
						IsRanged = false
					})
				}
			},
			new CardDefinition
			{
				DefinitionId = "warlord-p1",
				Name = "King Xod",
				Type = CardType.Character,
				Faction = "Dwarf",
				IsWarlord = true,
				Level = 4,
				Skill = 5,
				ArmorClass = 17,
				BaseHitPoints = 4,
				Attack = new AttackValue
				{
					Strikes = new _003C_003Ez__ReadOnlySingleElementList<StrikeDescriptor>(new StrikeDescriptor
					{
						Modifier = 5,
						IsRanged = false
					})
				}
			},
			new CardDefinition
			{
				DefinitionId = "fighter-melee",
				Name = "Frontline Fighter",
				Type = CardType.Character,
				Faction = "Deverenian",
				Level = 1,
				Skill = 2,
				ArmorClass = 13,
				BaseHitPoints = 2,
				Attack = new AttackValue
				{
					Strikes = new _003C_003Ez__ReadOnlySingleElementList<StrikeDescriptor>(new StrikeDescriptor
					{
						Modifier = 3,
						IsRanged = false
					})
				}
			},
			new CardDefinition
			{
				DefinitionId = "archer-ranged",
				Name = "Longbow Archer",
				Type = CardType.Character,
				Faction = "Deverenian",
				Level = 2,
				Skill = 3,
				ArmorClass = 12,
				BaseHitPoints = 2,
				Attack = new AttackValue
				{
					Strikes = new _003C_003Ez__ReadOnlySingleElementList<StrikeDescriptor>(new StrikeDescriptor
					{
						Modifier = 4,
						IsRanged = true
					})
				},
				Abilities = new _003C_003Ez__ReadOnlySingleElementList<AbilityDefinition>(new AbilityDefinition
				{
					Trigger = AbilityTrigger.SpendOrder,
					Description = "Spend Order: Draw 1 card.",
					Effects = new _003C_003Ez__ReadOnlySingleElementList<IEffect>(new DrawCardEffect(1))
				})
			},
			new CardDefinition
			{
				DefinitionId = "defender-react",
				Name = "Shield Guardian",
				Type = CardType.Character,
				Faction = "Dwarf",
				Level = 1,
				Skill = 2,
				ArmorClass = 14,
				BaseHitPoints = 3,
				Attack = new AttackValue
				{
					Strikes = new _003C_003Ez__ReadOnlySingleElementList<StrikeDescriptor>(new StrikeDescriptor
					{
						Modifier = 2,
						IsRanged = false
					})
				},
				Abilities = new _003C_003Ez__ReadOnlySingleElementList<AbilityDefinition>(new AbilityDefinition
				{
					Trigger = AbilityTrigger.SpendReact,
					Description = "Spend React: Deal 1 damage to target character.",
					Effects = new _003C_003Ez__ReadOnlySingleElementList<IEffect>(new DealDamageEffect(1, "physical"))
				})
			},
			new CardDefinition
			{
				DefinitionId = "item-sword",
				Name = "Iron Broadsword",
				Type = CardType.Item,
				Faction = "Mercenary",
				Level = 1
			},
			new CardDefinition
			{
				DefinitionId = "action-order-bolt",
				Name = "Arcane Bolt",
				Type = CardType.Action,
				Faction = "Deverenian",
				Level = 1,
				Abilities = new _003C_003Ez__ReadOnlySingleElementList<AbilityDefinition>(new AbilityDefinition
				{
					Trigger = AbilityTrigger.Order,
					Description = "Order: Deal 1 damage to target character.",
					Effects = new _003C_003Ez__ReadOnlySingleElementList<IEffect>(new DealDamageEffect(1, "magic"))
				})
			},
			new CardDefinition
			{
				DefinitionId = "action-react-counter",
				Name = "Riposte Strike",
				Type = CardType.Action,
				Faction = "Dwarf",
				Level = 1,
				Abilities = new _003C_003Ez__ReadOnlySingleElementList<AbilityDefinition>(new AbilityDefinition
				{
					Trigger = AbilityTrigger.React,
					Description = "React: Deal 1 damage to target character.",
					Effects = new _003C_003Ez__ReadOnlySingleElementList<IEffect>(new DealDamageEffect(1, "physical"))
				})
			}
		};
		return new CardDatabase
		{
			Definitions = source.ToFrozenDictionary((CardDefinition c) => c.DefinitionId)
		};
	}

	[Fact]
	public void Attack_WithAutoPassDisabled_PausesUntilBothPlayersPass()
	{
		CardDatabase cardDb = CreateTestDatabase();
		GameState gameState = new GameState
		{
			CurrentPhase = TurnPhase.Main,
			ActivePlayerIndex = 0,
			AutoPassWhenNoReactsAvailable = false
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
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "fighter-melee",
			CurrentHitPoints = 2,
			CurrentArmorClass = 13,
			CurrentSkill = 2,
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
			InstanceId = 2,
			DefinitionId = "fighter-melee",
			CurrentHitPoints = 5,
			CurrentArmorClass = 13,
			CurrentSkill = 2,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		int diceRolls = 0;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb, (int count, int sides) =>
		{
			diceRolls++;
			return 15;
		});
		ActionResult actionResult = gameEngine.Execute(new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		});
		Assert.True(actionResult.IsSuccess);
		Assert.True(gameState2.Priority.IsReactWindowOpen);
		Assert.Equal(0, gameState2.Priority.ConsecutivePasses);
		Assert.Equal(1, gameState2.Priority.ActivePlayerIndex);
		Assert.Equal(1, gameEngine.Resolver.PendingCount);
		Assert.Equal(0, diceRolls);
		Assert.Equal(5, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
		ActionResult actionResult2 = gameEngine.Execute(new PassAction
		{
			PlayerIndex = 1
		});
		Assert.True(actionResult2.IsSuccess);
		Assert.True(gameState2.Priority.IsReactWindowOpen);
		Assert.Equal(1, gameState2.Priority.ConsecutivePasses);
		Assert.Equal(0, gameState2.Priority.ActivePlayerIndex);
		Assert.Equal(0, diceRolls);
		Assert.Equal(5, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
		ActionResult actionResult3 = gameEngine.Execute(new PassAction
		{
			PlayerIndex = 0
		});
		Assert.True(actionResult3.IsSuccess);
		Assert.False(gameState2.Priority.IsReactWindowOpen);
		Assert.Equal(0, gameState2.ResolverCountOrPending(gameEngine));
		Assert.Equal(1, diceRolls);
		Assert.True(gameState2.Players[1].Ranks[1][0].CurrentHitPoints < 5);
	}

	[Fact]
	public void Attack_WhenReactIsPresent_PausesAndResolvesReactLifoBeforeAttackRoll()
	{
		CardDatabase cardDb = CreateTestDatabase();
		GameState gameState = new GameState
		{
			CurrentPhase = TurnPhase.Main,
			ActivePlayerIndex = 0,
			AutoPassWhenNoReactsAvailable = true
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
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "fighter-melee",
			CurrentHitPoints = 2,
			CurrentArmorClass = 13,
			CurrentSkill = 2,
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
			InstanceId = 2,
			DefinitionId = "defender-react",
			CurrentHitPoints = 5,
			CurrentArmorClass = 14,
			CurrentSkill = 2,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState state = gameState;
		List<string> resolutionLog = new List<string>();
		int attackerHpDuringAttackRoll = -1;
		GameEngine gameEngine = new GameEngine(state, cardDb, (int count, int sides) =>
		{
			attackerHpDuringAttackRoll = state.Players[0].Ranks[1][0].CurrentHitPoints;
			resolutionLog.Add($"AttackRoll(AttackerHP={attackerHpDuringAttackRoll})");
			return 15;
		});
		ActionResult actionResult = gameEngine.Execute(new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		});
		Assert.True(actionResult.IsSuccess);
		Assert.True(state.Priority.IsReactWindowOpen);
		Assert.Empty(resolutionLog);
		Assert.Equal(2, state.Players[0].Ranks[1][0].CurrentHitPoints);
		ActionResult actionResult2 = gameEngine.Execute(new UseAbilityAction
		{
			PlayerIndex = 1,
			SourceInstanceId = 2,
			AbilityIndex = 0,
			TargetInstanceIds = new int[1] { 1 }
		});
		Assert.True(actionResult2.IsSuccess);
		Assert.True(state.Priority.IsReactWindowOpen);
		Assert.Equal(0, state.Priority.ConsecutivePasses);
		Assert.Equal(2, gameEngine.Resolver.PendingCount);
		Assert.Equal(2, state.Players[0].Ranks[1][0].CurrentHitPoints);
		Assert.True(gameEngine.Execute(new PassAction
		{
			PlayerIndex = 0
		}).IsSuccess);
		Assert.True(state.Priority.IsReactWindowOpen);
		Assert.Empty(resolutionLog);
		Assert.True(gameEngine.Execute(new PassAction
		{
			PlayerIndex = 1
		}).IsSuccess);
		Assert.False(state.Priority.IsReactWindowOpen);
		Assert.Equal(0, gameEngine.Resolver.PendingCount);
		Assert.Equal(1, attackerHpDuringAttackRoll);
		Assert.Single(resolutionLog);
		Assert.Equal(1, state.Players[0].Ranks[1][0].CurrentHitPoints);
		Assert.Equal(4, state.Players[1].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void GetLegalActions_ReturnsValidDeploysAttacksOrdersAndPass_AndAllSucceedInExecuteAction()
	{
		CardDatabase cardDb = CreateTestDatabase();
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
			PlayerIndex = 0,
			WarlordInstanceId = 10
		};
		int num2 = 3;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = new CardInstance
		{
			InstanceId = 100,
			DefinitionId = "fighter-melee",
			CurrentHitPoints = 2,
			CurrentArmorClass = 13,
			CurrentSkill = 2,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		span2[1] = new CardInstance
		{
			InstanceId = 101,
			DefinitionId = "item-sword",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		span2[2] = new CardInstance
		{
			InstanceId = 102,
			DefinitionId = "action-order-bolt",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		playerState.Hand = list2;
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = new CardInstance
		{
			InstanceId = 200,
			DefinitionId = "fighter-melee",
			CurrentHitPoints = 2,
			CurrentArmorClass = 13,
			CurrentSkill = 2,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Deck,
			OwnerPlayerIndex = 0
		};
		playerState.Deck = list3;
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 2;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		Span<CardInstance> span3 = CollectionsMarshal.AsSpan(list4);
		span3[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "fighter-melee",
			CurrentHitPoints = 2,
			CurrentArmorClass = 13,
			CurrentSkill = 2,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		span3[1] = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "fighter-melee",
			CurrentHitPoints = 2,
			CurrentArmorClass = 13,
			CurrentSkill = 2,
			ReadyState = ReadyState.Spent,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		sortedDictionary[1] = list4;
		num2 = 1;
		List<CardInstance> list5 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list5, num2);
		CollectionsMarshal.AsSpan(list5)[0] = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "archer-ranged",
			CurrentHitPoints = 2,
			CurrentArmorClass = 12,
			CurrentSkill = 3,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 2
		};
		sortedDictionary[2] = list5;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			WarlordInstanceId = 20
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list6 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list6, num2);
		CollectionsMarshal.AsSpan(list6)[0] = new CardInstance
		{
			InstanceId = 11,
			DefinitionId = "fighter-melee",
			CurrentHitPoints = 2,
			CurrentArmorClass = 13,
			CurrentSkill = 2,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		sortedDictionary2[1] = list6;
		num2 = 1;
		List<CardInstance> list7 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list7, num2);
		CollectionsMarshal.AsSpan(list7)[0] = new CardInstance
		{
			InstanceId = 20,
			DefinitionId = "warlord-p1",
			CurrentHitPoints = 4,
			CurrentArmorClass = 17,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 2
		};
		sortedDictionary2[2] = list7;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		Assert.Empty(gameEngine.GetLegalActions(gameState2, 1));
		IReadOnlyList<IPlayerAction> legalActions = gameEngine.GetLegalActions(gameState2, 0);
		Assert.NotEmpty(legalActions);
		List<int?> collection = (from a in legalActions.OfType<PlayCardAction>()
			where a.CardInstanceId == 100
			select a.TargetRank).ToList();
		Assert.Contains(1, collection);
		Assert.DoesNotContain(2, collection);
		Assert.DoesNotContain(3, collection);
		Assert.DoesNotContain(4, collection);
		List<AttackAction> collection2 = legalActions.OfType<AttackAction>().ToList();
		Assert.Contains(collection2, (AttackAction a) => a.AttackerInstanceId == 1 && a.TargetInstanceId == 11);
		Assert.DoesNotContain(collection2, (AttackAction a) => a.AttackerInstanceId == 1 && a.TargetInstanceId == 20);
		Assert.DoesNotContain(collection2, (AttackAction a) => a.AttackerInstanceId == 2);
		Assert.Contains(collection2, (AttackAction a) => a.AttackerInstanceId == 10 && a.TargetInstanceId == 11);
		Assert.DoesNotContain(collection2, (AttackAction a) => a.AttackerInstanceId == 10 && a.TargetInstanceId == 20);
		Assert.Contains(legalActions.OfType<UseAbilityAction>(), (UseAbilityAction a) => a.SourceInstanceId == 10 && a.AbilityIndex == 0);
		Assert.Contains(legalActions.OfType<PassAction>(), (PassAction a) => a.PlayerIndex == 0);
		foreach (IPlayerAction item in legalActions)
		{
			GameState state = gameState2.DeepClone();
			GameEngine gameEngine2 = new GameEngine(state, cardDb);
			ActionResult actionResult = gameEngine2.ExecuteAction(item);
			Assert.True(actionResult.IsSuccess, $"Legal action {item} failed ExecuteAction: {string.Join(", ", actionResult.Errors)}");
		}
	}

	[Fact]
	public void GetLegalActions_InReactWindow_ReturnsReactAbilitiesCardsAndPassForPriorityPlayer()
	{
		CardDatabase cardDb = CreateTestDatabase();
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
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "fighter-melee",
			CurrentHitPoints = 3,
			CurrentArmorClass = 13,
			CurrentSkill = 2,
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
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = new CardInstance
		{
			InstanceId = 50,
			DefinitionId = "action-react-counter",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 1
		};
		playerState2.Hand = list3;
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		CollectionsMarshal.AsSpan(list4)[0] = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "defender-react",
			CurrentHitPoints = 4,
			CurrentArmorClass = 14,
			CurrentSkill = 2,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		sortedDictionary2[1] = list4;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		Assert.True(gameEngine.Execute(new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		}).IsSuccess);
		Assert.True(gameState2.Priority.IsReactWindowOpen);
		Assert.Equal(1, gameState2.Priority.ActivePlayerIndex);
		Assert.Empty(gameEngine.GetLegalActions(gameState2, 0));
		IReadOnlyList<IPlayerAction> legalActions = gameEngine.GetLegalActions(gameState2, 1);
		Assert.Contains(legalActions.OfType<UseAbilityAction>(), (UseAbilityAction a) => a.SourceInstanceId == 2 && a.AbilityIndex == 0);
		Assert.Contains(legalActions.OfType<PlayCardAction>(), (PlayCardAction a) => a.CardInstanceId == 50);
		Assert.Contains(legalActions.OfType<PassAction>(), (PassAction a) => a.PlayerIndex == 1);
		foreach (IPlayerAction item in legalActions)
		{
			GameState state = gameState2.DeepClone();
			GameEngine gameEngine2 = new GameEngine(state, cardDb);
			ActionResult actionResult = gameEngine2.ExecuteAction(item);
			Assert.True(actionResult.IsSuccess, $"React legal action {item} failed ExecuteAction: {string.Join(", ", actionResult.Errors)}");
		}
	}

	[Fact]
	public void DeepClone_ProducesCompletelyIndependentState_AndRandomizesOpponentHandForObserver()
	{
		GameState gameState = new GameState
		{
			TurnNumber = 3,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			NextInstanceId = 500,
			AutoPassWhenNoReactsAvailable = false
		};
		int num = 1;
		List<GameEvent> list = new List<GameEvent>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = new DeclareAttackEvent
		{
			AttackerInstanceId = 1,
			DefenderInstanceId = 2
		};
		gameState.PendingEvents = list;
		num = 2;
		List<PlayerState> list2 = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list2);
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0,
			DisplayName = "Observer",
			WarlordInstanceId = 1
		};
		int num2 = 2;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list3);
		span2[0] = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "c10",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		span2[1] = new CardInstance
		{
			InstanceId = 11,
			DefinitionId = "c11",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		playerState.Hand = list3;
		num2 = 1;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		CollectionsMarshal.AsSpan(list4)[0] = new CardInstance
		{
			InstanceId = 12,
			DefinitionId = "c12",
			Zone = CardZone.Deck,
			OwnerPlayerIndex = 0
		};
		playerState.Deck = list4;
		num2 = 1;
		List<CardInstance> list5 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list5, num2);
		CollectionsMarshal.AsSpan(list5)[0] = new CardInstance
		{
			InstanceId = 13,
			DefinitionId = "c13",
			Zone = CardZone.Discard,
			OwnerPlayerIndex = 0
		};
		playerState.DiscardPile = list5;
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list6 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list6, num2);
		ref CardInstance reference2 = ref CollectionsMarshal.AsSpan(list6)[0];
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warlord-p0",
			CurrentHitPoints = 4,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		int num3 = 1;
		List<int> list7 = new List<int>(num3);
		CollectionsMarshal.SetCount(list7, num3);
		CollectionsMarshal.AsSpan(list7)[0] = 99;
		cardInstance.AttachedItemIds = list7;
		reference2 = cardInstance;
		sortedDictionary[1] = list6;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference3 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Opponent",
			WarlordInstanceId = 2
		};
		num2 = 3;
		List<CardInstance> list8 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list8, num2);
		Span<CardInstance> span3 = CollectionsMarshal.AsSpan(list8);
		span3[0] = new CardInstance
		{
			InstanceId = 201,
			DefinitionId = "opp-h1",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 1
		};
		span3[1] = new CardInstance
		{
			InstanceId = 202,
			DefinitionId = "opp-h2",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 1
		};
		span3[2] = new CardInstance
		{
			InstanceId = 203,
			DefinitionId = "opp-h3",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 1
		};
		playerState2.Hand = list8;
		num2 = 5;
		List<CardInstance> list9 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list9, num2);
		Span<CardInstance> span4 = CollectionsMarshal.AsSpan(list9);
		span4[0] = new CardInstance
		{
			InstanceId = 301,
			DefinitionId = "opp-d1",
			Zone = CardZone.Deck,
			OwnerPlayerIndex = 1
		};
		span4[1] = new CardInstance
		{
			InstanceId = 302,
			DefinitionId = "opp-d2",
			Zone = CardZone.Deck,
			OwnerPlayerIndex = 1
		};
		span4[2] = new CardInstance
		{
			InstanceId = 303,
			DefinitionId = "opp-d3",
			Zone = CardZone.Deck,
			OwnerPlayerIndex = 1
		};
		span4[3] = new CardInstance
		{
			InstanceId = 304,
			DefinitionId = "opp-d4",
			Zone = CardZone.Deck,
			OwnerPlayerIndex = 1
		};
		span4[4] = new CardInstance
		{
			InstanceId = 305,
			DefinitionId = "opp-d5",
			Zone = CardZone.Deck,
			OwnerPlayerIndex = 1
		};
		playerState2.Deck = list9;
		num2 = 1;
		List<CardInstance> list10 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list10, num2);
		CollectionsMarshal.AsSpan(list10)[0] = new CardInstance
		{
			InstanceId = 401,
			DefinitionId = "opp-disc",
			Zone = CardZone.Discard,
			OwnerPlayerIndex = 1
		};
		playerState2.DiscardPile = list10;
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list11 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list11, num2);
		CollectionsMarshal.AsSpan(list11)[0] = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warlord-p1",
			CurrentHitPoints = 4,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		sortedDictionary2[1] = list11;
		playerState2.Ranks = sortedDictionary2;
		reference3 = playerState2;
		gameState.Players = list2;
		GameState gameState2 = gameState;
		gameState2.Priority.IsReactWindowOpen = true;
		gameState2.Priority.ConsecutivePasses = 1;
		gameState2.Priority.ActivePlayerIndex = 1;
		GameState gameState3 = gameState2.DeepClone(0);
		num = 2;
		List<int> list12 = new List<int>(num);
		CollectionsMarshal.SetCount(list12, num);
		Span<int> span5 = CollectionsMarshal.AsSpan(list12);
		span5[0] = 10;
		span5[1] = 11;
		Assert.Equal(list12, gameState3.Players[0].Hand.Select((CardInstance c) => c.InstanceId).ToList());
		Assert.Equal(3, gameState3.Players[1].Hand.Count);
		Assert.Equal(5, gameState3.Players[1].Deck.Count);
		Assert.All(gameState3.Players[1].Hand, (CardInstance c) =>
		{
			Assert.Equal(CardZone.Hand, c.Zone);
		});
		Assert.All(gameState3.Players[1].Deck, (CardInstance c) =>
		{
			Assert.Equal(CardZone.Deck, c.Zone);
		});
		HashSet<int> expected = new HashSet<int> { 201, 202, 203, 301, 302, 303, 304, 305 };
		HashSet<int> actual = gameState3.Players[1].Hand.Select((CardInstance c) => c.InstanceId).Concat(gameState3.Players[1].Deck.Select((CardInstance c) => c.InstanceId)).ToHashSet();
		Assert.Equal(expected, actual);
		List<int> expected2 = gameState2.Players[1].Hand.Select((CardInstance c) => c.InstanceId).ToList();
		List<int> actual2 = gameState3.Players[1].Hand.Select((CardInstance c) => c.InstanceId).ToList();
		Assert.NotEqual(expected2, actual2);
		gameState3.TurnNumber = 99;
		gameState3.Priority.IsReactWindowOpen = false;
		gameState3.Priority.ConsecutivePasses = 0;
		gameState3.PendingEvents.Clear();
		gameState3.Players[0].Hand.Clear();
		gameState3.Players[0].Deck.Clear();
		gameState3.Players[0].DiscardPile.Clear();
		gameState3.Players[0].Ranks[1][0].AttachedItemIds.Add(777);
		gameState3.Players[0].Ranks[1].Clear();
		gameState3.Players[1].Hand.Clear();
		Assert.Equal(3, gameState2.TurnNumber);
		Assert.True(gameState2.Priority.IsReactWindowOpen);
		Assert.Equal(1, gameState2.Priority.ConsecutivePasses);
		Assert.Single(gameState2.PendingEvents);
		Assert.Equal(2, gameState2.Players[0].Hand.Count);
		Assert.Single(gameState2.Players[0].Deck);
		Assert.Single(gameState2.Players[0].DiscardPile);
		Assert.Single(gameState2.Players[0].Ranks[1]);
		num = 1;
		List<int> list13 = new List<int>(num);
		CollectionsMarshal.SetCount(list13, num);
		CollectionsMarshal.AsSpan(list13)[0] = 99;
		Assert.Equal(list13, gameState2.Players[0].Ranks[1][0].AttachedItemIds);
		num = 3;
		List<int> list14 = new List<int>(num);
		CollectionsMarshal.SetCount(list14, num);
		Span<int> span6 = CollectionsMarshal.AsSpan(list14);
		span6[0] = 201;
		span6[1] = 202;
		span6[2] = 203;
		Assert.Equal(list14, gameState2.Players[1].Hand.Select((CardInstance c) => c.InstanceId).ToList());
	}
}
