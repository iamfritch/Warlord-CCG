using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Effects;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class DrawCardEffectTests
{
	[Fact]
	public void Yields_Correct_Number_Of_DrawCardEvents()
	{
		EffectContext context = EffectTestHelper.CreateBoardContext();
		DrawCardEffect drawCardEffect = new DrawCardEffect(3);
		List<GameEvent> list = drawCardEffect.Evaluate(context).ToList();
		Assert.Equal(3, list.Count);
		Assert.All(list, (GameEvent e) =>
		{
			DrawCardEvent drawCardEvent = Assert.IsType<DrawCardEvent>(e);
			Assert.Equal(0, drawCardEvent.PlayerIndex);
		});
	}

	[Fact]
	public void Single_Draw_Yields_One_Event()
	{
		EffectContext context = EffectTestHelper.CreateBoardContext();
		DrawCardEffect drawCardEffect = new DrawCardEffect(1);
		List<GameEvent> collection = drawCardEffect.Evaluate(context).ToList();
		Assert.Single(collection);
	}
}
