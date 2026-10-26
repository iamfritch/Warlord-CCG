using System.Diagnostics.CodeAnalysis;

namespace WarlordEngine.Actions;

public sealed record JourneyAction : MarchAction
{
	public JourneyAction()
	{
	}

	[SetsRequiredMembers]
	private JourneyAction(JourneyAction original)
		: base(original)
	{
	}
}
