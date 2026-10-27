using System;
using System.Collections.Generic;
using System.Linq;
using WarlordCli;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.AI;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public class Batch3PlaytestFixesTests
{
	private readonly CardDatabase _cardDb;

	public Batch3PlaytestFixesTests()
	{
		_cardDb = DatabaseLoader.LoadFromFile(DataPaths.CardsJsonPath);
	}

	private CardDefinition GetDef(string name)
	{
		CardDefinition def = _cardDb.Definitions.Values.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
		if (def == null)
		{
			string definitionId = name.ToLowerInvariant().Replace(" ", "_").Replace("'", "").Replace(",", "");
			def = _cardDb.GetDefinition(definitionId);
		}
		Assert.NotNull(def);
		return def;
	}

	private static PlayerState CreatePlayer(int playerIndex, string name)
	{
		return new PlayerState
		{
			PlayerIndex = playerIndex,
			DisplayName = name,
			Hand = new List<CardInstance>(),
			Deck = new List<CardInstance>(),
			DiscardPile = new List<CardInstance>(),
			BanishPile = new List<CardInstance>(),
			AttachedItems = new List<CardInstance>(),
			Ranks = new SortedDictionary<int, List<CardInstance>>
			{
				[1] = new List<CardInstance>(),
				[2] = new List<CardInstance>(),
				[3] = new List<CardInstance>()
			}
		};
	}

	private static GameState CreateState(PlayerState p0, PlayerState p1)
	{
		p0.IsSimulation = true;
		p1.IsSimulation = true;
		return new GameState
		{
			Players = new List<PlayerState> { p0, p1 },
			ActivePlayerIndex = 0,
			TurnNumber = 1,
			TurnState = new TurnState { CurrentPhase = TurnPhase.Order },
			Priority = new PriorityState
			{
				ActivePlayerIndex = 0,
				IsReactWindowOpen = false,
				ConsecutivePasses = 0,
				HasPassed = false
			},
			IsSimulation = true,
			AutoPassWhenNoReactsAvailable = true
		};
	}

	private CardInstance CreateCharacter(CardDefinition def, int instanceId, int ownerIndex, int rank)
	{
		return new CardInstance
		{
			InstanceId = instanceId,
			DefinitionId = def.DefinitionId,
			CurrentHitPoints = def.BaseHitPoints ?? 2,
			CurrentArmorClass = def.ArmorClass ?? 10,
			CurrentSkill = def.Skill ?? def.Level,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = ownerIndex,
			Rank = rank,
			SkillRatings = new Dictionary<string, int>()
		};
	}

	private CardInstance CreateItem(CardDefinition def, int instanceId, int ownerIndex, int attachedToId)
	{
		return new CardInstance
		{
			InstanceId = instanceId,
			DefinitionId = def.DefinitionId,
			CurrentHitPoints = def.BaseHitPoints ?? 0,
			CurrentArmorClass = def.ArmorClass ?? 0,
			CurrentSkill = 0,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Attached,
			OwnerPlayerIndex = ownerIndex,
			AttachedToInstanceId = attachedToId,
			SkillRatings = new Dictionary<string, int>()
		};
	}

	#region Bug 1: Attached Items Regression (UI Display, Legal Action Names, Scale Mail Target)

	[Fact]
	public void Bug1_EquippedItems_ShowInFormatCharacter_WhenInAttachedItems()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var warlordDef = GetDef("Church Enforcer Tancred");
		var itemDef = GetDef("Scale Mail");

		var warlord = CreateCharacter(warlordDef, 10, 0, 3);
		p0.WarlordInstanceId = 10;
		warlord.AttachedItemIds.Add(20);
		p0.Ranks[3].Add(warlord);

		var scaleMail = CreateItem(itemDef, 20, 0, 10);
		p0.AttachedItems.Add(scaleMail);

		var state = CreateState(p0, p1);

		string display = Program.FormatCharacter(warlord, p0, _cardDb, state);
		Assert.Contains("Scale Mail", display);
		Assert.Contains("[Items: Scale Mail]", display);
	}

	[Fact]
	public void Bug1_FormatUseAbility_ShowsItemNameAndDescription_ForAttachedItem()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var charDef = GetDef("Church Inquisitor");
		var itemDef = GetDef("Scepter of Djarat");

		var bearer = CreateCharacter(charDef, 10, 0, 1);
		bearer.AttachedItemIds.Add(20);
		p0.Ranks[1].Add(bearer);

		var scepter = CreateItem(itemDef, 20, 0, 10);
		p0.AttachedItems.Add(scepter);

		var state = CreateState(p0, p1);

		var useAbilityAction = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 20,
			AbilityIndex = 0,
			ChoiceIndex = 0,
			TargetInstanceIds = new int[] { 10 }
		};

		string actionText = Program.FormatAction(useAbilityAction, state, _cardDb);
		Assert.DoesNotContain("Use ability #1 on Card #20", actionText);
		Assert.Contains("Scepter of Djarat", actionText);
	}

	[Fact]
	public void Bug1_ScaleMail_BuffsOnlyEquippedWarlord_NotRank1Unit()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var warlordDef = GetDef("Church Enforcer Tancred");
		var rank1Def = GetDef("Aureliane");
		var scaleMailDef = GetDef("Scale Mail");

		var warlord = CreateCharacter(warlordDef, 10, 0, 3);
		p0.WarlordInstanceId = 10;
		p0.Ranks[3].Add(warlord);

		var rank1Char = CreateCharacter(rank1Def, 11, 0, 1);
		p0.Ranks[1].Add(rank1Char);

		var scaleMailCard = new CardInstance
		{
			InstanceId = 99,
			DefinitionId = scaleMailDef.DefinitionId,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready
		};
		p0.Hand.Add(scaleMailCard);

		var state = CreateState(p0, p1);
		var engine = new GameEngine(state, _cardDb);

		var playAction = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 99,
			TargetCharacterId = 10
		};

		var result = engine.Execute(playAction);
		Assert.True(result.IsSuccess, string.Join(", ", result.Errors));

		var warlordInState = p0.Ranks[3].First(c => c.InstanceId == 10);
		var rank1InState = p0.Ranks[1].First(c => c.InstanceId == 11);

		Assert.Contains(99, warlordInState.AttachedItemIds);
		Assert.DoesNotContain(99, rank1InState.AttachedItemIds);

		var rangedCombat = new CombatContext { IsDefending = true, IsRanged = true, IsMelee = false };

		int warlordRangedAc = GameEngine.GetEffectiveArmorClass(state, warlordInState, rangedCombat, _cardDb);
		int rank1RangedAc = GameEngine.GetEffectiveArmorClass(state, rank1InState, rangedCombat, _cardDb);

		Assert.Equal((warlordDef.ArmorClass ?? 15) + 5, warlordRangedAc);
		Assert.Equal(rank1Def.ArmorClass ?? 10, rank1RangedAc);
	}

	#endregion

	#region Bug 2: March vs. Journey Terminology & Relative Slot Selection

	[Fact]
	public void Bug2_MarchWithinRank_IsMarchAction_CrossRankIsJourneyAction()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var def1 = GetDef("Church Inquisitor");
		var def2 = GetDef("Aureliane");

		var char1 = CreateCharacter(def1, 10, 0, 2);
		var char2 = CreateCharacter(def2, 11, 0, 2);
		p0.Ranks[2].Add(char1);
		p0.Ranks[2].Add(char2);

		var frontUnit = CreateCharacter(def1, 12, 0, 1);
		p0.Ranks[1].Add(frontUnit);

		var state = CreateState(p0, p1);
		var engine = new GameEngine(state, _cardDb);

		var legalActions = engine.GetLegalActions(0);

		var marchWithin = legalActions.OfType<MarchAction>().Where(a => a.CardInstanceId == 10 && a.SourceRank == a.DestinationRank).ToList();
		Assert.NotEmpty(marchWithin);
		Assert.All(marchWithin, a => Assert.Equal(typeof(MarchAction), a.GetType()));

		var crossRankMoves = legalActions.OfType<JourneyAction>().Where(a => a.CardInstanceId == 10).ToList();
		Assert.NotEmpty(crossRankMoves);
		Assert.All(crossRankMoves, a => Assert.NotEqual(a.SourceRank, a.DestinationRank));
	}

	[Fact]
	public void Bug2_GetPlacementDescription_GeneratesRelativeSlotDescriptions()
	{
		var p0 = CreatePlayer(0, "P0");
		var def1 = GetDef("Church Inquisitor");
		var def2 = GetDef("Aureliane");

		var c1 = CreateCharacter(def1, 1, 0, 1);
		var c2 = CreateCharacter(def2, 2, 0, 1);
		p0.Ranks[1].Add(c1);
		p0.Ranks[1].Add(c2);

		string leftDesc = Program.GetPlacementDescription(1, 0, 99, p0, _cardDb);
		Assert.Equal("Left of Church Inquisitor", leftDesc);

		string betweenDesc = Program.GetPlacementDescription(1, 1, 99, p0, _cardDb);
		Assert.Equal("Between Church Inquisitor and Aureliane", betweenDesc);

		string rightDesc = Program.GetPlacementDescription(1, 2, 99, p0, _cardDb);
		Assert.Equal("Right of Aureliane", rightDesc);

		string emptyRankDesc = Program.GetPlacementDescription(3, 0, 99, p0, _cardDb);
		Assert.Equal("Into Rank 3", emptyRankDesc);
	}

	[Fact]
	public void Bug2_InRankMarch_NoOpBackToCurrentSlot_IsRejected()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var def1 = GetDef("Church Inquisitor");
		var def2 = GetDef("Aureliane");

		var c1 = CreateCharacter(def1, 10, 0, 1);
		var c2 = CreateCharacter(def2, 11, 0, 1);
		p0.Ranks[1].Add(c1);
		p0.Ranks[1].Add(c2);

		var state = CreateState(p0, p1);
		var engine = new GameEngine(state, _cardDb);

		var noOpMarch = new MarchAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			SourceRank = 1,
			DestinationRank = 1,
			TargetSlotIndex = 0
		};

		var result = engine.CanExecute(noOpMarch);
		Assert.False(result.IsSuccess);
		Assert.Contains(result.Errors, e => e.Contains("position", StringComparison.OrdinalIgnoreCase));

		var validMarch = new MarchAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			SourceRank = 1,
			DestinationRank = 1,
			TargetSlotIndex = 1
		};
		Assert.True(engine.CanExecute(validMarch).IsSuccess);
	}

	#endregion

	#region Bug 3: Mandatory Strike Targeting & Friendly Fire

	[Fact]
	public void Bug3_MeleeStrike_CalculatesDistance1_ForFriendlyCharactersInSameRankOrAdjacent()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var def = GetDef("Church Inquisitor");

		var attacker = CreateCharacter(def, 10, 0, 3);
		var allySameRank = CreateCharacter(def, 11, 0, 3);
		var allyAdjRank = CreateCharacter(def, 12, 0, 2);
		var allyDist2Rank = CreateCharacter(def, 13, 0, 1);

		p0.Ranks[3].Add(attacker);
		p0.Ranks[3].Add(allySameRank);
		p0.Ranks[2].Add(allyAdjRank);
		p0.Ranks[1].Add(allyDist2Rank);

		var state = CreateState(p0, p1);

		var strikeEffect = new PerformStrikeEffect(StrikeBonus: 20);

		var ctxSameRank = new EffectContext
		{
			SourceInstanceId = 10,
			TargetInstanceId = 11,
			State = state,
			CardDb = _cardDb,
			RollDice = (min, max) => 15
		};
		var eventsSameRank = strikeEffect.Evaluate(ctxSameRank).ToList();
		Assert.Contains(eventsSameRank, e => e is DealDamageEvent dmg && dmg.TargetInstanceId == 11);

		var ctxAdjRank = new EffectContext
		{
			SourceInstanceId = 10,
			TargetInstanceId = 12,
			State = state,
			CardDb = _cardDb,
			RollDice = (min, max) => 15
		};
		var eventsAdjRank = strikeEffect.Evaluate(ctxAdjRank).ToList();
		Assert.Contains(eventsAdjRank, e => e is DealDamageEvent dmg && dmg.TargetInstanceId == 12);

		var ctxDist2 = new EffectContext
		{
			SourceInstanceId = 10,
			TargetInstanceId = 13,
			State = state,
			CardDb = _cardDb,
			RollDice = (min, max) => 15
		};
		var eventsDist2 = strikeEffect.Evaluate(ctxDist2).ToList();
		Assert.Empty(eventsDist2);
	}

	[Fact]
	public void Bug3_ChurchEnforcerTancred_ForcesFriendlyFire_WhenNoEnemyInRange()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var tancredDef = GetDef("Church Enforcer Tancred");
		var allyDef = GetDef("Church Inquisitor");

		var tancred = CreateCharacter(tancredDef, 10, 0, 1);
		var r1Ally1 = CreateCharacter(allyDef, 11, 0, 1);
		var r1Ally2 = CreateCharacter(allyDef, 12, 0, 1);
		p0.Ranks[1].Add(tancred);
		p0.Ranks[1].Add(r1Ally1);
		p0.Ranks[1].Add(r1Ally2);

		var r2Ally1 = CreateCharacter(allyDef, 15, 0, 2);
		var r2Ally2 = CreateCharacter(allyDef, 16, 0, 2);
		p0.Ranks[2].Add(r2Ally1);
		p0.Ranks[2].Add(r2Ally2);

		var warlordInRank3 = CreateCharacter(allyDef, 20, 0, 3);
		var allyInRank3 = CreateCharacter(allyDef, 21, 0, 3);
		allyInRank3.CurrentHitPoints = 10;
		p0.Ranks[3].Add(warlordInRank3);
		p0.Ranks[3].Add(allyInRank3);

		var enemyInRank1 = CreateCharacter(allyDef, 30, 1, 1);
		p1.Ranks[1].Add(enemyInRank1);

		var state = CreateState(p0, p1);
		var engine = new GameEngine(state, _cardDb, (count, sides) => 15);

		var tancredAction = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 10,
			AbilityIndex = 0,
			ChoiceIndex = 0,
			TargetInstanceIds = new int[] { 20, 21 }
		};

		var result = engine.Execute(tancredAction);
		Assert.True(result.IsSuccess, string.Join(", ", result.Errors));

		var struckAlly = CardLocator.FindCard(state, 21);
		Assert.True(struckAlly.HasValue);
		Assert.True(struckAlly.Value.Wounds > 0, $"Ally in Rank 3 should suffer friendly fire. Wounds: {struckAlly.Value.Wounds}, HP: {struckAlly.Value.CurrentHitPoints}");
	}

	[Fact]
	public void Bug3_AlraynGaunt_JourneyToRank1_PromptsStrikeTargetSelection()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var alraynDef = GetDef("Alrayn Gaunt");
		var enemyDef = GetDef("Church Inquisitor");

		var alrayn = CreateCharacter(alraynDef, 10, 0, 2);
		p0.Ranks[2].Add(alrayn);

		var enemy = CreateCharacter(enemyDef, 20, 1, 1);
		p1.Ranks[1].Add(enemy);

		var state = CreateState(p0, p1);
		var engine = new GameEngine(state, _cardDb);

		var journeyAction = new JourneyAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			SourceRank = 2,
			DestinationRank = 1,
			TargetSlotIndex = 0
		};

		var journeyResult = engine.Execute(journeyAction);
		Assert.True(journeyResult.IsSuccess, string.Join(", ", journeyResult.Errors));
		Assert.True(state.Priority.IsReactWindowOpen, "React window should open for Alrayn Gaunt's AfterMove react.");

		if (state.Priority.ActivePlayerIndex != 0)
		{
			engine.Execute(new PassPriorityAction { PlayerIndex = state.Priority.ActivePlayerIndex });
		}

		var reacts = engine.GetLegalActions(state, 0).OfType<UseAbilityAction>().Where(a => a.SourceInstanceId == 10).ToList();
		Assert.NotEmpty(reacts);

		Assert.Contains(reacts, r => r.TargetInstanceIds != null && r.TargetInstanceIds.Contains(20));
	}

	[Fact]
	public void Bug3_AI_AvoidsFriendlyFireTargeting()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var tancredDef = GetDef("Church Enforcer Tancred");
		var allyDef = GetDef("Church Inquisitor");

		var tancred = CreateCharacter(tancredDef, 10, 1, 1);
		var warlordInRank3 = CreateCharacter(allyDef, 20, 1, 3);
		var allyInRank3 = CreateCharacter(allyDef, 21, 1, 3);
		p1.WarlordInstanceId = 20;

		p1.Ranks[1].Add(tancred);
		p1.Ranks[3].Add(warlordInRank3);
		p1.Ranks[3].Add(allyInRank3);

		var enemyFront = CreateCharacter(allyDef, 30, 0, 1);
		p0.Ranks[1].Add(enemyFront);

		var state = CreateState(p0, p1);
		state.ActivePlayerIndex = 1;
		state.Priority.ActivePlayerIndex = 1;

		var engine = new GameEngine(state, _cardDb);
		var ai = new HeuristicAiAgent();

		var chosenAction = ai.SelectAction(state, engine, 1);
		Assert.NotNull(chosenAction);

		if (chosenAction is UseAbilityAction uaa && uaa.SourceInstanceId == 10)
		{
			Assert.False(uaa.TargetInstanceIds != null && uaa.TargetInstanceIds.Length > 1 && uaa.TargetInstanceIds[1] == 21,
				"AI should avoid picking a target that forces friendly fire on ally 21.");
		}
	}

	#endregion

	#region Bug 4: Movement Reacts & Card-Driven Movement (Tunnel Worm, Swift, Sister Kiren)

	[Fact]
	public void Bug4_TunnelWorm_EmitsCharacterMovedEvent_AndTriggersAfterMoveReact()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var wormDef = GetDef("Tunnel Worm");
		var alraynDef = GetDef("Alrayn Gaunt");

		var worm = CreateCharacter(wormDef, 10, 0, 1);
		var alrayn = CreateCharacter(alraynDef, 11, 0, 1);
		p0.Ranks[1].Add(worm);
		p0.Ranks[1].Add(alrayn);

		var state = CreateState(p0, p1);

		var repositionEffect = new RepositionInRankEffect();
		var ctx = new EffectContext
		{
			SourceInstanceId = 11,
			TargetInstanceId = 11,
			State = state,
			CardDb = _cardDb
		};

		var events = repositionEffect.Evaluate(ctx).ToList();
		Assert.NotEmpty(events);
		Assert.IsType<CharacterMovedEvent>(events[0]);
		Assert.True(EventResolver.RequiresReactWindow(events[0]));
	}

	[Fact]
	public void Bug4_Swift_MovesCharacterForward_AndTriggersAfterMoveReact()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var alraynDef = GetDef("Alrayn Gaunt");

		var alrayn = CreateCharacter(alraynDef, 10, 0, 2);
		p0.Ranks[2].Add(alrayn);

		var state = CreateState(p0, p1);

		var journeyEffect = new JourneyEffect();
		var ctx = new EffectContext
		{
			SourceInstanceId = 10,
			TargetInstanceId = 10,
			State = state,
			CardDb = _cardDb
		};

		var events = journeyEffect.Evaluate(ctx).ToList();
		Assert.NotEmpty(events);

		var moveEvt = events.OfType<CharacterMovedEvent>().FirstOrDefault();
		Assert.NotNull(moveEvt);
		Assert.Equal(2, moveEvt.SourceRank);
		Assert.Equal(1, moveEvt.DestinationRank);
		Assert.True(EventResolver.RequiresReactWindow(moveEvt));
	}

	[Fact]
	public void Bug4_SisterKiren_TargetFilter_StrictlyRequiresHigherLevelDeverenian_CannotTargetSelf()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var kirenDef = GetDef("Sister Kiren"); // Level 3 Evil Deverenian Cleric
		var lowerDef = GetDef("Aureliane"); // Level 1 Good Deverenian Cleric
		var higherDeverenianDef = GetDef("Church Enforcer Tancred"); // Level 5 Evil Deverenian Fighter
		var higherNonDeverenianDef = GetDef("Cloudracer"); // Non-Deverenian

		var kiren = CreateCharacter(kirenDef, 10, 0, 2);
		var lowerAlly = CreateCharacter(lowerDef, 11, 0, 2);
		var higherDeverenian = CreateCharacter(higherDeverenianDef, 12, 0, 2);

		p0.Ranks[2].Add(kiren);
		p0.Ranks[2].Add(lowerAlly);
		p0.Ranks[2].Add(higherDeverenian);

		var state = CreateState(p0, p1);
		var engine = new GameEngine(state, _cardDb);

		var legalActions = engine.GetLegalActions(0).OfType<UseAbilityAction>().Where(a => a.SourceInstanceId == 10).ToList();
		Assert.NotEmpty(legalActions);

		Assert.DoesNotContain(legalActions, a => a.TargetInstanceIds != null && a.TargetInstanceIds.Contains(10));
		Assert.DoesNotContain(legalActions, a => a.TargetInstanceIds != null && a.TargetInstanceIds.Contains(11));

		Assert.Contains(legalActions, a => a.TargetInstanceIds != null && a.TargetInstanceIds.Contains(12));
	}

	#endregion

	#region Bug 5: Church Enforcer Tancred AC 13 vs 15 Bug

	[Fact]
	public void Bug5_ChurchEnforcerTancred_RetainsPrintedAc15_InMeleeDefense()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var tancredDef = GetDef("Church Enforcer Tancred");
		Assert.Equal(15, tancredDef.ArmorClass);

		var tancred = CreateCharacter(tancredDef, 10, 0, 1);
		p0.Ranks[1].Add(tancred);

		var state = CreateState(p0, p1);

		var meleeDefenseCombat = new CombatContext
		{
			IsDefending = true,
			IsMelee = true,
			IsRanged = false
		};

		int effectiveAc = GameEngine.GetEffectiveArmorClass(state, tancred, meleeDefenseCombat, _cardDb);
		Assert.Equal(15, effectiveAc);

		tancred.ReadyState = ReadyState.Stunned;
		int stunnedMeleeAc = GameEngine.GetEffectiveArmorClass(state, tancred, meleeDefenseCombat, _cardDb);
		Assert.Equal(15, stunnedMeleeAc);
	}

	#endregion

	#region Bug 6: Another Tavern Brawl, Stirges Timing, and Assassin's Strike Second Wound

	[Fact]
	public void Bug6_AnotherTavernBrawl_AllowsCrossBoardStrikes_RegardlessOfRank()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var def = GetDef("Church Inquisitor");

		var rank4Performer = CreateCharacter(def, 10, 0, 4);
		p0.Ranks[4] = new List<CardInstance> { rank4Performer };

		var rank1Opponent = CreateCharacter(def, 20, 1, 1);
		p1.Ranks[1].Add(rank1Opponent);

		var state = CreateState(p0, p1);

		var brawlEffect = new AnotherTavernBrawlEffect();
		var ctx = new EffectContext
		{
			SourceInstanceId = 10,
			TargetInstanceId = 20,
			State = state,
			CardDb = _cardDb,
			RollDice = (min, max) => 20
		};

		var events = brawlEffect.Evaluate(ctx).ToList();
		Assert.NotEmpty(events);
		var damages = events.OfType<DealDamageEvent>().ToList();
		Assert.Contains(damages, d => d.TargetInstanceId == 10);
		Assert.Contains(damages, d => d.TargetInstanceId == 20);
	}

	[Fact]
	public void Bug6_Stirges_ReactTriggersAfterStrikeHits_NotBeforeRoll()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var stirgesDef = GetDef("Stirges");
		var enemyDef = GetDef("Church Inquisitor");

		var stirges = CreateCharacter(stirgesDef, 10, 0, 1);
		p0.Ranks[1].Add(stirges);

		var enemy = CreateCharacter(enemyDef, 20, 1, 1);
		p1.Ranks[1].Add(enemy);

		var state = CreateState(p0, p1);
		var engine = new GameEngine(state, _cardDb);

		var declareAttackEvt = new DeclareAttackEvent
		{
			AttackerInstanceId = 10,
			DefenderInstanceId = 20
		};
		state.PendingEvents.Add(declareAttackEvt);

		var stirgesAbility = stirgesDef.Abilities.First(a => a.Timing == ReactTiming.AfterStrikeHit);
		bool canReactPreRoll = engine.IsReactTimingAndConditionMet(stirgesAbility, stirges, state, null, out string _);
		Assert.False(canReactPreRoll, "Stirges React should NOT trigger before strike roll on DeclareAttackEvent.");

		state.PendingEvents.Clear();
		var hitDamageEvt = new DealDamageEvent
		{
			SourceInstanceId = 10,
			TargetInstanceId = 20,
			Amount = 1,
			DamageType = "melee"
		};
		state.PendingEvents.Add(hitDamageEvt);

		bool canReactPostHit = engine.IsReactTimingAndConditionMet(stirgesAbility, stirges, state, null, out string _);
		Assert.True(canReactPostHit, "Stirges React MUST trigger after strike hit on DealDamageEvent.");
	}

	[Fact]
	public void Bug6_AssassinsStrike_GrantsAdvantageAndInflictsSecondWound()
	{
		var p0 = CreatePlayer(0, "P0");
		var p1 = CreatePlayer(1, "P1");
		var rogueDef = GetDef("Alrayn Gaunt");
		var targetDef = GetDef("Church Inquisitor");
		var strikeDef = GetDef("Assassin's Strike");

		var rogue = CreateCharacter(rogueDef, 10, 0, 1);
		p0.Ranks[1].Add(rogue);

		var target = CreateCharacter(targetDef, 20, 1, 1);
		target.ReadyState = ReadyState.Spent;
		p1.Ranks[1].Add(target);

		var strikeCard = new CardInstance
		{
			InstanceId = 99,
			DefinitionId = strikeDef.DefinitionId,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		p0.Hand.Add(strikeCard);

		var state = CreateState(p0, p1);
		var engine = new GameEngine(state, _cardDb, (count, sides) => 15);

		var playAction = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 99,
			PerformerInstanceId = 10,
			TargetCharacterId = 20
		};

		var result = engine.Execute(playAction);
		Assert.True(result.IsSuccess, string.Join(", ", result.Errors));

		Assert.True(GameEngine.HasTurnBuff(state, 10, "ADVANTAGE"));
		Assert.True(GameEngine.HasTurnBuff(state, 10, "WOUND_BONUS"));

		var bonuses = GameEngine.GetContinuousPassiveBonuses(state, rogue, new CombatContext { IsAttacking = true, IsMelee = true, OpponentCard = target }, _cardDb);
		Assert.Equal(1, bonuses.AdditionalWounds);
	}

	#endregion
}
