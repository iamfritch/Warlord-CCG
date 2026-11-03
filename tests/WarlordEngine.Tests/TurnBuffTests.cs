using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class TurnBuffTests
{
	[Fact]
	public void Duplicate_Atk_Buffs_Stack_Properly()
	{
		GameState state = CreateSimpleState();
		int characterId = 10;

		// Add two separate +2 ATK buffs on the same character
		GameEngine.AddTurnBuff(state, characterId, TurnBuffType.Attack, 2);
		GameEngine.AddTurnBuff(state, characterId, TurnBuffType.Attack, 2);

		// Assert that both exist in the active list
		Assert.True(state.TurnState.ActiveTurnBuffs.TryGetValue(characterId, out List<TurnBuff>? buffs));
		Assert.NotNull(buffs);
		Assert.Equal(2, buffs.Count);
		Assert.All(buffs, b => Assert.Equal(2, b.Value));

		// Assert GetTurnBuffValue returns 4
		Assert.Equal(4, GameEngine.GetTurnBuffValue(state, characterId, TurnBuffType.Attack));
		Assert.Equal(4, GameEngine.GetTurnBuffValue(state, characterId, "ATK"));

		// Find card and test effective attack
		CardInstance card = CardLocator.FindCard(state, characterId)!.Value;
		int effectiveAtk = GameEngine.GetEffectiveAttack(state, card, null, EffectTestHelper.TestCardDb);
		Assert.Equal(3 + 4, effectiveAtk); // base 3 + 4
	}

	[Fact]
	public void Duplicate_String_Atk_Buffs_Stack_To_Four()
	{
		GameState state = CreateSimpleState();
		int characterId = 10;

		// Add two duplicate string-based +2 ATK buffs
		GameEngine.AddTurnBuff(state, characterId, "ATK", 2);
		GameEngine.AddTurnBuff(state, characterId, "ATK", 2);

		// Assert both stack
		Assert.Equal(4, GameEngine.GetTurnBuffValue(state, characterId, TurnBuffType.Attack));
		Assert.Equal(4, GameEngine.GetTurnBuffValue(state, characterId, "ATK"));

		CardInstance card = CardLocator.FindCard(state, characterId)!.Value;
		int effectiveAtk = GameEngine.GetEffectiveAttack(state, card, null, EffectTestHelper.TestCardDb);
		Assert.Equal(7, effectiveAtk);
	}

	[Fact]
	public void GrantAdditionalStrikeEffect_Grants_Extra_Strike()
	{
		EffectContext context = EffectTestHelper.CreateBoardContext(sourceInstanceId: 10);
		CardInstance card = CardLocator.FindCard(context.State, 10)!.Value;
		int initialStrikesCount = GameEngine.GetEffectiveStrikes(context.State, card, context.CardDb).Count;

		// Evaluate GrantAdditionalStrikeEffect
		GrantAdditionalStrikeEffect effect = new GrantAdditionalStrikeEffect(2);
		List<GameEvent> events = effect.Evaluate(context).ToList();

		Assert.Empty(events);
		Assert.True(GameEngine.HasTurnBuff(context.State, 10, TurnBuffType.ExtraStrikes));
		Assert.True(GameEngine.HasTurnBuff(context.State, 10, "ADDITIONAL_STRIKES"));
		Assert.True(GameEngine.HasTurnBuff(context.State, 10, "EXTRA_STRIKE"));

		IReadOnlyList<StrikeDescriptor> newStrikes = GameEngine.GetEffectiveStrikes(context.State, card, context.CardDb);
		Assert.Equal(initialStrikesCount + 1, newStrikes.Count);
		Assert.Equal(2, newStrikes.Last().Modifier);
	}

	[Fact]
	public void ModifyStatEvent_ArmorClass_Does_Not_Permanently_Leak()
	{
		GameState state = CreateSimpleState();
		int characterId = 10;
		CardInstance card = CardLocator.FindCard(state, characterId)!.Value;
		int baseAC = card.CurrentArmorClass;

		ModifyStatEvent evt = new ModifyStatEvent
		{
			TargetInstanceId = characterId,
			StatName = "CurrentArmorClass",
			Amount = 3
		};
		evt.ApplyToState(state);

		// CurrentArmorClass field should NOT be permanently modified
		CardInstance updatedCard = CardLocator.FindCard(state, characterId)!.Value;
		Assert.Equal(baseAC, updatedCard.CurrentArmorClass);

		// Effective Armor Class should include the turn buff (+3)
		Assert.Equal(baseAC + 3, GameEngine.GetEffectiveArmorClass(state, updatedCard, null, EffectTestHelper.TestCardDb));

		// Reset for new turn
		state.TurnState.ResetForNewTurn();

		// Effective Armor Class should return to base without leak
		Assert.Equal(baseAC, GameEngine.GetEffectiveArmorClass(state, updatedCard, null, EffectTestHelper.TestCardDb));
	}

	[Fact]
	public void StrongarmStoutEffect_Does_Not_Double_Count_Or_Leak_HP()
	{
		EffectContext context = EffectTestHelper.CreateBoardContext(sourceInstanceId: 10);
		CardInstance card = CardLocator.FindCard(context.State, 10)!.Value;
		int baseHP = card.CurrentHitPoints;

		StrongarmStoutEffect effect = new StrongarmStoutEffect();
		List<GameEvent> events = effect.Evaluate(context).ToList();

		CardInstance updatedCard = CardLocator.FindCard(context.State, 10)!.Value;
		// CurrentHitPoints field should NOT be permanently mutated
		Assert.Equal(baseHP, updatedCard.CurrentHitPoints);

		// Player 1 had cards in deck, so 1 card was drawn (+1 HP buff)
		int expectedHp = baseHP + 1;
		Assert.Equal(expectedHp, GameEngine.GetEffectiveCurrentHitPoints(context.State, updatedCard, context.CardDb));

		// Reset for new turn
		context.State.TurnState.ResetForNewTurn();
		Assert.Equal(baseHP, GameEngine.GetEffectiveCurrentHitPoints(context.State, updatedCard, context.CardDb));
	}

	[Fact]
	public void TurnState_Clone_DeepCopies_ActiveTurnBuffs()
	{
		TurnState turnState = new TurnState();
		turnState.ActiveTurnBuffs[1] = new List<TurnBuff> { new TurnBuff(TurnBuffType.Attack, 2) };

		TurnState cloned = turnState.Clone();
		cloned.ActiveTurnBuffs[1].Add(new TurnBuff(TurnBuffType.ArmorClass, 3));

		Assert.Single(turnState.ActiveTurnBuffs[1]);
		Assert.Equal(2, cloned.ActiveTurnBuffs[1].Count);
	}

	private static GameState CreateSimpleState()
	{
		GameState state = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			NextInstanceId = 100
		};
		PlayerState p1 = new PlayerState { PlayerIndex = 0, DisplayName = "P1" };
		PlayerState p2 = new PlayerState { PlayerIndex = 1, DisplayName = "P2" };

		CardInstance card = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			Rank = 1
		};
		p1.Ranks[1] = new List<CardInstance> { card };
		state.Players = new List<PlayerState> { p1, p2 };
		return state;
	}
}
