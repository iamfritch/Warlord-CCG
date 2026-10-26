# Warlord CCG: Engine Audit, AI Opponent & Godot 4 C# Client Specification

## 1. Project Overview & Architectural Goal
This repository contains a working, headless C# rules engine (`WarlordEngine.csproj`, targeting .NET 9.0+ for Android compatibility) with 114+ passing xUnit tests (`WarlordEngine.Tests.csproj`), a card data pipeline (`DataParser.csproj` / `cards.json`), and a deprecated Blazor frontend (`src/WarlordEngine.UI/`).

**Core Objectives:**
**Implement Two-Tier AI Opponent:** Build an `IAiAgent` architecture supporting both a fast **Heuristic Evaluation Bot** and an **Information-Set Monte Carlo Tree Search (IS-MCTS)** agent capable of handling hidden information and d20 combat variance.

---

## 2. Phase 1: Complete

---

## 3. Phase 2: AI Opponent Architecture (`src/AI/`)
Create a clean namespace `WarlordEngine.AI` inside the core engine library:

### 3.1 Core Interfaces
- `IAiAgent`: Exposes `IPlayerAction SelectAction(GameState currentState, GameEngine engine, CancellationToken ct = default)`.
- `IStateEvaluator`: Exposes `double Evaluate(GameState state, int maximizingPlayerId)`.

### 3.2 Tier 1: Heuristic Bot (`HeuristicAiAgent.cs`)
- Uses `BoardStateEvaluator.cs` to score terminal and non-terminal states based on:
  - Warlord survival and remaining wounds/damage differential (highest weight).
  - Total character level/stats on board (Ready > Spent > Stunned).
  - Rank 1 protection density in front of the Warlord.
  - Card advantage (cards in hand + board presence).
- Evaluates all actions returned by `GetLegalActions()` via 1-ply (or 2-ply expectimax over d20 hit probability) lookahead and returns the highest-scoring action.

### 3.3 Tier 2: IS-MCTS Agent (`MctsAiAgent.cs`)
- Uses Upper Confidence Bound applied to Trees (UCT) with explicit **Chance Nodes** for d20 rolls.
- Handles hidden information via **Determinization** (`state.DeepClone(observerPlayerId: aiPlayerId)` at the root of each iteration).
- Limits rollout depth to the end of the current turn (or $K$ actions) and evaluates leaf nodes using `BoardStateEvaluator.Evaluate()` rather than random full-game playouts.
- Must write xUnit tests pitting `MctsAiAgent` vs `HeuristicAiAgent` in 100 headless simulated games to verify zero crashes, zero infinite priority loops, and measurable win-rate superiority.

---

## 4. Phase 3: Godot 4 C# Client (`WarlordClient.Godot/`)
Replace `src/WarlordEngine.UI` (Blazor) with a Godot 4 (.NET 8) project that references `WarlordEngine.csproj` directly.

### 4.1 Resolution & Cross-Platform Input (Desktop + Android)
- Target **1920x1080 landscape** base resolution with `canvas_items` stretch mode and `expand` aspect so it scales cleanly on 4K displays, handhelds (ROG Ally X), and Android phones/tablets.
- Support both mouse hover/click and touch tap/long-press-to-zoom-card gestures.

### 4.2 Visual Board Architecture
- **Event-Driven Rendering:** The Godot UI must NEVER mutate `GameState` directly. It sends `IPlayerAction` instances to `GameEngine` and subscribes to `GameEvent` emissions (`CardPlayed`, `AbilityActivatedEvent`, `AttackResolved`, `CharacterDefeatedEvent`) to queue visual animations (tweens, dice rolls, floating damage numbers) sequentially.
- **Rank Formation Containers:** Dynamic horizontal `Container` nodes for Player Ranks 1–3 and AI Ranks 1–3 (inverted top-down), visually displaying Ready (upright), Spent (rotated 90°), and Stunned (rotated 180°) states, equipped items/tokens, and wounds.
- **Priority / React Prompt Banner:** Clear non-blocking HUD bar showing current Phase, Active Player, Priority Window status ("Waiting for React to: [AttackAction]"), and a prominent "Pass / Resolve" button (with optional Auto-Pass when no legal Reacts exist in hand/board).

---

## 5. Card Scope & Milestones
- **Milestone 1 (Audit & Headless AI):** Complete Phase 1 & Phase 2 using the existing preset starter decks in `PresetDeckFactory.cs`. Ensure 100% of cards in the preset decks have fully implemented abilities/triggers.
- **Milestone 2 (Godot Playable Vertical Slice):** Build the Godot 4 board scene, hand management, targeting arrows, combat log, and AI turn controller. Play a full match from Starting Army deployment to Warlord elimination against the AI.
- **Milestone 3 (Full *Into the Accordlands* Set Expansion):** Audit `DataParser/cards.json` and `RulesParser.cs` to batch-implement remaining keyword primitives and bespoke card effects across the ~350-400 card set in testable chunks.