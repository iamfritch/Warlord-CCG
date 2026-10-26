\# Phase 2 Specification: Warlord CCG AI Opponent (`WarlordEngine.AI`)



\## Phase 1 Completed Baseline (DO NOT RE-IMPLEMENT)

The core engine (`WarlordEngine.csproj`) has 132 passing tests and already provides:

\- `GameEngine.GetLegalActions(GameState state, int playerId)` -> returns `IReadOnlyList<IPlayerAction>`

\- `GameEngine.ExecuteAction(GameState state, IPlayerAction action)` -> executes action \& drains/pauses React stack

\- `GameState.DeepClone(int? observerPlayerId = null)` -> fast deep copy with hidden-hand determinization

\- `GameEngine.SetRandomSeed(int seed)` / `SetDiceRoller(...)` -> controls d20 rolls (`ResolveAttackEffect` with `EnableCriticals`)



\## Architecture \& Deliverables (`src/AI/`)



\### 1. Core Contracts

\- `IAiAgent.cs`:

&#x20; - `string Name { get; }`

&#x20; - `IPlayerAction SelectAction(GameState state, GameEngine engine, int playerId, CancellationToken ct = default);`

\- `IStateEvaluator.cs`:

&#x20; - `double Evaluate(GameState state, int maximizingPlayerId);`



\### 2. Heuristic State Evaluator (`BoardStateEvaluator.cs` : `IStateEvaluator`)

Computes a deterministic scalar score from `maximizingPlayerId`'s perspective (`MyScore - OpponentScore`):

\- \*\*Terminal States:\*\* `+100,000` if `maximizingPlayerId` won (`GameState.IsGameOver`), `-100,000` if lost.

\- \*\*Warlord Health (Critical Priority):\*\* `-150` per wound on own Warlord; `+150` per wound on enemy Warlord.

\- \*\*Warlord Rank Protection:\*\* `+80` bonus if own Warlord is in Rank 2+ behind at least two Rank 1 characters; `-100` penalty if own Warlord is exposed in Rank 1 while other characters exist.

\- \*\*Board Presence (Characters in Ranks):\*\*

&#x20; - Base value per character: `(CardLevel \* 25) + (AttackBonus \* 8) + ((ArmorClass - 10) \* 6) - (Wounds \* 15)`

&#x20; - Posture multiplier: `Ready = 1.0x`, `Spent = 0.75x`, `Stunned = 0.4x`

\- \*\*Attached Items:\*\* `+15` per attached item on living characters.

\- \*\*Card Advantage:\*\* `+18` per card in `Hand`, `+1` per card remaining in `Deck`.



\### 3. Tier 1: Heuristic Bot (`HeuristicAiAgent.cs` : `IAiAgent`)

\- Calls `engine.GetLegalActions(state, playerId)`.

\- If only 1 legal action exists (e.g., forced `PassAction`), returns it immediately without cloning.

\- For each legal action:

&#x20; - Clones state via `state.DeepClone(observerPlayerId: playerId)`.

&#x20; - For `AttackAction` (which involves d20 variance), evaluates expected value across 3 representative d20 rolls (`d20 = 5`, `d20 = 11`, `d20 = 17`) on the cloned engine, averaging the resulting `BoardStateEvaluator.Evaluate()` scores.

&#x20; - For deterministic actions (`PlayCardAction`, `UseAbilityAction`, `PassAction`, `DeployStartingArmyAction`), executes on the clone and scores via `BoardStateEvaluator.Evaluate()`.

\- Returns the action with the highest expected score.



\### 4. Tier 2: IS-MCTS Agent (`MctsAiAgent.cs` : `IAiAgent`)

\- Implements Information-Set Monte Carlo Tree Search with configurable `Iterations` (default `250`) and exploration constant $C = 1.414$.

\- Each iteration:

&#x20; 1. \*\*Determinization:\*\* Calls `var simState = state.DeepClone(observerPlayerId: playerId)` and creates a simulation engine with standard random d20 rolls (`1..20`).

&#x20; 2. \*\*Selection \& Expansion:\*\* Traverses tree using UCT formula $\\frac{W\_i}{N\_i} + C \\sqrt{\\frac{\\ln N}{N\_i}}$, accounting for which player holds active priority at each node (`PriorityState.ActivePlayerId` during React windows, or `TurnState.ActivePlayerId` otherwise).

&#x20; 3. \*\*Rollout (Bounded Depth):\*\* Simulates up to `MaxRolloutSteps` (e.g., `8` actions or until turn changes / game over) using fast epsilon-greedy or lightweight heuristic action selection, then scores the leaf state using `BoardStateEvaluator.Evaluate(simState, playerId)` normalized to $\[0, 1]$ via `Math.Tanh(score / 500.0) \* 0.5 + 0.5`.

&#x20; 4. \*\*Backpropagation:\*\* Updates visit counts and cumulative rewards up the path.

\- Returns the root child action with the highest visit count $N\_i$.

