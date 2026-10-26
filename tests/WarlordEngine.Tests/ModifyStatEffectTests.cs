using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Effects;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class ModifyStatEffectTests
{
	[Fact]
	public void Yields_ModifyStatEvent_With_Correct_Stat_And_Amount()
	{
		EffectContext context = EffectTestHelper.CreateBoardContext(1, 2);
		ModifyStatEffect modifyStatEffect = new ModifyStatEffect("CurrentArmorClass", 2);
		List<GameEvent> list = modifyStatEffect.Evaluate(context).ToList();
		Assert.Single(list);
		ModifyStatEvent modifyStatEvent = Assert.IsType<ModifyStatEvent>(list[0]);
		Assert.Equal(2, modifyStatEvent.TargetInstanceId);
		Assert.Equal("CurrentArmorClass", modifyStatEvent.StatName);
		Assert.Equal(2, modifyStatEvent.Amount);
	}

	[Fact]
	public void Negative_Amount_Works_For_Debuffs()
	{
		EffectContext context = EffectTestHelper.CreateBoardContext(1, 2);
		ModifyStatEffect modifyStatEffect = new ModifyStatEffect("CurrentSkill", -3);
		List<GameEvent> list = modifyStatEffect.Evaluate(context).ToList();
		ModifyStatEvent modifyStatEvent = Assert.IsType<ModifyStatEvent>(list[0]);
		Assert.Equal(-3, modifyStatEvent.Amount);
	}
}
