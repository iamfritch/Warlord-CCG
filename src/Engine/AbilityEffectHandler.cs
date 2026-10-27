using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Abilities;
using WarlordEngine.Effects;
using WarlordEngine.Events;

namespace WarlordEngine.Engine;

public sealed class AbilityEffectHandler : IPostResolutionHandler
{
	private readonly CardDatabase _cardDb;

	private readonly DiceRoller? _diceRoller;

	private readonly Func<Action<AttackResolutionDetails>?>? _onAttackResolvedProvider;

	public AbilityEffectHandler(CardDatabase cardDb, DiceRoller? diceRoller = null, Func<Action<AttackResolutionDetails>?>? onAttackResolvedProvider = null)
	{
		_cardDb = cardDb ?? throw new ArgumentNullException("cardDb");
		_diceRoller = diceRoller;
		_onAttackResolvedProvider = onAttackResolvedProvider;
	}

	public IEnumerable<GameEvent> AfterResolve(GameEvent resolvedEvent, GameState state)
	{
		if (!(resolvedEvent is AbilityActivatedEvent activated))
		{
			yield break;
		}
		AbilityDefinition ability = activated.Ability;
		if (ability.Effects.Any(e => e is TargetPerformsStrikeEffect))
		{
			int primaryTarget = activated.TargetInstanceIds.Length > 0 ? activated.TargetInstanceIds[0] : activated.SourceInstanceId;
			EffectContext context = BuildContext(activated.SourceInstanceId, primaryTarget, state, activated.TargetInstanceIds, activated.PerformerInstanceId);
			foreach (IEffect effect in ability.Effects)
			{
				foreach (GameEvent item in effect.Evaluate(context))
				{
					yield return item;
				}
			}
			yield break;
		}
		if (activated.TargetInstanceIds.Length == 0)
		{
			EffectContext context = BuildContext(activated.SourceInstanceId, null, state, activated.TargetInstanceIds, activated.PerformerInstanceId);
			foreach (IEffect effect in ability.Effects)
			{
				foreach (GameEvent item in effect.Evaluate(context))
				{
					yield return item;
				}
			}
			yield break;
		}
		int[] targetInstanceIds = activated.TargetInstanceIds;
		foreach (int value in targetInstanceIds)
		{
			EffectContext context = BuildContext(activated.SourceInstanceId, value, state, activated.TargetInstanceIds, activated.PerformerInstanceId);
			foreach (IEffect effect2 in ability.Effects)
			{
				foreach (GameEvent item2 in effect2.Evaluate(context))
				{
					yield return item2;
				}
			}
		}
	}

	private EffectContext BuildContext(int sourceId, int? targetId, GameState state, IReadOnlyList<int>? targetIds = null, int? performerId = null)
	{
		int resolvedPerformerId = performerId ?? CardLocator.ResolvePerformerCharacterId(state, sourceId, _cardDb);
		return new EffectContext
		{
			SourceInstanceId = sourceId,
			PerformerInstanceId = resolvedPerformerId,
			PerformerSourceId = sourceId,
			TargetInstanceId = targetId,
			TargetInstanceIds = targetIds,
			State = state,
			CardDb = _cardDb,
			RollDice = (_diceRoller ?? ((DiceRoller)((int min, int max) => Random.Shared.Next(min, max + 1)))),
			OnAttackResolved = _onAttackResolvedProvider?.Invoke()
		};
	}
}
