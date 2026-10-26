using System;
using System.Collections.Generic;
using System.Threading;
using WarlordEngine.Actions;
using WarlordEngine.Engine;

namespace WarlordEngine.AI;

public sealed class MctsAiAgent : IAiAgent
{
	private sealed class MctsNode
	{
		public IPlayerAction? Action { get; }

		public int ActingPlayerIndex { get; }

		public MctsNode? Parent { get; }

		public List<MctsNode> Children { get; } = new List<MctsNode>();

		public int Visits { get; set; }

		public double TotalReward { get; set; }

		public MctsNode(IPlayerAction? action, int actingPlayerIndex, MctsNode? parent)
		{
			Action = action;
			ActingPlayerIndex = actingPlayerIndex;
			Parent = parent;
		}
	}

	private readonly IStateEvaluator _evaluator;

	private readonly Random _rng;

	public string Name { get; }

	public int Iterations { get; }

	public double ExplorationConstant { get; }

	public int MaxRolloutSteps { get; }

	public MctsAiAgent(IStateEvaluator? evaluator = null, int iterations = 200, double explorationConstant = 1.414, int maxRolloutSteps = 8, string name = "MctsAgent", Random? rng = null)
	{
		_evaluator = evaluator ?? new BoardStateEvaluator();
		Iterations = iterations;
		ExplorationConstant = explorationConstant;
		MaxRolloutSteps = maxRolloutSteps;
		Name = name;
		_rng = rng ?? new Random();
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
		MctsNode mctsNode = new MctsNode(null, playerId, null);
		for (int i = 0; i < legalActions.Count; i++)
		{
			mctsNode.Children.Add(new MctsNode(legalActions[i], playerId, mctsNode));
		}
		for (int j = 0; j < Iterations; j++)
		{
			if (ct.IsCancellationRequested)
			{
				break;
			}
			GameState gameState = state.DeepClone(playerId);
			gameState.IsSimulation = true;
			gameState.AutoPassWhenNoReactsAvailable = true;
			GameEngine gameEngine = new GameEngine(gameState, engine.CardDb, (int count, int sides) =>
			{
				int num23 = 0;
				for (int k = 0; k < count; k++)
				{
					num23 += _rng.Next(1, sides + 1);
				}
				return num23;
			});
			gameEngine.AutoPassWhenNoReactsAvailable = true;
			MctsNode mctsNode2 = mctsNode;
			while (!gameState.IsGameOver)
			{
				while (true)
				{
					bool flag = !gameState.IsGameOver;
					if (flag)
					{
						TurnPhase currentPhase = gameState.CurrentPhase;
						bool flag2 = (uint)(currentPhase - 1) <= 1u;
						flag = flag2;
					}
					if (!flag)
					{
						break;
					}
					gameEngine.AdvancePhase();
				}
				if (gameState.IsGameOver)
				{
					break;
				}
				int activePlayer = GetActivePlayer(gameState);
				IReadOnlyList<IPlayerAction> legalActions2 = gameEngine.GetLegalActions(gameState, activePlayer);
				if (legalActions2.Count == 0)
				{
					if (gameState.Priority.IsReactWindowOpen)
					{
						break;
					}
					gameEngine.AdvancePhase();
					continue;
				}
				MctsNode mctsNode3 = null;
				for (int num = 0; num < legalActions2.Count; num++)
				{
					IPlayerAction playerAction = legalActions2[num];
					MctsNode mctsNode4 = null;
					for (int num2 = 0; num2 < mctsNode2.Children.Count; num2++)
					{
						if (ActionEquals(mctsNode2.Children[num2].Action, playerAction))
						{
							mctsNode4 = mctsNode2.Children[num2];
							break;
						}
					}
					if (mctsNode4 == null)
					{
						MctsNode mctsNode5 = new MctsNode(playerAction, activePlayer, mctsNode2);
						mctsNode2.Children.Add(mctsNode5);
						mctsNode3 = mctsNode5;
						break;
					}
					if (mctsNode4.Visits == 0)
					{
						mctsNode3 = mctsNode4;
						break;
					}
				}
				if (mctsNode3 != null)
				{
					mctsNode2 = mctsNode3;
					gameEngine.ExecuteAction(mctsNode2.Action);
					DrainReactWindow(gameEngine, gameState);
					if (!gameState.Priority.IsReactWindowOpen && mctsNode2.Action is PassAction)
					{
						gameEngine.AdvancePhase();
					}
					break;
				}
				MctsNode mctsNode6 = null;
				double num3 = double.NegativeInfinity;
				double num4 = Math.Log(Math.Max(1, mctsNode2.Visits));
				for (int num5 = 0; num5 < mctsNode2.Children.Count; num5++)
				{
					MctsNode mctsNode7 = mctsNode2.Children[num5];
					bool flag3 = false;
					for (int num6 = 0; num6 < legalActions2.Count; num6++)
					{
						if (ActionEquals(mctsNode7.Action, legalActions2[num6]))
						{
							flag3 = true;
							break;
						}
					}
					if (flag3)
					{
						double num7;
						if (mctsNode7.Visits == 0)
						{
							num7 = double.PositiveInfinity;
						}
						else
						{
							double num8 = mctsNode7.TotalReward / (double)mctsNode7.Visits;
							double num9 = ((activePlayer == playerId) ? num8 : (1.0 - num8));
							double num10 = ExplorationConstant * Math.Sqrt(num4 / (double)mctsNode7.Visits);
							num7 = num9 + num10;
						}
						if (num7 > num3)
						{
							num3 = num7;
							mctsNode6 = mctsNode7;
						}
					}
				}
				if (mctsNode6 == null)
				{
					break;
				}
				mctsNode2 = mctsNode6;
				ActionResult actionResult = gameEngine.ExecuteAction(mctsNode2.Action);
				DrainReactWindow(gameEngine, gameState);
				if (!actionResult.IsSuccess)
				{
					break;
				}
				if (!gameState.Priority.IsReactWindowOpen && mctsNode2.Action is PassAction)
				{
					gameEngine.AdvancePhase();
				}
			}
			int num11 = 0;
			for (int num12 = 0; num12 < MaxRolloutSteps; num12++)
			{
				if (gameState.IsGameOver)
				{
					break;
				}
				while (true)
				{
					bool flag = !gameState.IsGameOver;
					if (flag)
					{
						TurnPhase currentPhase = gameState.CurrentPhase;
						bool flag2 = (uint)(currentPhase - 1) <= 1u;
						flag = flag2;
					}
					if (!flag)
					{
						break;
					}
					gameEngine.AdvancePhase();
				}
				if (gameState.IsGameOver)
				{
					break;
				}
				int activePlayer2 = GetActivePlayer(gameState);
				IReadOnlyList<IPlayerAction> legalActions3 = gameEngine.GetLegalActions(gameState, activePlayer2);
				if (legalActions3.Count == 0)
				{
					if (gameState.Priority.IsReactWindowOpen)
					{
						break;
					}
					gameEngine.AdvancePhase();
					continue;
				}
				IPlayerAction playerAction2 = SelectRolloutAction(legalActions3, gameState, activePlayer2);
				ActionResult actionResult2 = gameEngine.ExecuteAction(playerAction2);
				DrainReactWindow(gameEngine, gameState);
				if (!actionResult2.IsSuccess)
				{
					break;
				}
				num11++;
				if (!gameState.Priority.IsReactWindowOpen && playerAction2 is PassAction)
				{
					gameEngine.AdvancePhase();
				}
			}
			double num13 = Math.Pow(0.96, num11);
			double num14;
			if (gameState.IsGameOver)
			{
				if (gameState.WinningPlayerIndex == playerId)
				{
					num14 = 1.0 * num13;
				}
				else
				{
					num14 = ((!gameState.WinningPlayerIndex.HasValue) ? 0.5 : 0.0);
				}
			}
			else
			{
				num14 = Math.Clamp(Math.Tanh(stateEvaluator3.Evaluate(gameState, playerId) / 500.0) * 0.35 + 0.5, 0.15, 0.85) * num13;
			}
			for (MctsNode mctsNode8 = mctsNode2; mctsNode8 != null; mctsNode8 = mctsNode8.Parent)
			{
				mctsNode8.Visits++;
				mctsNode8.TotalReward += num14;
			}
		}
		int num15 = -1;
		for (int num16 = 0; num16 < mctsNode.Children.Count; num16++)
		{
			if (mctsNode.Children[num16].Visits > num15)
			{
				num15 = mctsNode.Children[num16].Visits;
			}
		}
		IPlayerAction result = legalActions[0];
		double num17 = double.NegativeInfinity;
		int num18 = -1;
		int num19 = Math.Max(1, (int)((double)num15 * 0.3));
		for (int num20 = 0; num20 < mctsNode.Children.Count; num20++)
		{
			MctsNode mctsNode9 = mctsNode.Children[num20];
			if (mctsNode9.Action != null && mctsNode9.Visits != 0)
			{
				double num21 = mctsNode9.TotalReward / (double)mctsNode9.Visits;
				double num22 = ((mctsNode9.Visits >= num19) ? num21 : (num21 * 0.5));
				if (num22 > num17 || (Math.Abs(num22 - num17) < 1E-06 && mctsNode9.Visits > num18))
				{
					num17 = num22;
					num18 = mctsNode9.Visits;
					result = mctsNode9.Action;
				}
			}
		}
		return result;
	}

