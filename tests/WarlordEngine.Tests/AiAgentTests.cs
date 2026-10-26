using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordEngine.AI;
using WarlordEngine.Actions;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class AiAgentTests
{
	private static string GetRealCardsJsonPath()
	{
		string[] array = new string[5]
		{
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataParser", "cards.json"),
			Path.Combine(Directory.GetCurrentDirectory(), "DataParser", "cards.json"),
			Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "DataParser", "cards.json"),
			Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "DataParser", "cards.json"),
			"C:\\Users\\iamfr\\Documents\\WarlordCCG\\DataParser\\cards.json"
		};
		string[] array2 = array;
		foreach (string path in array2)
		{
			if (File.Exists(path))
			{
				return Path.GetFullPath(path);
			}
		}
		throw new FileNotFoundException("cards.json not found.");
	}

	private static CardDatabase CreateTestCardDb()
	{
		return new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["warlord-hero"] = new CardDefinition
				{
					DefinitionId = "warlord-hero",
					Name = "Warlord Hero",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 3,
					ArmorClass = 15,
					BaseHitPoints = 12,
					IsWarlord = true,
					Faction = "Free Kingdoms",
					Attack = new AttackValue
					{
						Strikes = new List<StrikeDescriptor> { new StrikeDescriptor
						{
							Modifier = 5,
							IsRanged = false
						} },
						RawText = "+5"
					}
				},
				["veteran-sergeant"] = new CardDefinition
				{
					DefinitionId = "veteran-sergeant",
					Name = "Veteran Sergeant",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 2,
					ArmorClass = 14,
					BaseHitPoints = 8,
					Faction = "Free Kingdoms",
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
				["foot-soldier"] = new CardDefinition
				{
					DefinitionId = "foot-soldier",
					Name = "Foot Soldier",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 12,
					BaseHitPoints = 6,
					Faction = "Free Kingdoms",
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
	}

	[Fact]
	public void BoardStateEvaluator_Scores_Wounded_Enemy_Warlords_Higher_Than_Wounded_Own_Warlords()
	{
		CardDatabase cardDb = CreateTestCardDb();
		BoardStateEvaluator boardStateEvaluator = new BoardStateEvaluator(cardDb);
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
			PlayerIndex = 0,
			WarlordInstanceId = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 12,
			CurrentArmorClass = 15,
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
			WarlordInstanceId = 2
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState state = gameState;
		GameState gameState2 = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main
		};
		num = 2;
		List<PlayerState> list4 = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list4, num);
		Span<PlayerState> span2 = CollectionsMarshal.AsSpan(list4);
		ref PlayerState reference3 = ref span2[0];
		PlayerState playerState3 = new PlayerState
		{
			PlayerIndex = 0,
			WarlordInstanceId = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary3 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list5 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list5, num2);
		CollectionsMarshal.AsSpan(list5)[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary3[1] = list5;
		playerState3.Ranks = sortedDictionary3;
		reference3 = playerState3;
		ref PlayerState reference4 = ref span2[1];
		PlayerState playerState4 = new PlayerState
		{
			PlayerIndex = 1,
			WarlordInstanceId = 2
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary4 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list6 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list6, num2);
		CollectionsMarshal.AsSpan(list6)[0] = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 12,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		sortedDictionary4[1] = list6;
		playerState4.Ranks = sortedDictionary4;
		reference4 = playerState4;
		gameState2.Players = list4;
		GameState state2 = gameState2;
		double num3 = boardStateEvaluator.Evaluate(state, 0);
		double num4 = boardStateEvaluator.Evaluate(state2, 0);
		Assert.True(num3 > num4, $"Expected score with wounded enemy Warlord ({num3}) to be higher than with wounded own Warlord ({num4}).");
	}

	[Fact]
	public void BoardStateEvaluator_Scores_Ready_Characters_Higher_Than_Stunned_Characters()
	{
		CardDatabase cardDb = CreateTestCardDb();
		BoardStateEvaluator boardStateEvaluator = new BoardStateEvaluator(cardDb);
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
			PlayerIndex = 0,
			WarlordInstanceId = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 12,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary[2] = list2;
		num2 = 2;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list3);
		span2[0] = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "foot-soldier",
			CurrentHitPoints = 6,
			CurrentArmorClass = 12,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		span2[1] = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "foot-soldier",
			CurrentHitPoints = 6,
			CurrentArmorClass = 12,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary[1] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			WarlordInstanceId = 10
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		CollectionsMarshal.AsSpan(list4)[0] = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 12,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		sortedDictionary2[1] = list4;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState state = gameState;
		GameState gameState2 = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main
		};
		num = 2;
		List<PlayerState> list5 = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list5, num);
		Span<PlayerState> span3 = CollectionsMarshal.AsSpan(list5);
		ref PlayerState reference3 = ref span3[0];
		PlayerState playerState3 = new PlayerState
		{
			PlayerIndex = 0,
			WarlordInstanceId = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary3 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list6 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list6, num2);
		CollectionsMarshal.AsSpan(list6)[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 12,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary3[2] = list6;
		num2 = 2;
		List<CardInstance> list7 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list7, num2);
		Span<CardInstance> span4 = CollectionsMarshal.AsSpan(list7);
		span4[0] = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "foot-soldier",
			CurrentHitPoints = 6,
			CurrentArmorClass = 12,
			ReadyState = ReadyState.Stunned,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		span4[1] = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "foot-soldier",
			CurrentHitPoints = 6,
			CurrentArmorClass = 12,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary3[1] = list7;
		playerState3.Ranks = sortedDictionary3;
		reference3 = playerState3;
		ref PlayerState reference4 = ref span3[1];
		PlayerState playerState4 = new PlayerState
		{
			PlayerIndex = 1,
			WarlordInstanceId = 10
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary4 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list8 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list8, num2);
		CollectionsMarshal.AsSpan(list8)[0] = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 12,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		sortedDictionary4[1] = list8;
		playerState4.Ranks = sortedDictionary4;
		reference4 = playerState4;
		gameState2.Players = list5;
		GameState state2 = gameState2;
		double num3 = boardStateEvaluator.Evaluate(state, 0);
		double num4 = boardStateEvaluator.Evaluate(state2, 0);
		Assert.True(num3 > num4, $"Expected score with Ready character ({num3}) to be higher than with Stunned character ({num4}).");
	}

	[Fact]
	public void HeuristicAiAgent_Chooses_Lethal_Attack_On_Enemy_Warlord_Over_Passing()
	{
		CardDatabase cardDb = CreateTestCardDb();
		HeuristicAiAgent heuristicAiAgent = new HeuristicAiAgent();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 12,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "foot-soldier",
			CurrentHitPoints = 1,
			CurrentArmorClass = 12,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Combat
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0,
			WarlordInstanceId = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			WarlordInstanceId = 2
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance2;
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState state = gameState;
		GameEngine gameEngine = new GameEngine(state, cardDb);
		IReadOnlyList<IPlayerAction> legalActions = gameEngine.GetLegalActions(state, 0);
		Assert.Contains(legalActions, (IPlayerAction a) => a is AttackAction);
		Assert.Contains(legalActions, (IPlayerAction a) => a is PassAction);
		IPlayerAction playerAction = heuristicAiAgent.SelectAction(state, gameEngine, 0);
		Assert.IsType<AttackAction>(playerAction);
		AttackAction attackAction = (AttackAction)playerAction;
		Assert.Equal(1, attackAction.AttackerInstanceId);
		Assert.Equal(2, attackAction.TargetInstanceId);
	}

	[Fact]
	public void HeuristicAiAgent_Deploys_Characters_During_Main_Phase_Instead_Of_Passing_With_Empty_Board()
	{
		CardDatabase cardDb = CreateTestCardDb();
		HeuristicAiAgent heuristicAiAgent = new HeuristicAiAgent();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "foot-soldier",
			CurrentHitPoints = 6,
			CurrentArmorClass = 12,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
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
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		playerState.Hand = list2;
		playerState.Ranks = new SortedDictionary<int, List<CardInstance>>();
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			WarlordInstanceId = 10
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 12,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		sortedDictionary[1] = list3;
		playerState2.Ranks = sortedDictionary;
		reference2 = playerState2;
		gameState.Players = list;
		GameState state = gameState;
		GameEngine gameEngine = new GameEngine(state, cardDb);
		IReadOnlyList<IPlayerAction> legalActions = gameEngine.GetLegalActions(state, 0);
		Assert.Contains(legalActions, (IPlayerAction a) => a is PlayCardAction);
		Assert.Contains(legalActions, (IPlayerAction a) => a is PassAction);
		IPlayerAction playerAction = heuristicAiAgent.SelectAction(state, gameEngine, 0);
		Assert.IsType<PlayCardAction>(playerAction);
		PlayCardAction playCardAction = (PlayCardAction)playerAction;
		Assert.Equal(1, playCardAction.CardInstanceId);
		Assert.Equal(1, playCardAction.TargetRank);
	}

	[Fact]
	public void Two_HeuristicAiAgent_Bots_Can_Play_Complete_Headless_Match_Without_Exceptions_Or_Infinite_Loop()
	{
		string realCardsJsonPath = GetRealCardsJsonPath();
		CardDatabase cardDb = DatabaseLoader.LoadFromFile(realCardsJsonPath);
		IReadOnlyList<Deck> readOnlyList = PresetDeckFactory.GeneratePresetDecks(cardDb);
		Assert.True(readOnlyList.Count >= 2, "Expected at least 2 preset decks for match.");
		GameEngine gameEngine = GameEngine.CreateFromDecks(new List<Deck>
		{
			readOnlyList[0],
			readOnlyList[1]
		}, cardDb);
		for (int i = 0; i < 2; i++)
		{
			gameEngine.State.ActivePlayerIndex = i;
			List<CardInstance> hand = gameEngine.State.Players[i].Hand;
			CardInstance cardInstance = hand.First((CardInstance c) => cardDb.GetDefinition(c.DefinitionId).IsWarlord);
			List<CardInstance> list = hand.Where((CardInstance c) => !cardDb.GetDefinition(c.DefinitionId).IsWarlord && cardDb.GetDefinition(c.DefinitionId).Level == 2).Take(2).ToList();
			List<CardInstance> list2 = hand.Where((CardInstance c) => !cardDb.GetDefinition(c.DefinitionId).IsWarlord && cardDb.GetDefinition(c.DefinitionId).Level == 1).Take(3).ToList();
			DeployStartingArmyAction action = new DeployStartingArmyAction
			{
				PlayerIndex = i,
				Deployments = new List<StartingCharacterDeployment>
				{
					new StartingCharacterDeployment(cardInstance.InstanceId, 3),
					new StartingCharacterDeployment(list[0].InstanceId, 2),
					new StartingCharacterDeployment(list[1].InstanceId, 2),
					new StartingCharacterDeployment(list2[0].InstanceId, 1),
					new StartingCharacterDeployment(list2[1].InstanceId, 1),
					new StartingCharacterDeployment(list2[2].InstanceId, 1)
				}
			};
			ActionResult actionResult = gameEngine.CanExecute(action);
			Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
			gameEngine.Execute(action);
		}
		Assert.Equal(TurnPhase.Ready, gameEngine.State.CurrentPhase);
		HeuristicAiAgent heuristicAiAgent = new HeuristicAiAgent(null, "Bot 0");
		HeuristicAiAgent heuristicAiAgent2 = new HeuristicAiAgent(null, "Bot 1");
		int num = 0;
		while (!gameEngine.State.IsGameOver && num < 200)
		{
			TurnPhase currentPhase = gameEngine.State.CurrentPhase;
			if ((uint)(currentPhase - 1) <= 1u)
			{
				gameEngine.AdvancePhase();
				continue;
			}
			int num2 = (gameEngine.State.Priority.IsReactWindowOpen ? gameEngine.State.Priority.ActivePlayerIndex : gameEngine.State.ActivePlayerIndex);
			HeuristicAiAgent heuristicAiAgent3 = ((num2 == 0) ? heuristicAiAgent : heuristicAiAgent2);
			IPlayerAction playerAction = heuristicAiAgent3.SelectAction(gameEngine.State, gameEngine, num2);
			Assert.NotNull(playerAction);
			ActionResult actionResult2 = gameEngine.ExecuteAction(playerAction);
			Assert.True(actionResult2.IsSuccess, "Action execution failed (" + playerAction.GetType().Name + "): " + string.Join("; ", actionResult2.Errors));
			num++;
			if (!gameEngine.State.Priority.IsReactWindowOpen && playerAction is PassAction)
			{
				gameEngine.AdvancePhase();
			}
		}
		Assert.True(num > 0, "Bots should have taken at least one action.");
		Assert.True(num <= 200, $"Safety cap of {200} actions was exceeded.");
	}

	[Fact]
	public void MctsAiAgent_Chooses_Lethal_Attack_On_Enemy_Warlord_Over_Passing()
	{
		CardDatabase cardDb = CreateTestCardDb();
		MctsAiAgent mctsAiAgent = new MctsAiAgent(null, 100);
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 12,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "foot-soldier",
			CurrentHitPoints = 1,
			CurrentArmorClass = 12,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Combat
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0,
			WarlordInstanceId = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			WarlordInstanceId = 2
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance2;
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState state = gameState;
		GameEngine gameEngine = new GameEngine(state, cardDb);
		IReadOnlyList<IPlayerAction> legalActions = gameEngine.GetLegalActions(state, 0);
		Assert.Contains(legalActions, (IPlayerAction a) => a is AttackAction);
		Assert.Contains(legalActions, (IPlayerAction a) => a is PassAction);
		IPlayerAction playerAction = mctsAiAgent.SelectAction(state, gameEngine, 0);
		Assert.IsType<AttackAction>(playerAction);
		AttackAction attackAction = (AttackAction)playerAction;
		Assert.Equal(1, attackAction.AttackerInstanceId);
		Assert.Equal(2, attackAction.TargetInstanceId);
	}

	[Fact]
	public void MctsAiAgent_Handles_React_Window_Without_Crashing()
	{
		CardDatabase cardDb = CreateTestCardDb();
		MctsAiAgent mctsAiAgent = new MctsAiAgent(null, 50);
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 12,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 12,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Combat
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0,
			WarlordInstanceId = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			WarlordInstanceId = 2
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance2;
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		gameState2.Priority.IsReactWindowOpen = true;
		gameState2.Priority.ActivePlayerIndex = 1;
		IPlayerAction playerAction = mctsAiAgent.SelectAction(gameState2, gameEngine, 1);
		Assert.NotNull(playerAction);
		ActionResult actionResult = gameEngine.ExecuteAction(playerAction);
		Assert.True(actionResult.IsSuccess, "React action failed: " + string.Join("; ", actionResult.Errors));
	}

	[Fact]
	public void MctsAiAgent_Plays_Full_Headless_Match_Against_HeuristicAiAgent()
	{
		string realCardsJsonPath = GetRealCardsJsonPath();
		CardDatabase cardDb = DatabaseLoader.LoadFromFile(realCardsJsonPath);
		IReadOnlyList<Deck> readOnlyList = PresetDeckFactory.GeneratePresetDecks(cardDb);
		Assert.True(readOnlyList.Count >= 2, "Expected at least 2 preset decks for match.");
		GameEngine gameEngine = GameEngine.CreateFromDecks(new List<Deck>
		{
			readOnlyList[0],
			readOnlyList[1]
		}, cardDb);
		for (int i = 0; i < 2; i++)
		{
			gameEngine.State.ActivePlayerIndex = i;
			List<CardInstance> hand = gameEngine.State.Players[i].Hand;
			CardInstance cardInstance = hand.First((CardInstance c) => cardDb.GetDefinition(c.DefinitionId).IsWarlord);
			List<CardInstance> list = hand.Where((CardInstance c) => !cardDb.GetDefinition(c.DefinitionId).IsWarlord && cardDb.GetDefinition(c.DefinitionId).Level == 2).Take(2).ToList();
			List<CardInstance> list2 = hand.Where((CardInstance c) => !cardDb.GetDefinition(c.DefinitionId).IsWarlord && cardDb.GetDefinition(c.DefinitionId).Level == 1).Take(3).ToList();
			DeployStartingArmyAction action = new DeployStartingArmyAction
			{
				PlayerIndex = i,
				Deployments = new List<StartingCharacterDeployment>
				{
					new StartingCharacterDeployment(cardInstance.InstanceId, 3),
					new StartingCharacterDeployment(list[0].InstanceId, 2),
					new StartingCharacterDeployment(list[1].InstanceId, 2),
					new StartingCharacterDeployment(list2[0].InstanceId, 1),
					new StartingCharacterDeployment(list2[1].InstanceId, 1),
					new StartingCharacterDeployment(list2[2].InstanceId, 1)
				}
			};
			ActionResult actionResult = gameEngine.CanExecute(action);
			Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
			gameEngine.Execute(action);
		}
		Assert.Equal(TurnPhase.Ready, gameEngine.State.CurrentPhase);
		MctsAiAgent mctsAiAgent = new MctsAiAgent(null, 50, 1.414, 8, "MctsBot");
		HeuristicAiAgent heuristicAiAgent = new HeuristicAiAgent(null, "HeuristicBot");
		int num = 0;
		while (!gameEngine.State.IsGameOver && num < 150)
		{
			TurnPhase currentPhase = gameEngine.State.CurrentPhase;
			if ((uint)(currentPhase - 1) <= 1u)
			{
				gameEngine.AdvancePhase();
				continue;
			}
			int num2 = (gameEngine.State.Priority.IsReactWindowOpen ? gameEngine.State.Priority.ActivePlayerIndex : gameEngine.State.ActivePlayerIndex);
			IAiAgent aiAgent2;
			if (num2 != 0)
			{
				IAiAgent aiAgent = heuristicAiAgent;
				aiAgent2 = aiAgent;
			}
			else
			{
				IAiAgent aiAgent = mctsAiAgent;
				aiAgent2 = aiAgent;
			}
			IAiAgent aiAgent3 = aiAgent2;
			IPlayerAction playerAction = aiAgent3.SelectAction(gameEngine.State, gameEngine, num2);
			Assert.NotNull(playerAction);
			ActionResult actionResult2 = gameEngine.ExecuteAction(playerAction);
			Assert.True(actionResult2.IsSuccess, "Action execution failed (" + playerAction.GetType().Name + "): " + string.Join("; ", actionResult2.Errors));
			num++;
			if (!gameEngine.State.Priority.IsReactWindowOpen && playerAction is PassAction)
			{
				gameEngine.AdvancePhase();
			}
		}
		Assert.True(num > 0, "Bots should have taken at least one action.");
		Assert.True(num <= 150, $"Safety cap of {150} actions was exceeded.");
	}
}
