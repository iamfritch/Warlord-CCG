using WarlordEngine.Events;

namespace WarlordEngine.Tests;

internal sealed class CancelAllDamageInterceptor : IEventInterceptor
{
	public EventResolutionResult Intercept(GameEvent incomingEvent, GameState currentState)
	{
		if (incomingEvent is DealDamageEvent)
		{
			return EventResolutionResult.Cancel();
		}
		return EventResolutionResult.Allow();
	}
}
