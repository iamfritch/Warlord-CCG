using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Actions;

public sealed record PassAction : PlayerAction, IPlayerAction
{
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

	[CompilerGenerated]
	[SetsRequiredMembers]
	private PassAction(PassAction original)
		: base(original)
	{
	}
}
