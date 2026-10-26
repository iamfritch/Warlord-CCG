using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordEngine.Actions;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public class StarterDeckMechanicsTask7BTests
{
	private static CardDatabase CreateTestCardDb()
	{
		return new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["tyren_ruskin"] = new CardDefinition
				{
					DefinitionId = "tyren_ruskin",
					Name = "Tyren Ruskin",
					Type = CardType.Character,
					Class = CharacterClass.Cleric,
					Level = 2,
					ArmorClass = 9,
					BaseHitPoints = 2,
					RulesText = "Spend React: Before target character in this rank suffers wounds, wound Tyren: Reduce the wounds the target would suffer by one.",
					Abilities = RulesParser.Parse("Spend React: Before target character in this rank suffers wounds, wound Tyren: Reduce the wounds the target would suffer by one.", "Tyren Ruskin"),
					Attack = new AttackValue
					{
						RawText = "+0",
						Strikes = new _003C_003Ez__ReadOnlySingleElementList<StrikeDescriptor>(new StrikeDescriptor
						{
							Modifier = 0,
							IsRanged = false
						})
					}
				},
				["keen_eared_sentry"] = new CardDefinition
				{
					DefinitionId = "keen_eared_sentry",
					Name = "Keen Eared Sentry",
					Type = CardType.Character,
					Class = CharacterClass.Rogue,
					Level = 1,
					ArmorClass = 11,
					BaseHitPoints = 1,
					Traits = new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "Human", "Scout", "Stealth +9", "Free Kingdoms" }),
					SkillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Stealth"] = 9 },
					Attack = new AttackValue
					{
						RawText = "+2",
						Strikes = new _003C_003Ez__ReadOnlySingleElementList<StrikeDescriptor>(new StrikeDescriptor
						{
							Modifier = 2,
							IsRanged = false
						})
					}
				},
				["generic_fighter"] = new CardDefinition
				{
					DefinitionId = "generic_fighter",
					Name = "Fighter Soldier",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 10,
					BaseHitPoints = 3,
					Attack = new AttackValue
					{
						RawText = "+1",
						Strikes = new _003C_003Ez__ReadOnlySingleElementList<StrikeDescriptor>(new StrikeDescriptor
						{
							Modifier = 1,
							IsRanged = false
						})
					}
				},
				["enemy_target"] = new CardDefinition
				{
					DefinitionId = "enemy_target",
					Name = "Enemy Target",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 12,
					BaseHitPoints = 5,
					Attack = new AttackValue
					{
						RawText = "+0",
						Strikes = new _003C_003Ez__ReadOnlySingleElementList<StrikeDescriptor>(new StrikeDescriptor
						{
							Modifier = 0,
							IsRanged = false
						})
					}
				},
				["sneak_attack"] = new CardDefinition
				{
					DefinitionId = "sneak_attack",
					Name = "Sneak Attack",
					Type = CardType.Action,
					Class = CharacterClass.Rogue,
					Level = 1,
					RulesText = "Spend Order: Perform a +4 melee strike that may only target a spent character.",
					Abilities = RulesParser.Parse("Spend Order: Perform a +4 melee strike that may only target a spent character.", "Sneak Attack")
				},
				["stealth_ambush"] = new CardDefinition
				{
					DefinitionId = "stealth_ambush",
					Name = "Stealth Ambush",
					Type = CardType.Action,
					Class = CharacterClass.Rogue,
					Level = 1,
					RulesText = "Requires: Stealth 5\nOrder: Perform a +3 melee strike.",
					Abilities = RulesParser.Parse("Order: Perform a +3 melee strike.", "Stealth Ambush")
				},
				["scale_mail"] = new CardDefinition
				{
					DefinitionId = "scale_mail",
					Name = "Scale Mail",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					RulesText = "+2 AC",
					Traits = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Armor", "Mail" })
				},
				["bardiche"] = new CardDefinition
				{
					DefinitionId = "bardiche",
					Name = "Bardiche",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					RulesText = "+2 ATK",
					Traits = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Weapon", "Polearm" })
				},
				["riding_horse"] = new CardDefinition
				{
					DefinitionId = "riding_horse",
					Name = "Riding Horse",
					Type = CardType.Item,
					Class = CharacterClass.Classless,
					RulesText = "Order: Spend Riding Horse: Move forward one rank.",
					Abilities = RulesParser.Parse("Order: Spend Riding Horse: Move forward one rank.", "Riding Horse")
				}
			}
		};
	}

	[Fact]
	public void SneakAttack_RequiresValidPerformer_SpendsPerformer_And_RollsRealStrikeWithBonus()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance rogueInstance = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "keen_eared_sentry",
			CurrentHitPoints = 1,
			CurrentArmorClass = 11,
			CurrentSkill = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 11,
			DefinitionId = "generic_fighter",
			CurrentHitPoints = 3,
			CurrentArmorClass = 10,
			CurrentSkill = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 100,
			DefinitionId = "sneak_attack",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance enemyTarget = new CardInstance
		{
			InstanceId = 20,
			DefinitionId = "enemy_target",
			CurrentHitPoints = 5,
			CurrentArmorClass = 14,
			CurrentSkill = 1,
			ReadyState = ReadyState.Spent,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
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
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance2;
		playerState.Hand = list2;
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 2;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list3);
		span2[0] = rogueInstance;
		span2[1] = cardInstance;
		sortedDictionary[1] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		CollectionsMarshal.AsSpan(list4)[0] = enemyTarget;
		sortedDictionary2[1] = list4;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb, (int count, int sides) => 10);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 100,
			PerformerInstanceId = cardInstance.InstanceId,
			TargetCharacterId = enemyTarget.InstanceId
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains("does not match Action class", string.Join("; ", actionResult.Errors));
		PlayCardAction action2 = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 100,
			PerformerInstanceId = rogueInstance.InstanceId,
			TargetCharacterId = enemyTarget.InstanceId
		};
		ActionResult actionResult2 = gameEngine.CanExecute(action2);
		Assert.True(actionResult2.IsSuccess, string.Join("; ", actionResult2.Errors));
		ActionResult actionResult3 = gameEngine.Execute(action2);
		Assert.True(actionResult3.IsSuccess, string.Join("; ", actionResult3.Errors));
		Assert.Equal(ReadyState.Spent, gameState2.Players[0].Ranks[1].First((CardInstance c) => c.InstanceId == rogueInstance.InstanceId).ReadyState);
		Assert.DoesNotContain(gameState2.Players[0].Hand, (CardInstance c) => c.InstanceId == 100);
		Assert.Contains(gameState2.Players[0].DiscardPile, (CardInstance c) => c.InstanceId == 100 && c.Zone == CardZone.Discard);
		Assert.Equal(4, gameState2.Players[1].Ranks[1].First((CardInstance c) => c.InstanceId == enemyTarget.InstanceId).CurrentHitPoints);
	}

	[Fact]
	public void Attaching_StatItems_ScaleMail_And_Bardiche_Increases_Effective_AC_And_ATK_In_Combat()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "generic_fighter",
			CurrentHitPoints = 3,
			CurrentArmorClass = 10,
			CurrentSkill = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "enemy_target",
			CurrentHitPoints = 5,
			CurrentArmorClass = 12,
			CurrentSkill = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 50,
			DefinitionId = "scale_mail",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance4 = new CardInstance
		{
			InstanceId = 51,
			DefinitionId = "bardiche",
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
		int num2 = 2;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = cardInstance3;
		span2[1] = cardInstance4;
		playerState.Hand = list2;
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance;
		sortedDictionary[1] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		CollectionsMarshal.AsSpan(list4)[0] = cardInstance2;
		sortedDictionary2[1] = list4;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 50,
			TargetCharacterId = 1
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
		CardInstance cardInstance5 = gameState2.Players[0].Ranks[1][0];
		Assert.Contains(50, cardInstance5.AttachedItemIds);
		Assert.Equal(12, cardInstance5.CurrentArmorClass);
		gameEngine.Execute(new PassAction
		{
			PlayerIndex = 1
		});
		PlayCardAction action2 = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 51,
			TargetCharacterId = 1
		};
		ActionResult actionResult2 = gameEngine.Execute(action2);
		Assert.True(actionResult2.IsSuccess, string.Join("; ", actionResult2.Errors));
		Assert.Contains(51, gameState2.Players[0].Ranks[1][0].AttachedItemIds);
		gameEngine.Execute(new PassAction
		{
			PlayerIndex = 1
		});
		gameEngine.SetDiceRoller((int count, int sides) => 9);
		AttackAction action3 = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult3 = gameEngine.Execute(action3);
		Assert.True(actionResult3.IsSuccess, string.Join("; ", actionResult3.Errors));
		Assert.Equal(4, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void TyrenRuskin_SpendReact_Prevents1WoundOnSameRankAlly_WhileWoundingTyren()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "tyren_ruskin",
			CurrentHitPoints = 2,
			CurrentArmorClass = 9,
			CurrentSkill = 2,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "generic_fighter",
			CurrentHitPoints = 3,
			CurrentArmorClass = 10,
			CurrentSkill = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "enemy_target",
			CurrentHitPoints = 5,
			CurrentArmorClass = 12,
			CurrentSkill = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 1,
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
		span2[0] = cardInstance;
		span2[1] = cardInstance2;
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
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance3;
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb, (int count, int sides) => 15);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 1,
			AttackerInstanceId = 3,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
		Assert.True(gameState2.Priority.IsReactWindowOpen);
		Assert.Equal(0, gameState2.Priority.ActivePlayerIndex);
		UseAbilityAction action2 = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 1,
			AbilityIndex = 0,
			TargetInstanceIds = new int[1] { 2 }
		};
		ActionResult actionResult2 = gameEngine.CanExecute(action2);
		Assert.True(actionResult2.IsSuccess, string.Join("; ", actionResult2.Errors));
		ActionResult actionResult3 = gameEngine.Execute(action2);
		Assert.True(actionResult3.IsSuccess, string.Join("; ", actionResult3.Errors));
		gameEngine.Execute(new PassAction
		{
			PlayerIndex = 1
		});
		gameEngine.Execute(new PassAction
		{
			PlayerIndex = 0
		});
		CardInstance cardInstance4 = gameState2.Players[0].Ranks[1].First((CardInstance c) => c.InstanceId == 1);
		Assert.Equal(ReadyState.Spent, cardInstance4.ReadyState);
		Assert.Equal(1, cardInstance4.CurrentHitPoints);
		Assert.Equal(3, gameState2.Players[0].Ranks[1].First((CardInstance c) => c.InstanceId == 2).CurrentHitPoints);
	}

	[Fact]
	public void KeenEaredSentry_Satisfies_StealthRequirement_For_Actions()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "keen_eared_sentry",
			CurrentHitPoints = 1,
			CurrentArmorClass = 11,
			CurrentSkill = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1,
			SkillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Stealth"] = 9 }
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "generic_fighter",
			CurrentHitPoints = 3,
			CurrentArmorClass = 10,
			CurrentSkill = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 100,
			DefinitionId = "stealth_ambush",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance4 = new CardInstance
		{
			InstanceId = 20,
			DefinitionId = "enemy_target",
			CurrentHitPoints = 5,
			CurrentArmorClass = 12,
			CurrentSkill = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
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
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance3;
		playerState.Hand = list2;
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 2;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list3);
		span2[0] = cardInstance;
		span2[1] = cardInstance2;
		sortedDictionary[1] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		CollectionsMarshal.AsSpan(list4)[0] = cardInstance4;
		sortedDictionary2[1] = list4;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState state = gameState;
		GameEngine gameEngine = new GameEngine(state, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 100,
			PerformerInstanceId = 2,
			TargetCharacterId = 20
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		PlayCardAction action2 = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 100,
			PerformerInstanceId = 1,
			TargetCharacterId = 20
		};
		ActionResult actionResult2 = gameEngine.CanExecute(action2);
		Assert.True(actionResult2.IsSuccess, string.Join("; ", actionResult2.Errors));
	}

	[Fact]
	public void AttachedItem_Abilities_CanBeActivatedByEquippedCharacter()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "generic_fighter",
			CurrentHitPoints = 3,
			CurrentArmorClass = 10,
			CurrentSkill = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 2
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "riding_horse",
			Zone = CardZone.Attached,
			AttachedToInstanceId = 1,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready
		};
		cardInstance.AttachedItemIds.Add(10);
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "generic_fighter",
			CurrentHitPoints = 3,
			CurrentArmorClass = 10,
			CurrentSkill = 1,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
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
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance3;
		sortedDictionary[1] = list2;
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance;
		sortedDictionary[2] = list3;
		playerState.Ranks = sortedDictionary;
		num2 = 1;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		CollectionsMarshal.AsSpan(list4)[0] = cardInstance2;
		playerState.DiscardPile = list4;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		IReadOnlyList<IPlayerAction> legalActions = gameEngine.GetLegalActions(gameState2, 0);
		UseAbilityAction useAbilityAction = legalActions.OfType<UseAbilityAction>().FirstOrDefault((UseAbilityAction a) => a.SourceInstanceId == 10);
		Assert.NotNull(useAbilityAction);
		ActionResult actionResult = gameEngine.Execute(useAbilityAction);
		Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
		Assert.Equal(ReadyState.Spent, gameState2.Players[0].DiscardPile.First((CardInstance c) => c.InstanceId == 10).ReadyState);
		Assert.Contains(gameState2.Players[0].Ranks[1], (CardInstance c) => c.InstanceId == 1);
	}
}
