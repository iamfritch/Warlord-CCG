# Warlord CCG Engine - Agent Context & Architecture Rules

## 1. Core Architecture & File Map
- **RulesParser.cs**: Parses raw card text and metadata from starter_decks.json into structured ability, cost, targeting, and effect models. NEVER use hardcoded card.Name checks; always parse mechanics generically from text/traits.
- **GameEngine.cs**: Executes parsed abilities, combat resolution (melee/ranged strikes, initiative, ranks), state transitions, and victory checks against game state.
- **DataPaths.cs**: Centralizes relative file path resolution via AppContext.BaseDirectory and Path.Combine. Never use hardcoded absolute file paths.
- **Data (starter_decks.json)**: Contains the 114 starter deck cards across Characters, Items, and Actions.
- **Tests (	ests/WarlordEngine.Tests/)**: xUnit test suite (StarterDeckAuditTests.cs, etc.).

## 2. Core Warlord Tabletop Rules Invariants
- **Ranks & Formation**: Ranks determine strike distance and legal positioning. Melee strikes hit adjacent ranks; Ranged strikes hit across distance; True Strike bypasses standard stealth/evasion restrictions where applicable.
- **Card Types & Factions**: Characters have Factions, Classes, Skill Levels, and Ranks. Standard Items and Actions use Class/Level prerequisites rather than Faction restrictions unless explicitly printed in card text.
- **Targeting & Costs**: All costs (spend, stun, sacrifice, discard, destroy) and all target filters (including multi-target counts and traits) must be fully validated BEFORE mutating game state.
- **Simulations (MCTS / Heuristic AI)**: The engine runs fast headless state clones for AI simulations. Never fire static callbacks, UI events, or unseeded global side effects during simulation runs.

## 3. Strict Agent Guardrails (Token & Repo Safety)
1. **No External Python/PowerShell Regex Scripts**: Always use native file-editing tools for C# modifications.
2. **Minimal File Reads**: Read only the specific .cs files required for the current task. Do not read in/, obj/, or bulk .json files unless instructed.
3. **Single Test Pass**: Complete all code edits first, then run dotnet test --no-restore once at the end to verify.

## 4. CRITICAL TEST & BUILD RULE
- NEVER run full dotnet test suites or background polling tasks (schedule / Timed). The sandbox blocks MSBuild and MCTS tests take too long.
- To verify your C# code compiles, either run dotnet build --no-restore outside the sandbox ONCE, or stop after editing the .cs files and let the user run dotnet test manually.
