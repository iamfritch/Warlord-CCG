namespace WarlordEngine.Actions;

public sealed record PassAction : IPlayerAction
{
	public required int PlayerIndex { get; init; }

	public void Execute(GameState state)
	{
		if (state.Priority.IsReactWindowOpen)
		{
			state.Priority.ConsecutivePasses++;
			state.Priority.HasPassed = true;
			if (state.Players.Count > 0)
			{
				state.Priority.ActivePlayerIndex = (PlayerIndex + 1) % state.Players.Count;
			}
		}
		else
		{
			state.TurnState.RecordPass();
		}
	}
}
