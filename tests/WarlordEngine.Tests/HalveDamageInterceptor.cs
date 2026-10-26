using WarlordEngine.Events;

namespace WarlordEngine.Tests;

internal sealed class HalveDamageInterceptor : IEventInterceptor
{
	public EventResolutionResult Intercept(GameEvent incomingEvent, GameState currentState)
	{
		if (incomingEvent is DealDamageEvent dealDamageEvent)
		{
			DealDamageEvent modifiedEvent = dealDamageEvent with
			{
				Amount = dealDamageEvent.Amount / 2
			};
			return EventResolutionResult.Modify(modifiedEvent);
		}
		return EventResolutionResult.Allow();
	}
}
