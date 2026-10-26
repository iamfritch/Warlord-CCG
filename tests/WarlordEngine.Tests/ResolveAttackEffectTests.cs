using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Effects;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class ResolveAttackEffectTests
{
	[Fact]
	public void Hit_When_Roll_Plus_Bonus_Equals_AC()
	{
		EffectContext context = EffectTestHelper.CreateCombatContext(12);
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = 3
		};
		List<GameEvent> list = resolveAttackEffect.Evaluate(context).ToList();
		Assert.Single(list);
		DealDamageEvent dealDamageEvent = Assert.IsType<DealDamageEvent>(list[0]);
		Assert.Equal(1, dealDamageEvent.Amount);
		Assert.Equal(2, dealDamageEvent.TargetInstanceId);
	}

	[Fact]
	public void Hit_When_Roll_Plus_Bonus_Exceeds_AC()
	{
		EffectContext context = EffectTestHelper.CreateCombatContext(18);
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = 3
		};
		List<GameEvent> list = resolveAttackEffect.Evaluate(context).ToList();
		Assert.Single(list);
		DealDamageEvent dealDamageEvent = Assert.IsType<DealDamageEvent>(list[0]);
		Assert.Equal(1, dealDamageEvent.Amount);
	}

	[Fact]
	public void Miss_When_Roll_Plus_Bonus_Below_AC()
	{
		EffectContext context = EffectTestHelper.CreateCombatContext(11);
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = 3
		};
		List<GameEvent> collection = resolveAttackEffect.Evaluate(context).ToList();
		Assert.Empty(collection);
	}

	[Fact]
	public void Miss_With_Nat_1_Against_Low_AC()
	{
		EffectContext context = EffectTestHelper.CreateCombatContext(1, 1, 2, 5);
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = 3
		};
		List<GameEvent> collection = resolveAttackEffect.Evaluate(context).ToList();
		Assert.Empty(collection);
	}

	[Fact]
	public void Hit_With_Nat_20_Against_High_AC()
	{
		EffectContext context = EffectTestHelper.CreateCombatContext(20, 1, 2, 22);
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = 3
		};
		List<GameEvent> collection = resolveAttackEffect.Evaluate(context).ToList();
		Assert.Single(collection);
	}

	[Fact]
	public void Miss_With_Nat_20_Against_Very_High_AC()
	{
		EffectContext context = EffectTestHelper.CreateCombatContext(20, 1, 2, 25);
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = 3
		};
		List<GameEvent> collection = resolveAttackEffect.Evaluate(context).ToList();
		Assert.Empty(collection);
	}

	[Fact]
	public void Hit_With_Zero_Bonus_Roll_Equals_AC()
	{
		EffectContext context = EffectTestHelper.CreateCombatContext(10, 1, 2, 10);
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = 0
		};
		List<GameEvent> collection = resolveAttackEffect.Evaluate(context).ToList();
		Assert.Single(collection);
	}

	[Fact]
	public void Hit_With_Negative_Bonus()
	{
		EffectContext context = EffectTestHelper.CreateCombatContext(15, 1, 2, 10);
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = -2
		};
		List<GameEvent> collection = resolveAttackEffect.Evaluate(context).ToList();
		Assert.Single(collection);
	}

	[Fact]
	public void Damage_Type_Is_Passed_Through()
	{
		EffectContext context = EffectTestHelper.CreateCombatContext(20, 1, 2, 10);
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = 5,
			DamageType = "ranged"
		};
		List<GameEvent> list = resolveAttackEffect.Evaluate(context).ToList();
		DealDamageEvent dealDamageEvent = Assert.IsType<DealDamageEvent>(list[0]);
		Assert.Equal("ranged", dealDamageEvent.DamageType);
	}

	[Fact]
	public void No_Target_Yields_Nothing()
	{
		EffectContext context = EffectTestHelper.CreateBoardContext();
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = 10
		};
		List<GameEvent> collection = resolveAttackEffect.Evaluate(context).ToList();
		Assert.Empty(collection);
	}

	[Theory]
	[InlineData(new object[] { 12, 3, 15, true })]
	[InlineData(new object[] { 11, 3, 15, false })]
	[InlineData(new object[] { 1, 14, 15, true })]
	[InlineData(new object[] { 1, 13, 15, false })]
	[InlineData(new object[] { 20, 0, 20, true })]
	[InlineData(new object[] { 19, 0, 20, false })]
	public void Boundary_Cases(int roll, int bonus, int ac, bool shouldHit)
	{
		EffectContext context = EffectTestHelper.CreateCombatContext(roll, 1, 2, ac);
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = bonus
		};
		List<GameEvent> list = resolveAttackEffect.Evaluate(context).ToList();
		if (shouldHit)
		{
			Assert.Single(list);
			DealDamageEvent dealDamageEvent = Assert.IsType<DealDamageEvent>(list[0]);
			Assert.Equal(1, dealDamageEvent.Amount);
		}
		else
		{
			Assert.Empty(list);
		}
	}
}
