using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public class BucketsAAndDTests
{
	private static GameState CreateTwoPlayerState(CardInstance p0Card, CardInstance p1Card)
	{
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			NextInstanceId = 10
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
	public void Test_MeltMetal_FortitudeSave_Failure_InflictsWounds()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "victim",
			Name = "Victim",
			Type = CardType.Character,
			Level = 1,
			Skill = 2,
			BaseHitPoints = 5,
			ArmorClass = 10
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "sword",
			Name = "Iron Sword",
			Type = CardType.Item,
			Level = 1
		};
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "shield",
			Name = "Iron Shield",
			Type = CardType.Item,
			Level = 1
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition.DefinitionId] = cardDefinition,
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "victim",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 5,
			CurrentSkill = 2
		};
		int num = 2;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<int> span = CollectionsMarshal.AsSpan(list);
		span[0] = 2;
		span[1] = 3;
		cardInstance.AttachedItemIds = list;
		CardInstance p1Card = cardInstance;
		CardInstance p0Card = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "dummy",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board
		};
		GameState state = CreateTwoPlayerState(p0Card, p1Card);
		EffectContext context = new EffectContext
		{
			SourceInstanceId = 10,
			TargetInstanceId = 1,
			State = state,
			CardDb = cardDb,
			RollDice = (int count, int sides) => 5
		};
		FortitudeSaveEffect fortitudeSaveEffect = new FortitudeSaveEffect(13, new List<IEffect> { new DealDamageEffect(1, "physical") }, PerEquippedItem: true);
		List<GameEvent> list2 = fortitudeSaveEffect.Evaluate(context).ToList();
		Assert.Equal(2, list2.Count);
		Assert.All(list2, (GameEvent e) =>
		{
			Assert.IsType<DealDamageEvent>(e);
		});
	}

	[Fact]
	public void Test_MeltMetal_FortitudeSave_Success_NoWounds()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "victim",
			Name = "Victim",
			Type = CardType.Character,
			Level = 1,
			Skill = 4,
			BaseHitPoints = 5,
			ArmorClass = 10
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "sword",
			Name = "Iron Sword",
			Type = CardType.Item,
			Level = 1
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition.DefinitionId] = cardDefinition,
				[cardDefinition2.DefinitionId] = cardDefinition2
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "victim",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 5,
			CurrentSkill = 4
		};
		int num = 1;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = 2;
		cardInstance.AttachedItemIds = list;
		CardInstance p1Card = cardInstance;
		CardInstance p0Card = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "dummy",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board
		};
		GameState state = CreateTwoPlayerState(p0Card, p1Card);
		EffectContext context = new EffectContext
		{
			SourceInstanceId = 10,
			TargetInstanceId = 1,
			State = state,
			CardDb = cardDb,
			RollDice = (int count, int sides) => 15
		};
		FortitudeSaveEffect fortitudeSaveEffect = new FortitudeSaveEffect(13, new List<IEffect> { new DealDamageEffect(1, "physical") }, PerEquippedItem: true);
		List<GameEvent> collection = fortitudeSaveEffect.Evaluate(context).ToList();
		Assert.Empty(collection);
	}

	[Fact]
	public void Test_AssassinsStrike_Advantage_PicksHighestRoll()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "assassin",
			Name = "Assassin",
			Type = CardType.Character,
			Level = 2,
			Skill = 3,
			ArmorClass = 12,
			BaseHitPoints = 3
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "target",
			Name = "Target",
			Type = CardType.Character,
			Level = 2,
			Skill = 2,
			ArmorClass = 15,
			BaseHitPoints = 4
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
			DefinitionId = "assassin",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentArmorClass = 12,
			CurrentHitPoints = 3
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "target",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentArmorClass = 15,
			CurrentHitPoints = 4,
			ReadyState = ReadyState.Spent
		};
		GameState state = CreateTwoPlayerState(p0Card, p1Card);
		int rollIndex = 0;
		int[] rolls = new int[2] { 2, 18 };
		EffectContext context = new EffectContext
		{
			SourceInstanceId = 1,
			TargetInstanceId = 2,
			State = state,
			CardDb = cardDb,
			RollDice = (int count, int sides) => rolls[rollIndex++]
		};
		PerformStrikeEffect performStrikeEffect = new PerformStrikeEffect(0, IsRanged: false, 0, StunOnHit: false, SpendOnHit: false, PushOnHit: false, PullOnHit: false, DestroyItemOnHit: false, ZeroDamage: false, BonusFromLevel: false, TargetSpentOnly: false, TargetStunnedOnly: false, TargetSpentOrStunnedOnly: false, TargetLowerLevelOnly: false, BonusWoundAgainstHigherLevel: false, HasReach: false, TrueStrike: false, HasAdvantage: true);
		List<GameEvent> collection = performStrikeEffect.Evaluate(context).ToList();
		Assert.Contains(collection, (GameEvent e) => e is DealDamageEvent dealDamageEvent && dealDamageEvent.Amount > 0);
	}

	[Fact]
	public void Test_CunningShot_PullTargetForward_OnHit()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "shooter",
			Name = "Shooter",
			Type = CardType.Character,
			Level = 2,
			Skill = 2,
			ArmorClass = 12,
			BaseHitPoints = 3
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "target",
			Name = "Target",
			Type = CardType.Character,
			Level = 1,
			Skill = 1,
			ArmorClass = 10,
			BaseHitPoints = 3
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
			DefinitionId = "shooter",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentArmorClass = 12,
			CurrentHitPoints = 3
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "target",
			OwnerPlayerIndex = 1,
			Rank = 2,
			Zone = CardZone.Board,
			CurrentArmorClass = 10,
			CurrentHitPoints = 3
		};
		GameState state = CreateTwoPlayerState(p0Card, p1Card);
		EffectContext context = new EffectContext
		{
			SourceInstanceId = 1,
			TargetInstanceId = 2,
			State = state,
			CardDb = cardDb,
			RollDice = (int count, int sides) => 15
		};
		PerformStrikeEffect performStrikeEffect = new PerformStrikeEffect(4, IsRanged: true, 0, StunOnHit: false, SpendOnHit: true, PushOnHit: false, PullOnHit: true, DestroyItemOnHit: false, ZeroDamage: true);
		List<GameEvent> collection = performStrikeEffect.Evaluate(context).ToList();
		Assert.Contains(collection, (GameEvent e) => e is ChangeReadyStateEvent changeReadyStateEvent && changeReadyStateEvent.NewReadyState == ReadyState.Spent);
		Assert.Contains(collection, (GameEvent e) => e is MarchEvent { SourceRank: 2 } marchEvent && marchEvent.DestinationRank == 1);
	}

	[Fact]
	public void Test_ShieldOfSpurning_PushesAttackerBackward()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "attacker",
			Name = "Attacker",
			Type = CardType.Character,
			Level = 2,
			BaseHitPoints = 5,
			ArmorClass = 12,
			Attack = new AttackValue
			{
				Strikes = new List<StrikeDescriptor> { new StrikeDescriptor
				{
					Modifier = 0,
					IsRanged = false
				} }
			}
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "defender",
			Name = "Defender",
			Type = CardType.Character,
			Level = 2,
			BaseHitPoints = 5,
			ArmorClass = 15
		};
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "shield",
			Name = "Shield of Spurning",
			Type = CardType.Item,
			Level = 1,
			RulesText = "React: After an attacker targets this character with a melee strike: Push the attacker one rank backward."
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition.DefinitionId] = cardDefinition,
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3
			}
		};
		CardInstance p0Card = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "attacker",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 5,
			CurrentArmorClass = 12
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "defender",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 5,
			CurrentArmorClass = 18
		};
		int num = 1;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = 3;
		cardInstance.AttachedItemIds = list;
		CardInstance p1Card = cardInstance;
		CardInstance item = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "shield",
			OwnerPlayerIndex = 1,
			Zone = CardZone.Attached,
			AttachedToInstanceId = 2
		};
		GameState gameState = CreateTwoPlayerState(p0Card, p1Card);
		gameState.Players[1].DiscardPile.Add(item);
		GameEngine gameEngine = new GameEngine(gameState, cardDb);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		gameEngine.Execute(action);
		Assert.DoesNotContain(gameState.Players[0].Ranks.TryGetValue(1, out List<CardInstance> value) ? value : new List<CardInstance>(), (CardInstance c) => c.InstanceId == 1);
		Assert.True(gameState.Players[0].Ranks.TryGetValue(2, out List<CardInstance> value2) && value2.Any((CardInstance c) => c.InstanceId == 1));
	}

	[Fact]
	public void Test_AcVuk_TurnEnd_DiesIfDidNotWoundOpponent()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "acvuk",
			Name = "Ac'Vuk",
			Type = CardType.Character,
			Level = 2,
			BaseHitPoints = 3,
			ArmorClass = 12
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { [cardDefinition.DefinitionId] = cardDefinition }
		};
		CardInstance p0Card = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "acvuk",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 3,
			CurrentArmorClass = 12
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "dummy",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board
		};
		GameState gameState = CreateTwoPlayerState(p0Card, p1Card);
		GameEngine gameEngine = new GameEngine(gameState, cardDb);
		gameState.CurrentPhase = TurnPhase.End;
		gameEngine.AdvancePhase();
		Assert.DoesNotContain(gameState.Players[0].Ranks.TryGetValue(1, out List<CardInstance> value) ? value : new List<CardInstance>(), (CardInstance c) => c.InstanceId == 1);
		Assert.Contains(gameState.Players[0].DiscardPile, (CardInstance c) => c.InstanceId == 1);
	}

	[Fact]
	public void Test_AcVuk_TurnEnd_SurvivesIfWoundedOpponent()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "acvuk",
			Name = "Ac'Vuk",
			Type = CardType.Character,
			Level = 2,
			BaseHitPoints = 3,
			ArmorClass = 12
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { [cardDefinition.DefinitionId] = cardDefinition }
		};
		CardInstance p0Card = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "acvuk",
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board,
			CurrentHitPoints = 3,
			CurrentArmorClass = 12
		};
		CardInstance p1Card = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "dummy",
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board
		};
		GameState gameState = CreateTwoPlayerState(p0Card, p1Card);
		GameEngine gameEngine = new GameEngine(gameState, cardDb);
		GameEngine.AddTurnBuff(gameState, 1, "WOUNDED_OPPONENT_THIS_TURN", 1);
		gameState.CurrentPhase = TurnPhase.End;
		gameEngine.AdvancePhase();
		Assert.True(gameState.Players[0].Ranks[1].Any((CardInstance c) => c.InstanceId == 1));
	}

	[Fact]
	public void Test_RollTableEffect_EvaluatesCorrectBranch()
	{
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "dummy",
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		GameState state = CreateTwoPlayerState(cardInstance, cardInstance);
		List<RollTableBranch> branches = new List<RollTableBranch>
		{
			new RollTableBranch(1, 9, new List<IEffect> { new ModifyStatEffect("ATK", 1) }),
			new RollTableBranch(10, 19, new List<IEffect> { new ModifyStatEffect("ATK", 3) }),
			new RollTableBranch(20, 20, new List<IEffect> { new ModifyStatEffect("ATK", 10) })
		};
		RollTableEffect rollTableEffect = new RollTableEffect(branches);
		EffectContext context = new EffectContext
		{
			SourceInstanceId = 1,
			TargetInstanceId = 1,
			State = state,
			CardDb = new CardDatabase(),
			RollDice = (int count, int sides) => 15
		};
		List<GameEvent> list = rollTableEffect.Evaluate(context).ToList();
		Assert.Single(list);
		ModifyStatEvent modifyStatEvent = Assert.IsType<ModifyStatEvent>(list[0]);
		Assert.Equal(3, modifyStatEvent.Amount);
	}
}
