using System.Diagnostics.CodeAnalysis;

namespace WarlordEngine.Events;

public sealed record CharacterMovedEvent : MarchEvent
{
	public CharacterMovedEvent()
	{
	}

	[SetsRequiredMembers]
	private CharacterMovedEvent(CharacterMovedEvent original)
		: base(original)
	{
	}
}
