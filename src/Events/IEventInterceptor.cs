namespace WarlordEngine.Events;

public interface IEventInterceptor
{
	EventResolutionResult Intercept(GameEvent incomingEvent, GameState currentState);
}
