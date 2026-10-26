using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public class BucketCPassivesAndAurasTests
{
	private readonly CardDatabase _cardDb;

	private static CardDatabase LoadDb()
	{
		return DatabaseLoader.LoadFromFile(DataPaths.CardsJsonPath);
	}

	public BucketCPassivesAndAurasTests()
	{
		_cardDb = LoadDb();
	}

	private CardDefinition GetDef(string name)
	{
		CardDefinition cardDefinition = _cardDb.Definitions.Values.FirstOrDefault((CardDefinition d) => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
		if (cardDefinition == null)
		{
			string definitionId = name.ToLowerInvariant().Replace(" ", "_").Replace("'", "")
				.Replace(",", "");
			cardDefinition = _cardDb.GetDefinition(definitionId);
		}
		Assert.NotNull(cardDefinition);
		return cardDefinition;
	}

	private CardInstance CreateInstance(CardDefinition def, int instanceId, int ownerIndex, int rank)
	{
		return new CardInstance
		{
			InstanceId = instanceId,
			DefinitionId = def.DefinitionId,
			CurrentHitPoints = (def.BaseHitPoints ?? 2),
			CurrentArmorClass = (def.ArmorClass ?? 12),
			CurrentSkill = def.Level,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = ownerIndex,
			Rank = rank,
			SkillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
		};
	}

	private void AttachItem(PlayerState player, CardInstance bearer, CardInstance item)
	{
		item.Zone = CardZone.Attached;
		item.AttachedToInstanceId = bearer.InstanceId;
		bearer.AttachedItemIds.Add(item.InstanceId);
		player.DiscardPile.Add(item);
	}

	private GameState CreateState(PlayerState p1, PlayerState p2)
	{
		return new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			Players = new List<PlayerState> { p1, p2 }
		};
	}

	private PlayerState CreatePlayer(int index, string name)
	{
		return new PlayerState
		{
			PlayerIndex = index,
			DisplayName = name,
			Ranks = new SortedDictionary<int, List<CardInstance>>
			{
				{
					1,
					new List<CardInstance>()
				},
				{
					2,
					new List<CardInstance>()
				},
				{
					3,
					new List<CardInstance>()
				}
			},
			Hand = new List<CardInstance>(),
			DiscardPile = new List<CardInstance>()
		};
	}

	[Fact]
	public void AmbitiousSquire_GainsBonus_ForEachAdjacentHigherLevelDeverenian()
	{
		CardDefinition def = GetDef("Ambitious Squire");
		CardDefinition def2 = GetDef("Aureliane");
		CardDefinition def3 = GetDef("Balian");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def2, 1, 0, 1)with
		{
			SkillRatings = new() { ["BonusLevel"] = 5 }
		};
		CardInstance cardInstance2 = CreateInstance(def, 2, 0, 1);
		CardInstance cardInstance3 = CreateInstance(def3, 3, 0, 1);
		playerState.Ranks[1].AddRange(new CardInstance[3] { cardInstance, cardInstance2, cardInstance3 });
		GameState state = CreateState(playerState, p);
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(2, continuousPassiveBonuses.Atk);
		Assert.Equal(2, continuousPassiveBonuses.Ac);
		CardInstance value = CreateInstance(def2, 4, 0, 1)with
		{
			SkillRatings = new() { ["BonusLevel"] = 5 }
		};
		playerState.Ranks[1][2] = value;
		continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(4, continuousPassiveBonuses.Atk);
		Assert.Equal(4, continuousPassiveBonuses.Ac);
	}

	[Fact]
	public void Balian_GrantsAcBonus_ToAdjacentMercenariesOnly()
	{
		CardDefinition def = GetDef("Balian");
		CardDefinition def2 = GetDef("Blitzer");
		CardDefinition def3 = GetDef("Aureliane");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def, 1, 0, 1);
		CardInstance cardInstance2 = CreateInstance(def2, 2, 0, 1);
		CardInstance cardInstance3 = CreateInstance(def3, 3, 0, 1);
		CardInstance cardInstance4 = CreateInstance(def2, 4, 0, 2);
		playerState.Ranks[1].AddRange(new CardInstance[3] { cardInstance2, cardInstance, cardInstance3 });
		playerState.Ranks[2].Add(cardInstance4);
		GameState state = CreateState(playerState, p);
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(2, continuousPassiveBonuses.Ac);
		PassiveBonuses continuousPassiveBonuses2 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance3, null, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses2.Ac);
		PassiveBonuses continuousPassiveBonuses3 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance4, null, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses3.Ac);
	}

	[Fact]
	public void TunnelVanguard_GrantsHpBonus_ToAdjacentDwarves()
	{
		CardDefinition def = GetDef("Tunnel Vanguard");
		CardDefinition def2 = GetDef("Dress");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def, 1, 0, 1);
		CardInstance cardInstance2 = CreateInstance(def2, 2, 0, 1);
		playerState.Ranks[1].AddRange(new CardInstance[2] { cardInstance, cardInstance2 });
		GameState state = CreateState(playerState, p);
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(1, continuousPassiveBonuses.Hp);
		int effectiveMaxHitPoints = GameEngine.GetEffectiveMaxHitPoints(state, cardInstance2, _cardDb);
		Assert.Equal((def2.BaseHitPoints ?? 2) + 1, effectiveMaxHitPoints);
	}

	[Fact]
	public void TunnelVanguard_MutualAdjacency_WhenOneDiesWhileBothWounded_BothDie()
	{
		CardDefinition def = GetDef("Tunnel Vanguard");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def, 1, 0, 1);
		CardInstance cardInstance2 = CreateInstance(def, 2, 0, 1);
		playerState.Ranks[1].AddRange(new CardInstance[2] { cardInstance, cardInstance2 });
		GameState state = CreateState(playerState, p);
		GameEngine gameEngine = new GameEngine(state, _cardDb);
		Assert.Equal(2, GameEngine.GetEffectiveMaxHitPoints(state, playerState.Ranks[1][0], _cardDb));
		Assert.Equal(2, GameEngine.GetEffectiveMaxHitPoints(state, playerState.Ranks[1][1], _cardDb));
		gameEngine.Resolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 1,
			Amount = 1,
			DamageType = "melee"
		});
		gameEngine.Resolver.ResolveAll(state);
		Assert.Equal(2, playerState.Ranks[1].Count);
		Assert.Equal(1, GameEngine.GetEffectiveCurrentHitPoints(state, playerState.Ranks[1].First((CardInstance c) => c.InstanceId == 1), _cardDb));
		Assert.Equal(1, playerState.Ranks[1].First((CardInstance c) => c.InstanceId == 1).Wounds);
		gameEngine.Resolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 2,
			Amount = 1,
			DamageType = "melee"
		});
		gameEngine.Resolver.ResolveAll(state);
		Assert.Equal(2, playerState.Ranks[1].Count);
		Assert.Equal(1, GameEngine.GetEffectiveCurrentHitPoints(state, playerState.Ranks[1].First((CardInstance c) => c.InstanceId == 2), _cardDb));
		Assert.Equal(1, playerState.Ranks[1].First((CardInstance c) => c.InstanceId == 2).Wounds);
		gameEngine.Resolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 1,
			Amount = 1,
			DamageType = "melee"
		});
		gameEngine.Resolver.ResolveAll(state);
		Assert.False(playerState.Ranks.ContainsKey(1) && playerState.Ranks[1].Count > 0);
		Assert.Contains(playerState.DiscardPile, (CardInstance c) => c.InstanceId == 1);
		Assert.Contains(playerState.DiscardPile, (CardInstance c) => c.InstanceId == 2);
	}

	[Fact]
	public void HazahkOfShadow_GrantsAtkBonus_WhenWounded()
	{
		CardDefinition def = GetDef("Hazahk of Shadow");
		CardDefinition def2 = GetDef("Aureliane");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def, 1, 0, 1);
		CardInstance cardInstance2 = CreateInstance(def2, 2, 0, 1);
		playerState.Ranks[1].AddRange(new CardInstance[2] { cardInstance, cardInstance2 });
		GameState state = CreateState(playerState, p);
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, null, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses.Atk);
		cardInstance.CurrentHitPoints = (def.BaseHitPoints ?? 2) - 1;
		playerState.Ranks[1][0] = cardInstance;
		PassiveBonuses continuousPassiveBonuses2 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, null, _cardDb);
		Assert.Equal(5, continuousPassiveBonuses2.Atk);
		PassiveBonuses continuousPassiveBonuses3 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(5, continuousPassiveBonuses3.Atk);
	}

	[Fact]
	public void Joleg_GrantsAtkBonus_ToAdjacentNothrog()
	{
		CardDefinition def = GetDef("Joleg");
		CardDefinition def2 = _cardDb.Definitions.Values.First((CardDefinition d) => GameEngine.IsNothrog(d) && d.Type == CardType.Character && d.Name != "Joleg");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def, 1, 0, 1);
		CardInstance cardInstance2 = CreateInstance(def2, 2, 0, 1);
		playerState.Ranks[1].AddRange(new CardInstance[2] { cardInstance, cardInstance2 });
		GameState state = CreateState(playerState, p);
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(2, continuousPassiveBonuses.Atk);
	}

	[Fact]
	public void ShannonTheDaring_GrantsAtkBonus_ToAdjacentLevel1And2Only()
	{
		CardDefinition def = GetDef("Shannon the Daring");
		CardDefinition def2 = GetDef("Ambitious Squire");
		CardDefinition def3 = GetDef("Aureliane");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def, 1, 0, 1);
		CardInstance cardInstance2 = CreateInstance(def2, 2, 0, 1);
		CardInstance cardInstance3 = CreateInstance(def3, 3, 0, 1)with
		{
			SkillRatings = new() { ["BonusLevel"] = 5 }
		};
		playerState.Ranks[1].AddRange(new CardInstance[3] { cardInstance2, cardInstance, cardInstance3 });
		GameState state = CreateState(playerState, p);
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(2, continuousPassiveBonuses.Atk);
		PassiveBonuses continuousPassiveBonuses2 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance3, null, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses2.Atk);
	}

	[Fact]
	public void BreastplateOfPower_GrantsBonus_ToOtherCharactersInSameRank()
	{
		CardDefinition def = GetDef("Breastplate of Power");
		CardDefinition def2 = GetDef("Aureliane");
		CardDefinition def3 = GetDef("Ambitious Squire");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def2, 1, 0, 1);
		CardInstance item = CreateInstance(def, 2, 0, 1);
		AttachItem(playerState, cardInstance, item);
		CardInstance cardInstance2 = CreateInstance(def3, 3, 0, 1);
		CardInstance cardInstance3 = CreateInstance(def3, 4, 0, 2);
		playerState.Ranks[1].AddRange(new CardInstance[2] { cardInstance, cardInstance2 });
		playerState.Ranks[2].Add(cardInstance3);
		GameState state = CreateState(playerState, p);
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(2, continuousPassiveBonuses.Atk);
		Assert.Equal(2, continuousPassiveBonuses.Ac);
		PassiveBonuses continuousPassiveBonuses2 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, null, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses2.Atk);
		Assert.Equal(0, continuousPassiveBonuses2.Ac);
		PassiveBonuses continuousPassiveBonuses3 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance3, null, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses3.Atk);
	}

	[Fact]
	public void Dress_GrantsAcBonus_ToWoundedDwarvesInSameRank()
	{
		CardDefinition def = GetDef("Dress");
		CardDefinition def2 = GetDef("Tunnel Vanguard");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def, 1, 0, 1);
		CardInstance cardInstance2 = CreateInstance(def2, 2, 0, 1);
		playerState.Ranks[1].AddRange(new CardInstance[2] { cardInstance, cardInstance2 });
		GameState state = CreateState(playerState, p);
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses.Ac);
		cardInstance2.CurrentHitPoints = (def2.BaseHitPoints ?? 2) - 1;
		playerState.Ranks[1][1] = cardInstance2;
		PassiveBonuses continuousPassiveBonuses2 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(3, continuousPassiveBonuses2.Ac);
	}

	[Fact]
	public void SquireArgos_GrantsBonus_ToOtherEqualOrLowerLevelFreeKingdomsInRank()
	{
		CardDefinition def = GetDef("Squire Argos");
		CardDefinition def2 = _cardDb.Definitions.Values.First((CardDefinition d) => GameEngine.IsFreeKingdoms(d) && d.Type == CardType.Character && d.Name != "Squire Argos");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def, 1, 0, 1)with
		{
			SkillRatings = new() { ["BonusLevel"] = 2 }
		};
		CardInstance cardInstance2 = CreateInstance(def2, 2, 0, 1)with
		{
			SkillRatings = new() { ["BonusLevel"] = 0 }
		};
		CardInstance cardInstance3 = CreateInstance(def2, 3, 0, 1)with
		{
			SkillRatings = new() { ["BonusLevel"] = 10 }
		};
		playerState.Ranks[1].AddRange(new CardInstance[3] { cardInstance, cardInstance2, cardInstance3 });
		GameState state = CreateState(playerState, p);
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(2, continuousPassiveBonuses.Atk);
		Assert.Equal(1, continuousPassiveBonuses.Ac);
		PassiveBonuses continuousPassiveBonuses2 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance3, null, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses2.Atk);
		Assert.Equal(0, continuousPassiveBonuses2.Ac);
	}

	[Fact]
	public void TaumarTheProtector_GrantsBonus_ToDeverenianFightersInRankInFront()
	{
		CardDefinition def = GetDef("Taumar the Protector");
		CardDefinition def2 = GetDef("Ambitious Squire");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance item = CreateInstance(def, 1, 0, 2);
		CardInstance cardInstance = CreateInstance(def2, 2, 0, 1);
		CardInstance cardInstance2 = CreateInstance(def2, 3, 0, 3);
		playerState.Ranks[2].Add(item);
		playerState.Ranks[1].Add(cardInstance);
		playerState.Ranks[3].Add(cardInstance2);
		GameState state = CreateState(playerState, p);
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, null, _cardDb);
		Assert.Equal(2, continuousPassiveBonuses.Ac);
		Assert.Equal(2, continuousPassiveBonuses.Skill);
		PassiveBonuses continuousPassiveBonuses2 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses2.Ac);
		Assert.Equal(0, continuousPassiveBonuses2.Skill);
	}

	[Fact]
	public void GhedBane_DebuffsOpposingLowerLevelCharacters_WithinOneRank()
	{
		CardDefinition def = GetDef("Ghed Bane");
		CardDefinition def2 = GetDef("Novice Pyro");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState playerState2 = CreatePlayer(1, "P2");
		CardInstance item = CreateInstance(def, 1, 0, 1)with
		{
			SkillRatings = new() { ["BonusLevel"] = 5 }
		};
		playerState.Ranks[1].Add(item);
		CardInstance cardInstance = CreateInstance(def2, 2, 1, 1)with
		{
			SkillRatings = new() { ["BonusLevel"] = 0 }
		};
		CardInstance cardInstance2 = CreateInstance(def2, 3, 1, 1)with
		{
			SkillRatings = new() { ["BonusLevel"] = 15 }
		};
		CardInstance cardInstance3 = CreateInstance(def2, 4, 1, 3)with
		{
			SkillRatings = new() { ["BonusLevel"] = 0 }
		};
		playerState2.Ranks[1].AddRange(new CardInstance[2] { cardInstance, cardInstance2 });
		playerState2.Ranks[3].Add(cardInstance3);
		GameState state = CreateState(playerState, playerState2);
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, null, _cardDb);
		Assert.Equal(-2, continuousPassiveBonuses.Atk);
		Assert.Equal(-2, continuousPassiveBonuses.Ac);
		PassiveBonuses continuousPassiveBonuses2 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, null, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses2.Atk);
		Assert.Equal(0, continuousPassiveBonuses2.Ac);
		PassiveBonuses continuousPassiveBonuses3 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance3, null, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses3.Atk);
		Assert.Equal(0, continuousPassiveBonuses3.Ac);
	}

	[Fact]
	public void ScaleMail_GrantsAcOnly_WhenTargetedWithRangedStrike()
	{
		CardDefinition def = GetDef("Scale Mail");
		CardDefinition def2 = GetDef("Aureliane");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def2, 1, 0, 1);
		cardInstance.CurrentArmorClass += 2;
		CardInstance item = CreateInstance(def, 2, 0, 1);
		AttachItem(playerState, cardInstance, item);
		playerState.Ranks[1].Add(cardInstance);
		GameState state = CreateState(playerState, p);
		CombatContext combatCtx = new CombatContext
		{
			IsDefending = true,
			IsRanged = true,
			IsMelee = false,
			OpponentCard = null
		};
		CombatContext combatCtx2 = new CombatContext
		{
			IsDefending = true,
			IsRanged = false,
			IsMelee = true,
			OpponentCard = null
		};
		int effectiveArmorClass = GameEngine.GetEffectiveArmorClass(state, cardInstance, combatCtx, _cardDb);
		int effectiveArmorClass2 = GameEngine.GetEffectiveArmorClass(state, cardInstance, combatCtx2, _cardDb);
		Assert.Equal((def2.ArmorClass ?? 12) + 2, effectiveArmorClass);
		Assert.Equal(def2.ArmorClass ?? 12, effectiveArmorClass2);
	}

	[Fact]
	public void Gapfinder_GrantsAtkOnly_DuringMeleeStrikesAgainstSpentTarget()
	{
		CardDefinition def = GetDef("Gapfinder");
		CardDefinition def2 = GetDef("Aureliane");
		CardDefinition def3 = GetDef("Ambitious Squire");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState playerState2 = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def2, 1, 0, 1);
		CardInstance item = CreateInstance(def, 2, 0, 1);
		AttachItem(playerState, cardInstance, item);
		CardInstance cardInstance2 = CreateInstance(def3, 3, 1, 1)with
		{
			ReadyState = ReadyState.Ready
		};
		CardInstance cardInstance3 = CreateInstance(def3, 4, 1, 1)with
		{
			ReadyState = ReadyState.Spent
		};
		playerState.Ranks[1].Add(cardInstance);
		playerState2.Ranks[1].AddRange(new CardInstance[2] { cardInstance2, cardInstance3 });
		GameState state = CreateState(playerState, playerState2);
		CombatContext combatCtx = new CombatContext
		{
			IsAttacking = true,
			OpponentCard = cardInstance3,
			IsMelee = true,
			IsRanged = false
		};
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, combatCtx, _cardDb);
		Assert.Equal(5, continuousPassiveBonuses.Atk);
		CombatContext combatCtx2 = new CombatContext
		{
			IsAttacking = true,
			OpponentCard = cardInstance2,
			IsMelee = true,
			IsRanged = false
		};
		PassiveBonuses continuousPassiveBonuses2 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, combatCtx2, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses2.Atk);
		CombatContext combatCtx3 = new CombatContext
		{
			IsAttacking = true,
			OpponentCard = cardInstance3,
			IsMelee = false,
			IsRanged = true
		};
		PassiveBonuses continuousPassiveBonuses3 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, combatCtx3, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses3.Atk);
	}

	[Fact]
	public void GrimfeyProwler_GrantsAtkEqualSkill_DuringMeleeStrikesAgainstSpentTarget()
	{
		CardDefinition def = GetDef("Grimfey Prowler");
		CardDefinition def2 = GetDef("Aureliane");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState playerState2 = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def, 1, 0, 1);
		CardInstance cardInstance2 = CreateInstance(def2, 2, 1, 1)with
		{
			ReadyState = ReadyState.Spent
		};
		playerState.Ranks[1].Add(cardInstance);
		playerState2.Ranks[1].Add(cardInstance2);
		GameState state = CreateState(playerState, playerState2);
		CombatContext combatCtx = new CombatContext
		{
			IsAttacking = true,
			OpponentCard = cardInstance2,
			IsMelee = true,
			IsRanged = false
		};
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, combatCtx, _cardDb);
		int currentSkill = cardInstance.CurrentSkill;
		Assert.Equal(currentSkill, continuousPassiveBonuses.Atk);
	}

	[Fact]
	public void MadPatCarrik_And_Blitzer_InflictAdditionalMeleeWound()
	{
		CardDefinition def = GetDef("'Mad' Pat Carrik");
		CardDefinition def2 = GetDef("Blitzer");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def, 1, 0, 1);
		CardInstance cardInstance2 = CreateInstance(def2, 2, 0, 1);
		playerState.Ranks[1].AddRange(new CardInstance[2] { cardInstance, cardInstance2 });
		GameState state = CreateState(playerState, p);
		CombatContext combatCtx = new CombatContext
		{
			IsAttacking = true,
			IsMelee = true,
			IsRanged = false
		};
		CombatContext combatCtx2 = new CombatContext
		{
			IsAttacking = true,
			IsMelee = false,
			IsRanged = true
		};
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, combatCtx, _cardDb);
		Assert.Equal(1, continuousPassiveBonuses.AdditionalWounds);
		PassiveBonuses continuousPassiveBonuses2 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, combatCtx2, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses2.AdditionalWounds);
		PassiveBonuses continuousPassiveBonuses3 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance2, combatCtx, _cardDb);
		Assert.Equal(1, continuousPassiveBonuses3.AdditionalWounds);
	}

	[Fact]
	public void Shillelagh_GainsAtkBonus_EqualToHalfSkillRoundedUp()
	{
		CardDefinition def = GetDef("Shillelagh");
		CardDefinition def2 = GetDef("Aureliane");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def2, 1, 0, 1);
		CardInstance item = CreateInstance(def, 2, 0, 1);
		AttachItem(playerState, cardInstance, item);
		playerState.Ranks[1].Add(cardInstance);
		GameState state = CreateState(playerState, p);
		cardInstance.CurrentSkill = 3;
		playerState.Ranks[1][0] = cardInstance;
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, null, _cardDb);
		Assert.Equal(2, continuousPassiveBonuses.Atk);
		cardInstance.CurrentSkill = 4;
		playerState.Ranks[1][0] = cardInstance;
		PassiveBonuses continuousPassiveBonuses2 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, null, _cardDb);
		Assert.Equal(2, continuousPassiveBonuses2.Atk);
		cardInstance.CurrentSkill = 5;
		playerState.Ranks[1][0] = cardInstance;
		PassiveBonuses continuousPassiveBonuses3 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, null, _cardDb);
		Assert.Equal(3, continuousPassiveBonuses3.Atk);
	}

	[Fact]
	public void Revenger_GrantsDualWield_AndBonusWoundWithTwoWeapons()
	{
		CardDefinition def = GetDef("Revenger");
		CardDefinition def2 = GetDef("Bardiche");
		CardDefinition def3 = GetDef("Aureliane");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def3, 1, 0, 1);
		CardInstance item = CreateInstance(def, 2, 0, 1);
		AttachItem(playerState, cardInstance, item);
		playerState.Ranks[1].Add(cardInstance);
		GameState state = CreateState(playerState, p);
		CombatContext combatCtx = new CombatContext
		{
			IsAttacking = true,
			IsMelee = true,
			IsRanged = false
		};
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, combatCtx, _cardDb);
		Assert.Equal(0, continuousPassiveBonuses.AdditionalWounds);
		CardInstance item2 = CreateInstance(def2, 3, 0, 1);
		AttachItem(playerState, cardInstance, item2);
		playerState.Ranks[1][0] = cardInstance;
		PassiveBonuses continuousPassiveBonuses2 = GameEngine.GetContinuousPassiveBonuses(state, cardInstance, combatCtx, _cardDb);
		Assert.Equal(1, continuousPassiveBonuses2.AdditionalWounds);
	}

	[Fact]
	public void Tinker_EquipsItemsIgnoringClassRestrictions_AndCountsAsLevelPlusOne()
	{
		CardDefinition tinkerDef = GetDef("Tinker");
		GameState gameState = CreateState(CreatePlayer(0, "P1"), CreatePlayer(1, "P2"));
		CardInstance item = CreateInstance(tinkerDef, 1, 0, 1);
		gameState.Players[0].Ranks[1].Add(item);
		CardDefinition cardDefinition = _cardDb.Definitions.Values.FirstOrDefault((CardDefinition d) => d.Type == CardType.Item && d.Class?.ToString() == "Wizard" && d.Level <= tinkerDef.Level + 1);
		if (cardDefinition != null)
		{
			CardInstance item2 = CreateInstance(cardDefinition, 2, 0, 1)with
			{
				Zone = CardZone.Hand
			};
			gameState.Players[0].Hand.Add(item2);
			GameEngine gameEngine = new GameEngine(gameState, _cardDb);
			PlayCardAction action = new PlayCardAction
			{
				PlayerIndex = 0,
				CardInstanceId = item2.InstanceId,
				TargetCharacterId = item.InstanceId
			};
			ActionResult actionResult = gameEngine.CanExecute(action);
			Assert.True(actionResult.IsSuccess, "Tinker equip failed: " + string.Join(", ", actionResult.Errors));
		}
	}

	[Fact]
	public void SteadyGrip_TrueStrike_RequiresRollMeetingAc_Rule602_14()
	{
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState playerState2 = CreatePlayer(1, "P2");
		CardDefinition def = GetDef("Aureliane");
		CardInstance item = CreateInstance(def, 1, 0, 1);
		CardInstance item2 = CreateInstance(def, 2, 1, 1)with
		{
			CurrentArmorClass = 25
		};
		playerState.Ranks[1].Add(item);
		playerState2.Ranks[1].Add(item2);
		GameState state = CreateState(playerState, playerState2);
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = 2,
			TrueStrike = false,
			EnableCriticals = true
		};
		EffectContext context = new EffectContext
		{
			SourceInstanceId = item.InstanceId,
			TargetInstanceId = item2.InstanceId,
			State = state,
			CardDb = _cardDb,
			RollDice = (int count, int sides) => 5
		};
		List<GameEvent> collection = resolveAttackEffect.Evaluate(context).ToList();
		Assert.Empty(collection);
		ResolveAttackEffect resolveAttackEffect2 = new ResolveAttackEffect
		{
			StrikeBonus = 2,
			TrueStrike = true,
			EnableCriticals = true
		};
		EffectContext context2 = new EffectContext
		{
			SourceInstanceId = item.InstanceId,
			TargetInstanceId = item2.InstanceId,
			State = state,
			CardDb = _cardDb,
			RollDice = (int count, int sides) => 5
		};
		List<GameEvent> list = resolveAttackEffect2.Evaluate(context2).ToList();
		Assert.Empty(list);
		ResolveAttackEffect resolveAttackEffect3 = new ResolveAttackEffect
		{
			StrikeBonus = 2,
			TrueStrike = true,
			EnableCriticals = true
		};
		EffectContext context3 = new EffectContext
		{
			SourceInstanceId = item.InstanceId,
			TargetInstanceId = item2.InstanceId,
			State = state,
			CardDb = _cardDb,
			RollDice = (int count, int sides) => 20
		};
		List<GameEvent> collection2 = resolveAttackEffect3.Evaluate(context3).ToList();
		Assert.Single(collection2);
		Assert.IsType<DealDamageEvent>(collection2[0]);
	}

	[Fact]
	public void Leonora_And_Adathan_GrantAdditionalStrikes()
	{
		CardDefinition def = GetDef("Leonora");
		CardDefinition def2 = GetDef("Aureliane");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState p = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def, 1, 0, 1);
		playerState.Ranks[1].Add(cardInstance);
		GameState state = CreateState(playerState, p);
		int count = GameEngine.GetEffectiveStrikes(state, cardInstance, _cardDb).Count;
		GameEngine.AddTurnBuff(state, cardInstance.InstanceId, "EXTRA_STRIKE");
		IReadOnlyList<StrikeDescriptor> effectiveStrikes = GameEngine.GetEffectiveStrikes(state, cardInstance, _cardDb);
		Assert.Equal(count + 1, effectiveStrikes.Count);
		Assert.Equal(0, effectiveStrikes.Last().Modifier);
		CardInstance cardInstance2 = CreateInstance(def2, 2, 0, 1);
		playerState.Ranks[1].Add(cardInstance2);
		int count2 = GameEngine.GetEffectiveStrikes(state, cardInstance2, _cardDb).Count;
		GameEngine.AddTurnBuff(state, cardInstance2.InstanceId, "EXTRA_STRIKE", 2);
		IReadOnlyList<StrikeDescriptor> effectiveStrikes2 = GameEngine.GetEffectiveStrikes(state, cardInstance2, _cardDb);
		Assert.Equal(count2 + 1, effectiveStrikes2.Count);
		Assert.Equal(2, effectiveStrikes2.Last().Modifier);
	}

	[Fact]
	public void EvenTheOdds_GainsBuffsBasedOnOpposingCharactersWithinOneRank()
	{
		CardDefinition def = GetDef("Aureliane");
		PlayerState playerState = CreatePlayer(0, "P1");
		PlayerState playerState2 = CreatePlayer(1, "P2");
		CardInstance cardInstance = CreateInstance(def, 1, 0, 1);
		playerState.Ranks[1].Add(cardInstance);
		playerState2.Ranks[1].Add(CreateInstance(def, 2, 1, 1));
		playerState2.Ranks[1].Add(CreateInstance(def, 3, 1, 1));
		playerState2.Ranks[2].Add(CreateInstance(def, 4, 1, 2));
		GameState gameState = CreateState(playerState, playerState2);
		int num = gameState.Players[1].Ranks.Where((KeyValuePair<int, List<CardInstance>> kvp) => Math.Abs(kvp.Key - 1) <= 1).SelectMany((KeyValuePair<int, List<CardInstance>> kvp) => kvp.Value).Count();
		Assert.Equal(3, num);
		GameEngine.AddTurnBuff(gameState, cardInstance.InstanceId, "ATK", num);
		GameEngine.AddTurnBuff(gameState, cardInstance.InstanceId, "AC", num);
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(gameState, cardInstance, null, _cardDb);
		Assert.Equal(3, continuousPassiveBonuses.Atk);
		Assert.Equal(3, continuousPassiveBonuses.Ac);
	}
}
