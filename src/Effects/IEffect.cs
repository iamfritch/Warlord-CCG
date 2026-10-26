using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public interface IEffect
{
	IEnumerable<GameEvent> Evaluate(EffectContext context);
}
