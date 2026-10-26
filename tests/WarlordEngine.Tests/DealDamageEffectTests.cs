using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Effects;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class DealDamageEffectTests
{
	[Fact]
	public void Yields_DealDamageEvent_With_Correct_Amount_And_Type()
	{
		EffectContext context = EffectTestHelper.CreateBoardContext(1, 2);
		DealDamageEffect dealDamageEffect = new DealDamageEffect(3, "fire");
		List<GameEvent> list = dealDamageEffect.Evaluate(context).ToList();
		Assert.Single(list);
		DealDamageEvent dealDamageEvent = Assert.IsType<DealDamageEvent>(list[0]);
		Assert.Equal(2, dealDamageEvent.TargetInstanceId);
		Assert.Equal(3, dealDamageEvent.Amount);
		Assert.Equal("fire", dealDamageEvent.DamageType);
	}

	[Fact]
	public void No_Target_Yields_Nothing()
	{
		EffectContext context = EffectTestHelper.CreateBoardContext();
		DealDamageEffect dealDamageEffect = new DealDamageEffect(5, "melee");
		Assert.Empty(dealDamageEffect.Evaluate(context).ToList());
	}
}
