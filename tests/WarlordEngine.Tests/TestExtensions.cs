using WarlordEngine.Engine;

namespace WarlordEngine.Tests;

internal static class TestExtensions
{
	public static int ResolverCountOrPending(this GameState state, GameEngine engine)
	{
		return engine.Resolver.PendingCount + state.PendingEvents.Count;
	}
}