	private static int GetActivePlayer(GameState state)
	{
		if (!state.Priority.IsReactWindowOpen)
		{
			return state.TurnState.ActivePlayerIndex;
		}
		return state.Priority.ActivePlayerIndex;
	}

	private IPlayerAction SelectRolloutAction(IReadOnlyList<IPlayerAction> actions, GameState state, int activePlayer)
	{
		if (actions.Count == 1)
		{
			return actions[0];
		}
		List<IPlayerAction> list = new List<IPlayerAction>();
		List<IPlayerAction> list2 = new List<IPlayerAction>();
		IPlayerAction playerAction = null;
		for (int i = 0; i < actions.Count; i++)
		{
			IPlayerAction playerAction2 = actions[i];
			if ((playerAction2 is PassAction || playerAction2 is PassPriorityAction) ? true : false)
			{
				playerAction = playerAction2;
			}
			else if ((playerAction2 is AttackAction || playerAction2 is DeclareAttackAction) ? true : false)
			{
				list.Add(playerAction2);
			}
			else
			{
				list2.Add(playerAction2);
			}
		}
		if (state.Priority.IsReactWindowOpen)
		{
			if (list2.Count > 0 && _rng.NextDouble() < 0.5)
			{
				return list2[_rng.Next(list2.Count)];
			}
			return playerAction ?? actions[_rng.Next(actions.Count)];
		}
		if (list.Count > 0 && _rng.NextDouble() < 0.65)
		{
			return list[_rng.Next(list.Count)];
		}
		if (list2.Count > 0 && _rng.NextDouble() < 0.7)
		{
			return list2[_rng.Next(list2.Count)];
		}
		return playerAction ?? actions[_rng.Next(actions.Count)];
	}

	private static bool ActionEquals(IPlayerAction? a, IPlayerAction? b)
	{
		if (a == b)
		{
			return true;
		}
		if (a == null || b == null)
		{
			return false;
		}
		if (a.GetType() != b.GetType())
		{
			return false;
		}
		if (a is UseAbilityAction useAbilityAction && b is UseAbilityAction useAbilityAction2)
		{
			if (useAbilityAction.PlayerIndex == useAbilityAction2.PlayerIndex && useAbilityAction.SourceInstanceId == useAbilityAction2.SourceInstanceId && useAbilityAction.AbilityIndex == useAbilityAction2.AbilityIndex)
			{
				return useAbilityAction.TargetInstanceIds.AsSpan().SequenceEqual(useAbilityAction2.TargetInstanceIds.AsSpan());
			}
			return false;
		}
		return a.Equals(b);
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
