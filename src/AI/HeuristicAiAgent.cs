using System;
using System.Collections.Generic;
using System.Threading;
using WarlordEngine.Actions;
using WarlordEngine.Engine;

namespace WarlordEngine.AI;

public sealed class HeuristicAiAgent : IAiAgent
{
	private readonly IStateEvaluator _evaluator;

	public string Name { get; }

	public HeuristicAiAgent(IStateEvaluator? evaluator = null, string name = "HeuristicAgent")
	{
		_evaluator = evaluator ?? new BoardStateEvaluator();
		Name = name;
	}

	public IPlayerAction SelectAction(GameState state, GameEngine engine, int playerId, CancellationToken ct = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(state, "state");
		ArgumentNullException.ThrowIfNull(engine, "engine");
		IReadOnlyList<IPlayerAction> legalActions = engine.GetLegalActions(state, playerId);
		if (legalActions.Count == 0)
		{
			return new PassAction
			{
				PlayerIndex = playerId
			};
		}
		if (legalActions.Count == 1)
		{
			return legalActions[0];
		}
		IStateEvaluator stateEvaluator;
		if (!(_evaluator is BoardStateEvaluator { CardDb: null }))
		{
			stateEvaluator = _evaluator;
		}
		else
		{
			IStateEvaluator stateEvaluator2 = new BoardStateEvaluator(engine.CardDb);
			stateEvaluator = stateEvaluator2;
		}
		IStateEvaluator stateEvaluator3 = stateEvaluator;
		IPlayerAction result = legalActions[0];
		double num = double.NegativeInfinity;
		int[] array = new int[3] { 5, 11, 17 };
		foreach (IPlayerAction item in legalActions)
		{
			if (ct.IsCancellationRequested)
			{
				break;
			}
			double num3;
			if ((item is AttackAction || item is DeclareAttackAction) ? true : false)
			{
				double num2 = 0.0;
				foreach (int roll in array)
				{
					GameState gameState = state.DeepClone(playerId);
					gameState.IsSimulation = true;
					gameState.AutoPassWhenNoReactsAvailable = true;
					GameEngine gameEngine = new GameEngine(gameState, engine.CardDb, (int count, int sides) => roll)
					{
						AutoPassWhenNoReactsAvailable = true
					};
					ActionResult actionResult = gameEngine.ExecuteAction(item);
					DrainReactWindow(gameEngine, gameState);
					num2 += (actionResult.IsSuccess ? stateEvaluator3.Evaluate(gameState, playerId) : (-100000.0));
				}
				num3 = num2 / (double)array.Length;
			}
			else
			{
				GameState gameState2 = state.DeepClone(playerId);
				gameState2.IsSimulation = true;
				gameState2.AutoPassWhenNoReactsAvailable = true;
				GameEngine gameEngine2 = new GameEngine(gameState2, engine.CardDb, engine.DiceRoller)
				{
					AutoPassWhenNoReactsAvailable = true
				};
				ActionResult actionResult2 = gameEngine2.ExecuteAction(item);
				DrainReactWindow(gameEngine2, gameState2);
				num3 = (actionResult2.IsSuccess ? stateEvaluator3.Evaluate(gameState2, playerId) : (-100000.0));
			}
			if (num3 > num)
			{
				num = num3;
				result = item;
			}
		}
		return result;
	}

	private static void DrainReactWindow(GameEngine simEngine, GameState simState)
	{
		int num = 20;
		while (simState.Priority.IsReactWindowOpen && num-- > 0)
		{
			simEngine.ExecuteAction(new PassAction
			{
				PlayerIndex = simState.Priority.ActivePlayerIndex
			});
		}
	}
}
