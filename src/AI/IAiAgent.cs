using System.Threading;
using WarlordEngine.Actions;
using WarlordEngine.Engine;

namespace WarlordEngine.AI;

public interface IAiAgent
{
	string Name { get; }

	IPlayerAction SelectAction(GameState state, GameEngine engine, int playerId, CancellationToken ct = default(CancellationToken));
}
