using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordEngine.AI;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Events;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class CoreTabletopRulesBugsTests
{
	[Fact]
	public void Rule1_DatabaseLoader_Loads_Correct_Stats_For_CrownlandStrategist_BrineFiend_TunnelWorm()
	{
		string text = Path.Combine(AppContext.BaseDirectory, "DataParser", "cards.json");
		if (!File.Exists(text))
		{
			text = Path.Combine(Directory.GetCurrentDirectory(), "DataParser", "cards.json");
		}
		CardDatabase cardDatabase = DatabaseLoader.LoadFromFile(text);
		Assert.True(cardDatabase.Definitions.TryGetValue("crownland_strategist", out CardDefinition value));
		Assert.Equal(14, value.ArmorClass);
		Assert.Equal(2, value.Level);
		Assert.True(cardDatabase.Definitions.TryGetValue("brine_fiend", out CardDefinition value2));
		Assert.Equal(8, value2.ArmorClass);
		Assert.Equal(2, value2.BaseHitPoints);
		Assert.Equal(0, value2.Attack?.Strikes[0].Modifier);
		Assert.True(cardDatabase.Definitions.TryGetValue("tunnel_worm", out CardDefinition value3));
		Assert.Equal(10, value3.ArmorClass);
		Assert.Equal(1, value3.BaseHitPoints);
		Assert.Equal(3, value3.Attack?.Strikes[0].Modifier);
	}

	[Fact]
	public void Rule1_StrictWarlordCombat_StandardStrike_Deals_One_Wound_And_Nat20_Does_Not_Deal_Two_Wounds()
	{
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["tunnel_worm"] = new CardDefinition
				{
					DefinitionId = "tunnel_worm",
					Name = "Tunnel Worm",
					Type = CardType.Character,
					Class = CharacterClass.Cleric,
					Level = 1,
					ArmorClass = 10,
					BaseHitPoints = 1,
					Attack = new AttackValue
					{
						Strikes = new List<StrikeDescriptor> { new StrikeDescriptor
						{
							Modifier = 3,
							IsRanged = false
						} },
						RawText = "+3"
					}
				},
				["crownland_strategist"] = new CardDefinition
				{
					DefinitionId = "crownland_strategist",
					Name = "Crownland Strategist",
					Type = CardType.Character,
					Class = CharacterClass.Rogue,
					Level = 2,
					ArmorClass = 14,
					BaseHitPoints = 5,
					Attack = new AttackValue
					{
						Strikes = new List<StrikeDescriptor> { new StrikeDescriptor
						{
							Modifier = 2,
							IsRanged = false
						} },
						RawText = "+2"
					}
				}
			}
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
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
			DefinitionId = "tunnel_worm",
			CurrentArmorClass = 10,
			CurrentHitPoints = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
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
			DefinitionId = "crownland_strategist",
			CurrentArmorClass = 14,
			CurrentHitPoints = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		AttackResolutionDetails capturedDetails1 = null;
		AttackResolutionDetails capturedDetails2 = null;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb, (int count, int sides) => 12);
		gameEngine.OnAttackResolved = (AttackResolutionDetails d) =>
		{
			capturedDetails1 = d;
		};
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.NotNull(capturedDetails1);
		Assert.Equal(12, capturedDetails1.Roll);
		Assert.Equal(3, capturedDetails1.StrikeBonus);
		Assert.Equal(15, capturedDetails1.TotalRoll);
		Assert.Equal(14, capturedDetails1.DefenderArmorClass);
		Assert.True(capturedDetails1.IsHit);
		Assert.False(capturedDetails1.IsCritical);
		Assert.Equal(1, capturedDetails1.DamageDealt);
		Assert.Equal(4, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
		gameState2.Players[1].Ranks[1][0] = gameState2.Players[1].Ranks[1][0]with
		{
			CurrentArmorClass = 30
		};
		gameState2.Players[0].Ranks[1][0] = gameState2.Players[0].Ranks[1][0]with
		{
			ReadyState = ReadyState.Ready
		};
		gameState2.ActivePlayerIndex = 0;
		gameEngine.SetDiceRoller((int count, int sides) => 20);
		gameEngine.OnAttackResolved = (AttackResolutionDetails d) =>
		{
			capturedDetails2 = d;
		};
		AttackAction action2 = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult2 = gameEngine.Execute(action2);
		Assert.True(actionResult2.IsSuccess);
		Assert.NotNull(capturedDetails2);
		Assert.Equal(20, capturedDetails2.Roll);
		Assert.True(capturedDetails2.IsCritical);
		Assert.True(capturedDetails2.IsHit);
		Assert.Equal(1, capturedDetails2.DamageDealt);
		Assert.Equal(3, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
		Assert.Equal(1, capturedDetails1.DamageDealt);
		Assert.Equal(1, capturedDetails2.DamageDealt);
		gameState2.Players[0].Ranks[1][0] = gameState2.Players[0].Ranks[1][0]with
		{
			ReadyState = ReadyState.Ready
		};
		gameState2.ActivePlayerIndex = 0;
		AttackResolutionDetails capturedDetails3 = null;
		gameEngine.SetDiceRoller((int count, int sides) => 1);
		gameEngine.OnAttackResolved = (AttackResolutionDetails d) =>
		{
			capturedDetails3 = d;
		};
		AttackAction action3 = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult3 = gameEngine.Execute(action3);
		Assert.True(actionResult3.IsSuccess);
		Assert.NotNull(capturedDetails3);
		Assert.Equal(1, capturedDetails3.Roll);
		Assert.True(capturedDetails3.IsCriticalFailure);
		Assert.False(capturedDetails3.IsHit);
		Assert.Equal(0, capturedDetails3.DamageDealt);
		Assert.Equal(3, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void Rule2_InherentRankMovement_Enumerates_Legal_MarchOrders_And_Spends_Character()
	{
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { ["hero"] = new CardDefinition
			{
				DefinitionId = "hero",
				Name = "Hero",
				Type = CardType.Character,
				Class = CharacterClass.Fighter,
				Level = 1,
				ArmorClass = 12,
				BaseHitPoints = 5
			} }
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main
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
			DefinitionId = "hero",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		span2[1] = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "hero",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary[1] = list2;
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "hero",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary[2] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		IReadOnlyList<IPlayerAction> legalActions = gameEngine.GetLegalActions(gameState2, 0);
		MarchAction marchAction = legalActions.OfType<MarchAction>().FirstOrDefault((MarchAction m) => m.CardInstanceId == 3 && m.DestinationRank == 1);
		Assert.NotNull(marchAction);
		ActionResult actionResult = gameEngine.Execute(marchAction);
		Assert.True(actionResult.IsSuccess);
		Assert.Contains(gameState2.Players[0].Ranks[1], (CardInstance c) => c.InstanceId == 3 && c.ReadyState == ReadyState.Spent);
		Assert.False(gameState2.Players[0].Ranks.ContainsKey(2));
		GameState gameState3 = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main
		};
		num = 2;
		List<PlayerState> list4 = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list4, num);
		Span<PlayerState> span3 = CollectionsMarshal.AsSpan(list4);
		ref PlayerState reference2 = ref span3[0];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 0
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list5 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list5, num2);
		CollectionsMarshal.AsSpan(list5)[0] = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "hero",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary2[1] = list5;
		num2 = 1;
		List<CardInstance> list6 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list6, num2);
		CollectionsMarshal.AsSpan(list6)[0] = new CardInstance
		{
			InstanceId = 11,
			DefinitionId = "hero",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary2[2] = list6;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		span3[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState3.Players = list4;
		GameState state = gameState3;
		GameEngine gameEngine2 = new GameEngine(state, cardDb);
		MarchAction action = new MarchAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			SourceRank = 1,
			DestinationRank = 2
		};
		ActionResult actionResult2 = gameEngine2.CanExecute(action);
		Assert.False(actionResult2.IsSuccess);
	}

	[Fact]
	public void Rule3_LimitedAbilities_TrackedOncePerTurn_And_ClearedInReadyPhase()
	{
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { ["adathan"] = new CardDefinition
			{
				DefinitionId = "adathan",
				Name = "Adathan, Celestial Messenger",
				Type = CardType.Character,
				Class = CharacterClass.Cleric,
				Level = 5,
				ArmorClass = 16,
				BaseHitPoints = 3,
				Abilities = new List<AbilityDefinition> { new AbilityDefinition
				{
					Trigger = AbilityTrigger.Order,
					IsLimited = true,
					Description = "Limited Order: Boost friendly characters",
					Effects = new List<IEffect> { new ModifyStatEffect("ATK", 2) } }
				}
			} }
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main
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
			DefinitionId = "adathan",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 1,
			AbilityIndex = 0,
			TargetInstanceIds = new int[1] { 1 }
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.Contains("1_0", gameState2.TurnState.UsedLimitedAbilityKeysThisTurn);
		Assert.Contains(0, gameState2.Players[0].Ranks[1][0].AbilitiesUsedThisTurn);
		gameState2.ActivePlayerIndex = 0;
		ActionResult actionResult2 = gameEngine.CanExecute(action);
		Assert.False(actionResult2.IsSuccess);
		Assert.Contains("This limited ability has already been used this turn.", actionResult2.Errors);
		IReadOnlyList<IPlayerAction> legalActions = gameEngine.GetLegalActions(gameState2, 0);
		Assert.DoesNotContain(legalActions.OfType<UseAbilityAction>(), (UseAbilityAction a) => a.SourceInstanceId == 1 && a.AbilityIndex == 0);
		gameEngine.AdvancePhase();
		gameEngine.AdvancePhase();
		gameEngine.AdvancePhase();
		Assert.Equal(2, gameState2.TurnNumber);
		Assert.Empty(gameState2.TurnState.UsedLimitedAbilityKeysThisTurn);
		Assert.Empty(gameState2.Players[0].Ranks[1][0].AbilitiesUsedThisTurn);
	}

	[Fact]
	public void Rule4_SkipReactWindow_When_Only_PassAction_Available()
	{
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["attacker"] = new CardDefinition
				{
					DefinitionId = "attacker",
					Name = "Attacker",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 10,
					BaseHitPoints = 5,
					Attack = new AttackValue
					{
						Strikes = new List<StrikeDescriptor> { new StrikeDescriptor
						{
							Modifier = 2,
							IsRanged = false
						} }
					}
				},
				["defender"] = new CardDefinition
				{
					DefinitionId = "defender",
					Name = "Defender",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 10,
					BaseHitPoints = 5
				}
			}
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
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
			DefinitionId = "attacker",
			CurrentHitPoints = 5,
			CurrentArmorClass = 10,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
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
			DefinitionId = "defender",
			CurrentHitPoints = 5,
			CurrentArmorClass = 10,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb, (int count, int sides) => 15);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.False(gameState2.Priority.IsReactWindowOpen);
		Assert.Equal(4, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void Rule5_Ai_Actively_Attacks_Instead_Of_Passing()
	{
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["ai_fighter"] = new CardDefinition
				{
					DefinitionId = "ai_fighter",
					Name = "AI Fighter",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 12,
					BaseHitPoints = 5,
					Attack = new AttackValue
					{
						Strikes = new List<StrikeDescriptor> { new StrikeDescriptor
						{
							Modifier = 5,
							IsRanged = false
						} }
					}
				},
				["enemy_target"] = new CardDefinition
				{
					DefinitionId = "enemy_target",
					Name = "Enemy Target",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 10,
					BaseHitPoints = 1
				}
			}
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
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
			DisplayName = "AI Player"
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "ai_fighter",
			CurrentArmorClass = 12,
			CurrentHitPoints = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Enemy Player",
			WarlordInstanceId = 2
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "enemy_target",
			CurrentArmorClass = 10,
			CurrentHitPoints = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState state = gameState;
		GameEngine engine = new GameEngine(state, cardDb, (int count, int sides) => 15);
		HeuristicAiAgent heuristicAiAgent = new HeuristicAiAgent();
		IPlayerAction playerAction = heuristicAiAgent.SelectAction(state, engine, 0);
		Assert.IsNotType<PassAction>(playerAction);
		bool condition = ((playerAction is AttackAction || playerAction is DeclareAttackAction) ? true : false);
		Assert.True(condition);
	}

	[Fact]
	public void Rule6_PlayedActions_And_DefeatedCharactersAndItems_Are_Placed_In_DiscardPile()
	{
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["strike_action"] = new CardDefinition
				{
					DefinitionId = "strike_action",
					Name = "Power Strike",
					Type = CardType.Action,
					Level = 1,
					Abilities = new List<AbilityDefinition> { new AbilityDefinition
					{
						Trigger = AbilityTrigger.Order,
						Effects = new List<IEffect> { new DealDamageEffect(2, "physical") } }
					}
				},
				["sword"] = new CardDefinition
				{
					DefinitionId = "sword",
					Name = "Broadsword",
					Type = CardType.Item,
					Level = 1
				},
				["warrior"] = new CardDefinition
				{
					DefinitionId = "warrior",
					Name = "Warrior",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 10,
					BaseHitPoints = 2
				}
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "sword",
			Zone = CardZone.Attached,
			AttachedToInstanceId = 2,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main
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
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "strike_action",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		playerState.Hand = list2;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		ref CardInstance reference3 = ref CollectionsMarshal.AsSpan(list3)[0];
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warrior",
			CurrentHitPoints = 2,
			CurrentArmorClass = 10,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		int num3 = 1;
		List<int> list4 = new List<int>(num3);
		CollectionsMarshal.SetCount(list4, num3);
		CollectionsMarshal.AsSpan(list4)[0] = 10;
		cardInstance2.AttachedItemIds = list4;
		reference3 = cardInstance2;
		sortedDictionary[1] = list3;
		playerState2.Ranks = sortedDictionary;
		num2 = 1;
		List<CardInstance> list5 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list5, num2);
		CollectionsMarshal.AsSpan(list5)[0] = cardInstance;
		playerState2.DiscardPile = list5;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 1,
			TargetCharacterId = 2
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.Contains(gameState2.Players[0].DiscardPile, (CardInstance c) => c.InstanceId == 1 && c.Zone == CardZone.Discard);
		Assert.Contains(gameState2.Players[1].DiscardPile, (CardInstance c) => c.InstanceId == 2 && c.Zone == CardZone.Discard);
		CardInstance cardInstance3 = gameState2.Players[1].DiscardPile.FirstOrDefault((CardInstance c) => c.InstanceId == 10);
		Assert.Equal(CardZone.Discard, cardInstance3.Zone);
		Assert.Null(cardInstance3.AttachedToInstanceId);
	}

	[Fact]
	public void TabletopRule1_RangedStrikeDistance_Rank2ToRank2_Rejected_While_Rank1ToRank2_And_Rank2ToRank1_Succeed()
	{
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["archer"] = new CardDefinition
				{
					DefinitionId = "archer",
					Name = "Elven Archer",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 2,
					ArmorClass = 12,
					BaseHitPoints = 3,
					Attack = new AttackValue
					{
						Strikes = [new StrikeDescriptor { Modifier = 2, IsRanged = true }],
						RawText = "+2 ranged"
					}
				},
				["target_dummy"] = new CardDefinition
				{
					DefinitionId = "target_dummy",
					Name = "Target Dummy",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 10,
					BaseHitPoints = 5,
					Attack = new AttackValue
					{
						Strikes = [new StrikeDescriptor { Modifier = 0, IsRanged = false }],
						RawText = "+0"
					}
				},
				["cunning_shot"] = new CardDefinition
				{
					DefinitionId = "cunning_shot",
					Name = "Cunning Shot",
					Type = CardType.Action,
					Class = CharacterClass.Fighter,
					Level = 1,
					RulesText = "Order: Perform a +4 ranged strike that does not inflict wounds. If the strike hits, spend the target and move them forward one rank.",
					Abilities = RulesParser.Parse("Order: Perform a +4 ranged strike that does not inflict wounds. If the strike hits, spend the target and move them forward one rank.", "Cunning Shot")
				}
			}
		};

		GameState state = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			AutoPassWhenNoReactsAvailable = true,
			Players =
			[
				new PlayerState
				{
					PlayerIndex = 0,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = [new CardInstance { InstanceId = 10, DefinitionId = "archer", CurrentArmorClass = 12, CurrentHitPoints = 3, ReadyState = ReadyState.Ready, Zone = CardZone.Board, OwnerPlayerIndex = 0, Rank = 1 }],
						[2] = [new CardInstance { InstanceId = 11, DefinitionId = "archer", CurrentArmorClass = 12, CurrentHitPoints = 3, ReadyState = ReadyState.Ready, Zone = CardZone.Board, OwnerPlayerIndex = 0, Rank = 2 }]
					},
					Hand =
					[
						new CardInstance { InstanceId = 100, DefinitionId = "cunning_shot", Zone = CardZone.Hand, OwnerPlayerIndex = 0 }
					]
				},
				new PlayerState
				{
					PlayerIndex = 1,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = [new CardInstance { InstanceId = 20, DefinitionId = "target_dummy", CurrentArmorClass = 10, CurrentHitPoints = 5, ReadyState = ReadyState.Ready, Zone = CardZone.Board, OwnerPlayerIndex = 1, Rank = 1 }],
						[2] = [new CardInstance { InstanceId = 21, DefinitionId = "target_dummy", CurrentArmorClass = 10, CurrentHitPoints = 5, ReadyState = ReadyState.Ready, Zone = CardZone.Board, OwnerPlayerIndex = 1, Rank = 2 }]
					}
				}
			]
		};

		GameEngine engine = new GameEngine(state, cardDb);

		// 1. BoardGeometry.IsRangedValid tests
		CardInstance p0R1 = state.Players[0].Ranks[1][0]; // ID 10
		CardInstance p0R2 = state.Players[0].Ranks[2][0]; // ID 11
		CardInstance p1R1 = state.Players[1].Ranks[1][0]; // ID 20
		CardInstance p1R2 = state.Players[1].Ranks[2][0]; // ID 21

		// Rank 1 -> Rank 1: dist = 1 + 1 - 1 = 1 <= 2 (Valid)
		Assert.True(BoardGeometry.IsRangedValid(p0R1, p1R1, state, 2));
		// Rank 1 -> Rank 2: dist = 1 + 2 - 1 = 2 <= 2 (Valid)
		Assert.True(BoardGeometry.IsRangedValid(p0R1, p1R2, state, 2));
		// Rank 2 -> Rank 1: dist = 2 + 1 - 1 = 2 <= 2 (Valid)
		Assert.True(BoardGeometry.IsRangedValid(p0R2, p1R1, state, 2));
		// Rank 2 -> Rank 2: dist = 2 + 2 - 1 = 3 > 2 (INVALID!)
		Assert.False(BoardGeometry.IsRangedValid(p0R2, p1R2, state, 2));

		// 2. AttackAction target validation:
		// Rank 2 -> Rank 1 attack succeeds
		AttackAction atkR2ToR1 = new AttackAction { PlayerIndex = 0, AttackerInstanceId = 11, TargetInstanceId = 20 };
		Assert.True(engine.CanExecute(atkR2ToR1, state).IsSuccess);

		// Rank 2 -> Rank 2 attack FAILS
		AttackAction atkR2ToR2 = new AttackAction { PlayerIndex = 0, AttackerInstanceId = 11, TargetInstanceId = 21 };
		ActionResult resR2ToR2 = engine.CanExecute(atkR2ToR2, state);
		Assert.False(resR2ToR2.IsSuccess);
		Assert.Contains(resR2ToR2.Errors, e => e.Contains("out of range") || e.Contains("reach up to 2 ranks away"));

		// Rank 1 -> Rank 2 attack succeeds
		AttackAction atkR1ToR2 = new AttackAction { PlayerIndex = 0, AttackerInstanceId = 10, TargetInstanceId = 21 };
		Assert.True(engine.CanExecute(atkR1ToR2, state).IsSuccess);

		// 3. PlayCardAction (Cunning Shot):
		// Rank 2 -> Rank 2 fails
		PlayCardAction playCunningShotR2ToR2 = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 100,
			PerformerInstanceId = 11, // Rank 2
			TargetCharacterId = 21 // Rank 2
		};
		ActionResult playResR2ToR2 = engine.CanExecute(playCunningShotR2ToR2, state);
		Assert.False(playResR2ToR2.IsSuccess);
		Assert.Contains(playResR2ToR2.Errors, e => e.Contains("out of range"));

		// Rank 2 -> Rank 1 succeeds
		PlayCardAction playCunningShotR2ToR1 = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 100,
			PerformerInstanceId = 11, // Rank 2
			TargetCharacterId = 20 // Rank 1
		};
		Assert.True(engine.CanExecute(playCunningShotR2ToR1, state).IsSuccess);
	}

	[Fact]
	public void TabletopRule2_Natural20_CriticalHit_AutoHits_And_Deals_One_Base_Wound()
	{
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["attacker"] = new CardDefinition
				{
					DefinitionId = "attacker",
					Name = "Attacker",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 10,
					BaseHitPoints = 5,
					Attack = new AttackValue
					{
						Strikes = [new StrikeDescriptor { Modifier = 0, IsRanged = false }],
						RawText = "+0"
					}
				},
				["tank"] = new CardDefinition
				{
					DefinitionId = "tank",
					Name = "Armored Tank",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 3,
					ArmorClass = 35, // AC 35 - roll of 20 + 0 = 20 is far below 35
					BaseHitPoints = 10,
					Attack = new AttackValue
					{
						Strikes = [new StrikeDescriptor { Modifier = 0, IsRanged = false }],
						RawText = "+0"
					}
				}
			}
		};

		GameState state = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			AutoPassWhenNoReactsAvailable = true,
			Players =
			[
				new PlayerState
				{
					PlayerIndex = 0,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = [new CardInstance { InstanceId = 1, DefinitionId = "attacker", CurrentArmorClass = 10, CurrentHitPoints = 5, ReadyState = ReadyState.Ready, Zone = CardZone.Board, OwnerPlayerIndex = 0, Rank = 1 }]
					}
				},
				new PlayerState
				{
					PlayerIndex = 1,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = [new CardInstance { InstanceId = 2, DefinitionId = "tank", CurrentArmorClass = 35, CurrentHitPoints = 10, ReadyState = ReadyState.Ready, Zone = CardZone.Board, OwnerPlayerIndex = 1, Rank = 1 }]
					}
				}
			]
		};

		AttackResolutionDetails? details = null;
		GameEngine engine = new GameEngine(state, cardDb, (int count, int sides) => 20); // Natural 20
		engine.OnAttackResolved = d => details = d;

		AttackAction atk = new AttackAction { PlayerIndex = 0, AttackerInstanceId = 1, TargetInstanceId = 2 };
		ActionResult res = engine.Execute(atk);

		Assert.True(res.IsSuccess);
		Assert.NotNull(details);
		Assert.Equal(20, details.Roll);
		Assert.True(details.IsCritical);
		Assert.True(details.IsHit);
		Assert.Equal(1, details.DamageDealt); // Base wound is 1, NOT 2!
		Assert.Equal(9, state.Players[1].Ranks[1][0].CurrentHitPoints); // Exactly 1 wound taken
		Assert.Equal(1, state.Players[1].Ranks[1][0].Wounds);
	}

	[Fact]
	public void TabletopRule3_Dupe_AppearsInLegalReacts_WhenTargetedByEnemyMeleeStrike_AndCausesMiss()
	{
		string cardsJsonPath = Path.Combine(AppContext.BaseDirectory, "DataParser", "cards.json");
		if (!File.Exists(cardsJsonPath))
		{
			cardsJsonPath = Path.Combine(Directory.GetCurrentDirectory(), "DataParser", "cards.json");
		}
		CardDatabase cardDb = DatabaseLoader.LoadFromFile(cardsJsonPath);

		CardDefinition alraynDef = cardDb.GetDefinition("alrayn_gaunt")!;
		CardDefinition dupeDef = cardDb.GetDefinition("dupe")!;

		// Enemy melee attacker
		CardDefinition enemyDef = new CardDefinition
		{
			DefinitionId = "enemy_fighter",
			Name = "Enemy Fighter",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 3,
			ArmorClass = 12,
			BaseHitPoints = 5,
			Skill = 2,
			Attack = new AttackValue
			{
				Strikes = [new StrikeDescriptor { Modifier = 5, IsRanged = false }],
				RawText = "+5"
			}
		};
		Dictionary<string, CardDefinition> dict = new Dictionary<string, CardDefinition>(cardDb.Definitions, StringComparer.OrdinalIgnoreCase);
		dict["enemy_fighter"] = enemyDef;
		cardDb = new CardDatabase { Definitions = dict };

		GameState state = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			AutoPassWhenNoReactsAvailable = true,
			Players =
			[
				new PlayerState
				{
					PlayerIndex = 0,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = [new CardInstance { InstanceId = 1, DefinitionId = "enemy_fighter", CurrentArmorClass = 12, CurrentHitPoints = 5, CurrentSkill = 2, ReadyState = ReadyState.Ready, Zone = CardZone.Board, OwnerPlayerIndex = 0, Rank = 1 }]
					}
				},
				new PlayerState
				{
					PlayerIndex = 1,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] =
						[
							new CardInstance { InstanceId = 10, DefinitionId = "alrayn_gaunt", CurrentArmorClass = alraynDef.ArmorClass ?? 16, CurrentHitPoints = alraynDef.BaseHitPoints ?? 3, CurrentSkill = alraynDef.Skill ?? 9, ReadyState = ReadyState.Ready, Zone = CardZone.Board, OwnerPlayerIndex = 1, Rank = 1 },
							new CardInstance { InstanceId = 11, DefinitionId = "alrayn_gaunt", CurrentArmorClass = alraynDef.ArmorClass ?? 16, CurrentHitPoints = alraynDef.BaseHitPoints ?? 3, CurrentSkill = alraynDef.Skill ?? 9, ReadyState = ReadyState.Ready, Zone = CardZone.Board, OwnerPlayerIndex = 1, Rank = 1 }
						]
					},
					Hand =
					[
						new CardInstance { InstanceId = 50, DefinitionId = "dupe", Zone = CardZone.Hand, OwnerPlayerIndex = 1 }
					]
				}
			]
		};

		GameEngine engine = new GameEngine(state, cardDb, (int count, int sides) => 15);

		// Player 0 attacks Alrayn Gaunt (ID 10)
		AttackAction attackAlrayn = new AttackAction { PlayerIndex = 0, AttackerInstanceId = 1, TargetInstanceId = 10 };
		ActionResult attackResult = engine.Execute(attackAlrayn);
		Assert.True(attackResult.IsSuccess);

		// Because Player 1 has Dupe in hand and Alrayn was targeted by a melee strike,
		// the React window must be open with priority on Player 1!
		Assert.True(state.Priority.IsReactWindowOpen);
		Assert.Equal(1, state.Priority.ActivePlayerIndex);

		// Inspect legal actions for Player 1: Dupe MUST appear!
		var legalReacts = engine.GetLegalActions(state, 1);
		var dupeAction = legalReacts.OfType<PlayCardAction>().FirstOrDefault(a => a.CardInstanceId == 50);
		Assert.NotNull(dupeAction);
		Assert.Equal(10, dupeAction.PerformerInstanceId); // Alrayn Gaunt is the defender and performer!

		// Execute Dupe
		ActionResult dupeResult = engine.Execute(dupeAction);
		Assert.True(dupeResult.IsSuccess);

		// Alrayn Gaunt has DUPE_ACTIVE buff and moved to a different position in Rank 1
		Assert.True(GameEngine.HasTurnBuff(state, 10, "DUPE_ACTIVE"));
		Assert.Equal(10, state.Players[1].Ranks[1][1].InstanceId); // Was at index 0, shifted to index 1

		// Both players pass priority to let the attack resolve
		engine.Execute(new PassAction { PlayerIndex = 1 });
		engine.Execute(new PassAction { PlayerIndex = 0 });

		// React window closed, attack resolved
		Assert.False(state.Priority.IsReactWindowOpen);

		// The strike was a critical failure / miss: Alrayn Gaunt took 0 wounds and remains at full HP!
		CardInstance alraynPostCombat = state.Players[1].Ranks[1].First(c => c.InstanceId == 10);
		Assert.Equal(alraynDef.BaseHitPoints ?? 3, alraynPostCombat.CurrentHitPoints);
		Assert.Equal(0, alraynPostCombat.Wounds);
	}

	[Fact]
	public void TabletopRule3b_Dupe_CannotBePlayed_WhenDefenderIsSoloInRank()
	{
		string cardsPath = Path.Combine(AppContext.BaseDirectory, "DataParser", "cards.json");
		if (!File.Exists(cardsPath))
		{
			cardsPath = Path.Combine(Directory.GetCurrentDirectory(), "DataParser", "cards.json");
		}
		if (!File.Exists(cardsPath))
		{
			cardsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "DataParser", "cards.json"));
		}
		CardDatabase cardDb = DatabaseLoader.LoadFromFile(cardsPath);

		CardDefinition alraynDef = cardDb.GetDefinition("alrayn_gaunt");
		Assert.NotNull(alraynDef);
		CardDefinition dupeDef = cardDb.GetDefinition("dupe");
		Assert.NotNull(dupeDef);

		CardDefinition enemyDef = new CardDefinition
		{
			DefinitionId = "enemy_fighter",
			Name = "Enemy Fighter",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 1,
			ArmorClass = 12,
			BaseHitPoints = 5,
			Attack = new AttackValue
			{
				Strikes = new List<StrikeDescriptor> { new StrikeDescriptor { Modifier = 5, IsRanged = false } }
			}
		};

		Dictionary<string, CardDefinition> dict = new Dictionary<string, CardDefinition>(cardDb.Definitions, StringComparer.OrdinalIgnoreCase);
		dict["enemy_fighter"] = enemyDef;
		cardDb = new CardDatabase { Definitions = dict };

		GameState state = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			AutoPassWhenNoReactsAvailable = false,
			Players =
			[
				new PlayerState
				{
					PlayerIndex = 0,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = [new CardInstance { InstanceId = 1, DefinitionId = "enemy_fighter", CurrentArmorClass = 12, CurrentHitPoints = 5, CurrentSkill = 2, ReadyState = ReadyState.Ready, Zone = CardZone.Board, OwnerPlayerIndex = 0, Rank = 1 }]
					}
				},
				new PlayerState
				{
					PlayerIndex = 1,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						// Solo in Rank 1
						[1] =
						[
							new CardInstance { InstanceId = 10, DefinitionId = "alrayn_gaunt", CurrentArmorClass = alraynDef.ArmorClass ?? 16, CurrentHitPoints = alraynDef.BaseHitPoints ?? 3, CurrentSkill = alraynDef.Skill ?? 9, ReadyState = ReadyState.Ready, Zone = CardZone.Board, OwnerPlayerIndex = 1, Rank = 1 }
						]
					},
					Hand =
					[
						new CardInstance { InstanceId = 50, DefinitionId = "dupe", Zone = CardZone.Hand, OwnerPlayerIndex = 1 }
					]
				}
			]
		};

		GameEngine engine = new GameEngine(state, cardDb, (int count, int sides) => 15);

		// Player 0 attacks Alrayn Gaunt (ID 10)
		AttackAction attackAlrayn = new AttackAction { PlayerIndex = 0, AttackerInstanceId = 1, TargetInstanceId = 10 };
		ActionResult attackResult = engine.Execute(attackAlrayn);
		Assert.True(attackResult.IsSuccess);

		// Check legal actions for Player 1: Dupe must NOT appear because Alrayn is solo in Rank 1!
		var legalActions = engine.GetLegalActions(state, 1);
		var dupeAction = legalActions.OfType<PlayCardAction>().FirstOrDefault(a => a.CardInstanceId == 50);
		Assert.Null(dupeAction);

		// Directly attempting to play Dupe must fail validation
		PlayCardAction directDupe = new PlayCardAction
		{
			PlayerIndex = 1,
			CardInstanceId = 50,
			PerformerInstanceId = 10,
			TargetCharacterId = 10
		};
		ActionResult canExecuteResult = engine.CanExecute(directDupe, state);
		Assert.False(canExecuteResult.IsSuccess);
		Assert.Contains(canExecuteResult.Errors, e => e.Contains("Character must be in a rank with at least one other character to move to a different position in this rank."));
	}
}
