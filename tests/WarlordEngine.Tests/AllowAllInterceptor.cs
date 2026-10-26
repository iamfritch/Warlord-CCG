using WarlordEngine.Events;

namespace WarlordEngine.Tests;

internal sealed class AllowAllInterceptor : IEventInterceptor
{
	public int CallCount { get; private set; }

	public EventResolutionResult Intercept(GameEvent incomingEvent, GameState currentState)
	{
		CallCount++;
		return EventResolutionResult.Allow();
	}
}
