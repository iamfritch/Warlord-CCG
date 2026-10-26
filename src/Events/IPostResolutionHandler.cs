using System.Collections.Generic;

namespace WarlordEngine.Events;

public interface IPostResolutionHandler
{
	IEnumerable<GameEvent> AfterResolve(GameEvent resolvedEvent, GameState state);
}
