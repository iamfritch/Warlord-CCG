using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Actions;

public abstract record PlayerAction : IPlayerAction
{
	public required int PlayerIndex { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	protected PlayerAction(PlayerAction original)
	{
		PlayerIndex = original.PlayerIndex;
	}
}
