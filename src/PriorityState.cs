namespace WarlordEngine;

public sealed class PriorityState
{
	public int ActivePlayerIndex { get; set; }

	public bool HasPassed { get; set; }

	public int ConsecutivePasses { get; set; }

	public bool IsReactWindowOpen { get; set; }

	public PriorityState DeepClone()
	{
		return new PriorityState
		{
			ActivePlayerIndex = ActivePlayerIndex,
			HasPassed = HasPassed,
			ConsecutivePasses = ConsecutivePasses,
			IsReactWindowOpen = IsReactWindowOpen
		};
	}
}
