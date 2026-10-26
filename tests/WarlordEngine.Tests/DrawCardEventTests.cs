using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class DrawCardEventTests
{
	[Fact]
	public void DrawCardEvent_Moves_Top_Card_From_Deck_To_Hand()
	{
		GameState state = EffectTestHelper.CreateBoardContext().State;
		Assert.Equal(2, state.Players[0].Deck.Count);
		Assert.Empty(state.Players[0].Hand);
		DrawCardEvent drawCardEvent = new DrawCardEvent
		{
			PlayerIndex = 0
		};
		drawCardEvent.ApplyToState(state);
		Assert.Single(state.Players[0].Hand);
		Assert.Equal(50, state.Players[0].Hand[0].InstanceId);
		Assert.Equal(CardZone.Hand, state.Players[0].Hand[0].Zone);
		Assert.Single(state.Players[0].Deck);
		Assert.Equal(51, state.Players[0].Deck[0].InstanceId);
	}

	[Fact]
	public void DrawCardEvent_Empty_Deck_Is_NoOp()
	{
		GameState state = EffectTestHelper.CreateBoardContext().State;
		state.Players[0].Deck.Clear();
		DrawCardEvent drawCardEvent = new DrawCardEvent
		{
			PlayerIndex = 0
		};
		drawCardEvent.ApplyToState(state);
		Assert.Empty(state.Players[0].Hand);
	}
}
