using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Effects;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class MoveRankEffectTests
{
	[Fact]
	public void Yields_MarchEvent_From_Current_To_Destination()
	{
		EffectContext context = EffectTestHelper.CreateBoardContext();
		MoveRankEffect moveRankEffect = new MoveRankEffect(2);
		List<GameEvent> list = moveRankEffect.Evaluate(context).ToList();
		Assert.Single(list);
		MarchEvent marchEvent = Assert.IsType<MarchEvent>(list[0]);
		Assert.Equal(0, marchEvent.PlayerIndex);
		Assert.Equal(1, marchEvent.CardInstanceId);
		Assert.Equal(1, marchEvent.SourceRank);
		Assert.Equal(2, marchEvent.DestinationRank);
	}

	[Fact]
	public void Already_At_Destination_Yields_Nothing()
	{
		EffectContext context = EffectTestHelper.CreateBoardContext(1, null, 2);
		MoveRankEffect moveRankEffect = new MoveRankEffect(2);
		Assert.Empty(moveRankEffect.Evaluate(context).ToList());
	}
}
