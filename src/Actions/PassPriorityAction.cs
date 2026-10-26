using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Actions;

public sealed record PassPriorityAction : PlayerAction
{
	[CompilerGenerated]
	[SetsRequiredMembers]
	private PassPriorityAction(PassPriorityAction original)
		: base(original)
	{
	}
}
