namespace WarlordCli;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WarlordEngine;
using WarlordEngine.Actions;
using WarlordEngine.AI;
using WarlordEngine.Effects;
using WarlordEngine.Engine;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("======================================================================");
        Console.WriteLine("                     WARLORD CCG - PLAY VS AI                         ");
        Console.WriteLine("======================================================================\n");

        string? cardsPath = FindCardsJsonPath();
        if (cardsPath == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Could not locate DataParser/cards.json.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Loading card database from: {cardsPath}");
        var cardDb = DatabaseLoader.LoadFromFile(cardsPath);
        var starterDecks = PresetDeckFactory.GetSixFactionStarterDecks(cardDb, targetDeckSize: 50);
        if (starterDecks.Count < 2)
        {
            Console.WriteLine("Error: Less than 2 starter decks available.");
            return;
        }

        Console.WriteLine("\nChoose AI Opponent:");
        Console.WriteLine("  [1] HeuristicAiAgent (Fast 1-ply lookahead)");
        Console.WriteLine("  [2] MctsAiAgent (Tier 2 IS-MCTS, 200 iterations) [Default]");
        Console.Write("Selection (1-2, default 2): ");
        string? aiChoice = Console.ReadLine()?.Trim();

        IAiAgent aiAgent = aiChoice == "1"
            ? new HeuristicAiAgent(name: "Heuristic AI")
            : new MctsAiAgent(iterations: 200, name: "IS-MCTS AI");

        Console.WriteLine($"Selected AI: {aiAgent.Name}");

        // Fix 1: 6 Core Faction Starter Decks & CLI Deck Selection
        Console.WriteLine("\n======================================================================");
        Console.WriteLine("              CHOOSE YOUR CORE FACTION STARTER DECK                   ");
        Console.WriteLine("======================================================================");
        for (int i = 0; i < starterDecks.Count; i++)
        {
            var def = cardDb.GetDefinition(starterDecks[i].WarlordDefinitionId);
            string faction = def?.GetEffectiveFaction() ?? "Neutral";
            Console.WriteLine($"  [{i + 1}] {faction,-16} : {starterDecks[i].DeckName} ({starterDecks[i].TotalCardCount} cards)");
        }

        Console.Write("\nSelect Human Player Deck (1-6, default 1): ");
        string? humanDeckInput = Console.ReadLine()?.Trim();
        int humanDeckIdx = int.TryParse(humanDeckInput, out int hd) && hd >= 1 && hd <= starterDecks.Count ? hd - 1 : 0;
        var humanDeck = starterDecks[humanDeckIdx];
        Console.WriteLine($"You selected: {humanDeck.DeckName}");

        Console.WriteLine("\nChoose AI Opponent Deck:");
        Console.WriteLine("  [0] Random Faction Choice [Default]");
        for (int i = 0; i < starterDecks.Count; i++)
        {
            var def = cardDb.GetDefinition(starterDecks[i].WarlordDefinitionId);
            string faction = def?.GetEffectiveFaction() ?? "Neutral";
            Console.WriteLine($"  [{i + 1}] {faction,-16} : {starterDecks[i].DeckName} ({starterDecks[i].TotalCardCount} cards)");
        }

        Console.Write($"Select AI Deck (0-{starterDecks.Count}, default 0): ");
        string? aiDeckInput = Console.ReadLine()?.Trim();
        Deck aiDeck;
        if (int.TryParse(aiDeckInput, out int ad) && ad >= 1 && ad <= starterDecks.Count)
        {
            aiDeck = starterDecks[ad - 1];
        }
        else
        {
            int randomIdx = Random.Shared.Next(0, starterDecks.Count);
            aiDeck = starterDecks[randomIdx];
            Console.WriteLine($"AI randomly selected: {aiDeck.DeckName}");
        }

        int humanPlayerId = 0;
        int aiPlayerId = 1;

        var playerDecks = new[] { humanDeck, aiDeck };

        // Fix 4: Real seeded random d20 roller (1..20, NOT the static 15 test roller)
        var rng = new Random();
        var engine = GameEngine.CreateFromDecks(playerDecks, cardDb, minDeckSize: 50, diceRoller: (count, sides) =>
        {
            int sum = 0;
            for (int i = 0; i < count; i++)
                sum += rng.Next(1, sides + 1);
            return sum;
        });
        engine.SetRandomSeed(Random.Shared.Next());
        engine.ShuffleAllDecks(rng);
        Console.WriteLine("[Setup] Shuffled both player draw decks (50 cards each).");

        BoardGeometry.OnFormationNotice = msg =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(msg);
            Console.ResetColor();
        };

        // Fix 4: Log exact d20 roll, ATK bonus, target AC, Hit/Miss/Crit result, and Wounds dealt
        engine.OnAttackResolved = details =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[COMBAT STRIKE RESOLUTION]");
            Console.WriteLine($"  Attacker: {details.AttackerName} (ID: {details.AttackerInstanceId})");
            Console.WriteLine($"  Target:   {details.DefenderName} (ID: {details.DefenderInstanceId}) [AC: {details.DefenderArmorClass}]");
            Console.WriteLine($"  Attack Roll: d20 ({details.Roll}) + ATK Bonus ({details.StrikeBonus}) = Total {details.TotalRoll} vs AC {details.DefenderArmorClass}");

            if (details.IsCritical)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"  Result:   CRITICAL HIT! (Natural 20 -> Critical Wound bonus!) -> Wounds Dealt: {details.DamageDealt}");
            }
            else if (details.IsCriticalFailure)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("  Result:   CRITICAL MISS! (Natural 1 -> Automatic Miss) -> Wounds Dealt: 0");
            }
            else if (details.IsHit)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  Result:   HIT! ({details.TotalRoll} >= {details.DefenderArmorClass}) -> Wounds Dealt: {details.DamageDealt}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  Result:   MISS! ({details.TotalRoll} < {details.DefenderArmorClass}) -> Wounds Dealt: 0");
            }
            Console.ResetColor();
        };
        ResolveAttackEffect.GlobalOnAttackResolved = engine.OnAttackResolved;

        RollTableEffect.GlobalOnRollTableResolved = (sourceId, roll, minRoll, maxRoll) =>
        {
            string srcName = FindCardSimpleName(engine.State, cardDb, sourceId);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[ROLL ORDER] {srcName} (ID: {sourceId}) rolled d20 = {roll} (matched range {minRoll}-{maxRoll})!");
            Console.ResetColor();
        };

        // Multi-strike dynamic targeting: prompt human player for each strike 1 at a time
        engine.SelectStrikeTarget = prompt =>
        {
            var defCards = prompt.LegalTargets;
            if (defCards.Count == 0) return null;

            if (prompt.Attacker.OwnerPlayerIndex == 0)
            {
                var atkDef = cardDb.GetDefinition(prompt.Attacker.DefinitionId);
                string atkName = atkDef?.Name ?? prompt.Attacker.DefinitionId;
                string bonusStr = prompt.Strike.Modifier >= 0 ? $"+{prompt.Strike.Modifier}" : prompt.Strike.Modifier.ToString();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n======================================================================");
                Console.WriteLine($"  [MULTI-STRIKE TARGETING] Strike #{prompt.StrikeIndex + 1} of {prompt.TotalStrikes} ({bonusStr} {(prompt.Strike.IsRanged ? "Ranged" : "Melee")})");
                Console.WriteLine($"  Attacker: {atkName} (ID: {prompt.Attacker.InstanceId})");
                Console.WriteLine("======================================================================");
                Console.ResetColor();

                Console.WriteLine("Select target for this strike:");
                int defaultIdx = 1;
                for (int i = 0; i < defCards.Count; i++)
                {
                    var c = defCards[i];
                    var cDef = cardDb.GetDefinition(c.DefinitionId);
                    string name = cDef?.Name ?? c.DefinitionId;
                    int maxHp = GameEngine.GetEffectiveMaxHitPoints(prompt.State, c, cardDb);
                    int curHp = Math.Max(0, maxHp - c.Wounds);
                    string targetInfo = $"{name} (Rank {c.Rank}, ID {c.InstanceId}) [AC: {c.CurrentArmorClass}, HP: {curHp}/{maxHp}, Wounds: {c.Wounds}]";
                    Console.WriteLine($"  [{i + 1}] {targetInfo}");

                    if (prompt.PreviousTargetId.HasValue && c.InstanceId == prompt.PreviousTargetId.Value)
                    {
                        defaultIdx = i + 1;
                    }
                }
                Console.WriteLine("  [0] Skip remaining strikes");

                while (true)
                {
                    Console.Write($"Choose target (1-{defCards.Count}, default [{defaultIdx}], 0 to skip): ");
                    string? input = Console.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(input))
                    {
                        return defCards[defaultIdx - 1].InstanceId;
                    }
                    if (int.TryParse(input, out int choice))
                    {
                        if (choice == 0) return null;
                        if (choice >= 1 && choice <= defCards.Count)
                        {
                            return defCards[choice - 1].InstanceId;
                        }
                    }
                    Console.WriteLine("Invalid selection. Please choose a valid target number.");
                }
            }
            else
            {
                if (prompt.PreviousTargetId.HasValue)
                {
                    var prev = defCards.FirstOrDefault(c => c.InstanceId == prompt.PreviousTargetId.Value);
                    if (prev.InstanceId != 0) return prev.InstanceId;
                }

                var best = defCards.OrderBy(c => Math.Max(0, GameEngine.GetEffectiveMaxHitPoints(prompt.State, c, cardDb) - c.Wounds)).First();
                return best.InstanceId;
            }
        };

        // Auto-deploy starting armies for both players to advance through Setup phase
        for (int pIdx = 0; pIdx < 2; pIdx++)
        {
            engine.State.ActivePlayerIndex = pIdx;
            var playerHand = engine.State.Players[pIdx].Hand;

            var warlord = playerHand.First(c => cardDb.GetDefinition(c.DefinitionId)!.IsWarlord);
            var lvl2s = playerHand.Where(c => !cardDb.GetDefinition(c.DefinitionId)!.IsWarlord && cardDb.GetDefinition(c.DefinitionId)!.Level == 2).Take(2).ToList();
            var lvl1s = playerHand.Where(c => !cardDb.GetDefinition(c.DefinitionId)!.IsWarlord && cardDb.GetDefinition(c.DefinitionId)!.Level == 1).Take(3).ToList();

            var deployArmy = new DeployStartingArmyAction
            {
                PlayerIndex = pIdx,
                Deployments =
                [
                    new StartingCharacterDeployment(warlord.InstanceId, 3),
                    new StartingCharacterDeployment(lvl2s[0].InstanceId, 2),
                    new StartingCharacterDeployment(lvl2s[1].InstanceId, 2),
                    new StartingCharacterDeployment(lvl1s[0].InstanceId, 1),
                    new StartingCharacterDeployment(lvl1s[1].InstanceId, 1),
                    new StartingCharacterDeployment(lvl1s[2].InstanceId, 1)
                ]
            };

            var canExec = engine.CanExecute(deployArmy);
            if (canExec.IsSuccess)
            {
                engine.Execute(deployArmy);
            }
        }

        Console.WriteLine("\nStarting match! Entering the battlefield...");

        int lastPrintedInitiativeTurn = 0;

        // Main game loop
        while (!engine.State.IsGameOver)
        {
            // Automatic transitions for Ready and Draw phases (with optional pre-draw discard for human player)
            while (!engine.State.IsGameOver && engine.State.CurrentPhase is TurnPhase.Ready or TurnPhase.Draw)
            {
                if (engine.State.CurrentPhase == TurnPhase.Ready && engine.State.TurnNumber >= 1)
                {
                    var humanPlayer = engine.State.Players[humanPlayerId];
                    while (humanPlayer.Hand.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("\n======================================================================");
                        Console.WriteLine("             DRAW PHASE - OPTIONAL PRE-DRAW DISCARD                   ");
                        Console.WriteLine("======================================================================");
                        Console.ResetColor();
                        Console.WriteLine($"You have {humanPlayer.Hand.Count} card(s) in hand. You may discard unwanted cards before drawing up to 5:");
                        for (int i = 0; i < humanPlayer.Hand.Count; i++)
                        {
                            var c = humanPlayer.Hand[i];
                            var def = cardDb.GetDefinition(c.DefinitionId);
                            string name = def?.Name ?? c.DefinitionId;
                            string type = def?.Type.ToString() ?? "Unknown";
                            Console.WriteLine($"  [{i + 1}] Discard {name} [{type}] (ID: {c.InstanceId})");
                        }
                        Console.WriteLine("  [0] Done discarding / Pass (Draw up to 5 cards)");
                        Console.Write($"Select card to discard (0-{humanPlayer.Hand.Count}, default [0]): ");
                        string? discInput = Console.ReadLine()?.Trim();
                        if (string.IsNullOrEmpty(discInput) || discInput == "0")
                        {
                            break;
                        }
                        if (int.TryParse(discInput, out int discChoice) && discChoice >= 1 && discChoice <= humanPlayer.Hand.Count)
                        {
                            var discarded = humanPlayer.Hand[discChoice - 1];
                            string discName = cardDb.GetDefinition(discarded.DefinitionId)?.Name ?? discarded.DefinitionId;
                            engine.DiscardCardFromHand(humanPlayerId, discarded.InstanceId);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"  -> Discarded {discName} (ID: {discarded.InstanceId}).");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.WriteLine("Invalid selection. Enter a card number or 0 to finish.");
                        }
                    }
                }

                Console.WriteLine($"[Phase: {engine.State.CurrentPhase}] Advancing phase...");
                engine.AdvancePhase();
            }

            if (engine.State.IsGameOver)
                break;

            // Fix 2: Initiative Phase d20 Roll at start of Turn 1 and after Ready/Draw phases
            if (engine.State.CurrentPhase == TurnPhase.Main && lastPrintedInitiativeTurn != engine.State.TurnNumber)
            {
                lastPrintedInitiativeTurn = engine.State.TurnNumber;
                var initRoll = engine.LastInitiativeRoll ?? engine.RollInitiative();
                Console.WriteLine("\n======================================================================");
                Console.WriteLine($"             INITIATIVE PHASE - TURN {engine.State.TurnNumber}        ");
                Console.WriteLine("======================================================================");
                foreach (var tie in engine.InitiativeTieHistory)
                {
                    Console.WriteLine($"  [Tie] Player 1 rolled {tie.Roll0}, Player 2 rolled {tie.Roll1}. Rerolling initiative...");
                }
                Console.WriteLine($"  Player 1 ({engine.State.Players[0].DisplayName}) rolled: 1d20 = {initRoll.Roll0}");
                Console.WriteLine($"  Player 2 ({engine.State.Players[1].DisplayName}) rolled: 1d20 = {initRoll.Roll1}");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  >>> Winner: Player {initRoll.WinnerIndex + 1} ({engine.State.Players[initRoll.WinnerIndex].DisplayName}) wins Initiative and has first Decree! <<<");
                Console.ResetColor();
                Console.WriteLine("======================================================================\n");
            }

            int actingPlayer = engine.State.Priority.IsReactWindowOpen
                ? engine.State.Priority.ActivePlayerIndex
                : engine.State.ActivePlayerIndex;

            if (actingPlayer == humanPlayerId)
            {
                // Human player turn or react priority
                RenderScreen(engine.State, cardDb, humanPlayerId, aiPlayerId, aiAgent.Name);

                var legalActions = engine.GetLegalActions(engine.State, humanPlayerId);
                if (legalActions.Count == 0)
                {
                    Console.WriteLine("\nNo legal actions available. Passing priority automatically...");
                    var autoPass = new PassAction { PlayerIndex = humanPlayerId };
                    engine.ExecuteAction(autoPass);
                    continue;
                }

                if (engine.State.Priority.IsReactWindowOpen)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n┌──────────────────────────────────────────────────────────────────┐");
                    Console.WriteLine("│ [REACT WINDOW OPEN]                                              │");
                    Console.WriteLine("│ An action or strike is pending resolution on the stack!          │");
                    Console.WriteLine("│ You may respond with a [React] ability, or Pass to resolve it.   │");
                    Console.WriteLine("└──────────────────────────────────────────────────────────────────┘");
                    Console.ResetColor();
                }

                var groups = GroupLegalActions(legalActions, engine.State, cardDb);

                Console.WriteLine(engine.State.Priority.IsReactWindowOpen
                    ? "\nAvailable React Actions:"
                    : $"\nLegal Actions (Round {engine.State.TurnNumber} - Choose 1 Order/Decree):");

                for (int i = 0; i < groups.Count; i++)
                {
                    Console.WriteLine($"  [{i + 1}] {groups[i].DisplayTitle}");
                }

                Console.Write("\nEnter action number (or 'auto' for AI recommendation, 'q' to quit): ");
                string? input = Console.ReadLine()?.Trim();

                if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Exiting match. Goodbye!");
                    return;
                }

                IPlayerAction chosenAction;
                if (string.Equals(input, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Evaluating best move with AI...");
                    chosenAction = aiAgent.SelectAction(engine.State, engine, humanPlayerId);
                    Console.WriteLine($"Auto-selected: {FormatAction(chosenAction, engine.State, cardDb)}");
                }
                else if (int.TryParse(input, out int selectedIdx) && selectedIdx >= 1 && selectedIdx <= groups.Count)
                {
                    var chosenGroup = groups[selectedIdx - 1];
                    if (chosenGroup.Items.Count == 1)
                    {
                        chosenAction = chosenGroup.Items[0].Action;
                    }
                    else
                    {
                        Console.WriteLine($"\n--- Options for: {chosenGroup.DisplayTitle} ---");
                        for (int j = 0; j < chosenGroup.Items.Count; j++)
                        {
                            Console.WriteLine($"  [{j + 1}] {chosenGroup.Items[j].SubLabel}");
                        }
                        Console.WriteLine("  [0] Back");
                        Console.Write("Select option (0 to return to main actions): ");
                        string? subInput = Console.ReadLine()?.Trim();

                        if (subInput == "0" || string.IsNullOrWhiteSpace(subInput))
                        {
                            continue; // Return to main action list without executing
                        }

                        if (int.TryParse(subInput, out int subIdx) && subIdx >= 1 && subIdx <= chosenGroup.Items.Count)
                        {
                            chosenAction = chosenGroup.Items[subIdx - 1].Action;
                        }
                        else
                        {
                            Console.WriteLine("Invalid selection. Returning to main menu.");
                            continue;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Invalid selection. Please choose a valid action number.");
                    continue;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nYou executed: {FormatAction(chosenAction, engine.State, cardDb)}");
                Console.ResetColor();

                var execResult = engine.ExecuteAction(chosenAction);
                if (!execResult.IsSuccess)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Action execution error: {string.Join("; ", execResult.Errors)}");
                    Console.ResetColor();
                }
            }
            else
            {
                // AI player turn or react priority
                if (engine.State.Priority.IsReactWindowOpen)
                {
                    Console.WriteLine($"\n--- AI Priority [React Window] ({aiAgent.Name}) ---");
                    Console.WriteLine("AI is checking for reactions...");
                    var aiAction = aiAgent.SelectAction(engine.State, engine, actingPlayer);
                    Console.WriteLine($"AI chose: {FormatAction(aiAction, engine.State, cardDb)}");

                    var execResult = engine.ExecuteAction(aiAction);
                    if (!execResult.IsSuccess)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"AI action error: {string.Join("; ", execResult.Errors)}");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\n======================================================================");
                    Console.WriteLine($"  [AI DECREE - ROUND {engine.State.TurnNumber}] Player 2 ({aiAgent.Name})");
                    Console.WriteLine("======================================================================");
                    Console.ResetColor();
                    Console.WriteLine("Thinking...");

                    var aiAction = aiAgent.SelectAction(engine.State, engine, actingPlayer);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"AI Order: {FormatAction(aiAction, engine.State, cardDb)}");
                    Console.ResetColor();

                    var execResult = engine.ExecuteAction(aiAction);
                    if (!execResult.IsSuccess)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"AI action error: {string.Join("; ", execResult.Errors)}");
                        Console.ResetColor();
                    }
                }
            }
        }

        // Game Over Announcement
        Console.WriteLine("\n======================================================================");
        Console.WriteLine("                             GAME OVER!                               ");
        Console.WriteLine("======================================================================");
        if (engine.State.WinningPlayerIndex == humanPlayerId)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("VICTORY! You have eliminated the enemy Warlord and won the game!");
        }
        else if (engine.State.WinningPlayerIndex == aiPlayerId)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("DEFEAT! Your Warlord has fallen. Better luck next time!");
        }
        else
        {
            Console.WriteLine("The game ended in a draw.");
        }
        Console.ResetColor();
        Console.WriteLine($"Reason: {engine.State.GameOverReason}");
    }

    private static void RenderScreen(
        GameState state,
        CardDatabase cardDb,
        int humanPlayerId,
        int aiPlayerId,
        string aiName)
    {
        Console.WriteLine("\n══════════════════════════════════════════════════════════════════════");
        string priorityNotice = state.Priority.IsReactWindowOpen ? " [REACT WINDOW OPEN]" : "";
        Console.WriteLine($"  ROUND (TURN) {state.TurnNumber} | Phase: {state.CurrentPhase} | Current Decree: Player {state.ActivePlayerIndex + 1}{priorityNotice}");
        Console.WriteLine("══════════════════════════════════════════════════════════════════════");

        var aiPlayer = state.Players[aiPlayerId];
        var humanPlayer = state.Players[humanPlayerId];

        string topAiDiscard = aiPlayer.DiscardPile.Count > 0
            ? (cardDb.GetDefinition(aiPlayer.DiscardPile[^1].DefinitionId)?.Name ?? aiPlayer.DiscardPile[^1].DefinitionId)
            : "None";

        // AI Ranks (Highest down to 1)
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"--- AI: {aiName} (Player {aiPlayerId + 1}) --- [Hand: {aiPlayer.Hand.Count} | Deck: {aiPlayer.Deck.Count} | Discard: {aiPlayer.DiscardPile.Count} (Top: {topAiDiscard})]");
        Console.ResetColor();
        int maxAiRank = Math.Max(1, aiPlayer.Ranks.Where(kvp => kvp.Value.Count > 0).Select(kvp => kvp.Key).DefaultIfEmpty(1).Max());
        for (int r = maxAiRank; r >= 1; r--)
        {
            bool hasCards = aiPlayer.Ranks.TryGetValue(r, out var rankCards) && rankCards.Count > 0;
            if (r == 1 || hasCards)
            {
                Console.WriteLine($"  Rank {r}:");
                if (hasCards)
                {
                    foreach (var card in rankCards!)
                    {
                        Console.WriteLine($"    • {FormatCharacter(card, aiPlayer, cardDb, state)}");
                    }
                }
                else
                {
                    Console.WriteLine("    (empty)");
                }
            }
        }

        Console.WriteLine("──────────────────────────────────────────────────────────────────────");

        string topHumanDiscard = humanPlayer.DiscardPile.Count > 0
            ? (cardDb.GetDefinition(humanPlayer.DiscardPile[^1].DefinitionId)?.Name ?? humanPlayer.DiscardPile[^1].DefinitionId)
            : "None";

        // Player Ranks (1 up to Highest)
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"--- Player {humanPlayerId + 1} (You) --- [Hand: {humanPlayer.Hand.Count} | Deck: {humanPlayer.Deck.Count} | Discard: {humanPlayer.DiscardPile.Count} (Top: {topHumanDiscard})]");
        Console.ResetColor();
        int maxHumanRank = Math.Max(1, humanPlayer.Ranks.Where(kvp => kvp.Value.Count > 0).Select(kvp => kvp.Key).DefaultIfEmpty(1).Max());
        for (int r = 1; r <= maxHumanRank; r++)
        {
            bool hasCards = humanPlayer.Ranks.TryGetValue(r, out var rankCards) && rankCards.Count > 0;
            if (r == 1 || hasCards)
            {
                Console.WriteLine($"  Rank {r}:");
                if (hasCards)
                {
                    foreach (var card in rankCards!)
                    {
                        Console.WriteLine($"    • {FormatCharacter(card, humanPlayer, cardDb, state)}");
                    }
                }
                else
                {
                    Console.WriteLine("    (empty)");
                }
            }
        }

        Console.WriteLine("══════════════════════════════════════════════════════════════════════");

        // Player's Hand
        Console.WriteLine($"Player {humanPlayerId + 1}'s Hand ({humanPlayer.Hand.Count} cards):");
        if (humanPlayer.Hand.Count == 0)
        {
            Console.WriteLine("  (hand is empty)");
        }
        else
        {
            for (int i = 0; i < humanPlayer.Hand.Count; i++)
            {
                var card = humanPlayer.Hand[i];
                var def = cardDb.GetDefinition(card.DefinitionId);
                string name = def?.Name ?? card.DefinitionId;
                string type = def?.Type.ToString() ?? "Unknown";
                string extra = def?.Type == CardType.Character
                    ? $" | Lv {def.Level} | ATK {def.Attack?.RawText ?? "+0"} | AC {def.ArmorClass ?? 10}"
                    : "";
                Console.WriteLine($"  ({i + 1}) {name} [{type}{extra}] (ID: {card.InstanceId})");
            }
        }
    }

    private static string FormatCharacter(CardInstance card, PlayerState player, CardDatabase cardDb, GameState? state = null)
    {
        var def = cardDb.GetDefinition(card.DefinitionId);
        string name = def?.Name ?? card.DefinitionId;
        bool isWarlord = (player.WarlordInstanceId.HasValue && card.InstanceId == player.WarlordInstanceId.Value) || (def?.IsWarlord ?? false);
        string warlordTag = isWarlord ? " [WARLORD]" : "";

        int level = def?.Level ?? card.CurrentSkill;

        int itemAtk = 0;
        int itemAc = 0;
        int itemHp = 0;
        var itemNames = new List<string>();

        foreach (var itemId in card.AttachedItemIds)
        {
            var itemCard = player.DiscardPile.FirstOrDefault(c => c.InstanceId == itemId);
            var itemDef = itemCard.DefinitionId != null ? cardDb.GetDefinition(itemCard.DefinitionId) : null;
            if (itemDef != null)
            {
                var stats = GameEngine.GetItemPassiveStats(itemDef);
                itemAtk += stats.Atk;
                itemAc += stats.Ac;
                itemHp += stats.Hp;
                itemNames.Add(itemDef.Name);
            }
        }

        string baseAtk = def?.Attack?.RawText ?? "+0";
        int baseFirstMod = (def?.Attack != null && def.Attack.Strikes.Count > 0) ? def.Attack.Strikes[0].Modifier : 0;
        int effAtkMod = state != null
            ? GameEngine.GetEffectiveAttack(state, card, null, cardDb)
            : baseFirstMod + itemAtk;
        int bonusAtk = effAtkMod - baseFirstMod;
        string atk = bonusAtk > 0
            ? $"{baseAtk} (+{bonusAtk} Buff/Item)"
            : (bonusAtk < 0 ? $"{baseAtk} ({bonusAtk})" : baseAtk);
        int baseAc = def?.ArmorClass ?? 10;
        int ac = state != null
            ? GameEngine.GetEffectiveArmorClass(state, card, null, cardDb)
            : Math.Max(card.CurrentArmorClass, baseAc + itemAc);
        int maxHp = state != null
            ? GameEngine.GetEffectiveMaxHitPoints(state, card, cardDb)
            : (def?.BaseHitPoints ?? card.CurrentHitPoints) + itemHp;
        int wounds = card.Wounds;
        int currentHp = Math.Max(0, maxHp - wounds);
        string itemStr = itemNames.Count > 0 ? $" [Items: {string.Join(", ", itemNames)}]" : "";

        return $"{name}{warlordTag} (Lv {level} | ATK {atk} | AC {ac} | HP {currentHp}/{maxHp} | Wounds: {wounds} | {card.ReadyState}){itemStr} [ID: {card.InstanceId}]";
    }

    public static string FormatAction(IPlayerAction action, GameState state, CardDatabase cardDb)
    {
        return action switch
        {
            AttackAction atk => FormatAttack(atk, state, cardDb),
            DeclareAttackAction dec => FormatDeclareAttack(dec, state, cardDb),
            PlayCardAction play => FormatPlayCard(play, state, cardDb),
            DeployCharacterAction dep => $"Deploy character {dep.DefinitionId} to Rank {dep.TargetRank}",
            MarchAction march => $"March {FindCardSimpleName(state, cardDb, march.CardInstanceId)} (ID: {march.CardInstanceId}) from Rank {march.SourceRank} to Rank {march.DestinationRank}",
            UseAbilityAction ability => FormatUseAbility(ability, state, cardDb),
            DiscardCardAction disc => $"Discard {FindCardSimpleName(state, cardDb, disc.CardInstanceId)} (ID: {disc.CardInstanceId}) from hand",
            PassAction => state.Priority.IsReactWindowOpen ? "Pass priority (Allow pending action to resolve)" : "Pass Decree (End orders for this round)",
            PassPriorityAction => "Pass priority (Allow pending action to resolve)",
            _ => action.GetType().Name
        };
    }

    private static string FormatUseAbility(UseAbilityAction ability, GameState state, CardDatabase cardDb)
    {
        string cardName = FindCardName(state, cardDb, ability.SourceInstanceId);
        CardDefinition? def = null;

        foreach (var player in state.Players)
        {
            foreach (var rankCards in player.Ranks.Values)
            {
                var found = rankCards.Cast<CardInstance?>().FirstOrDefault(c => c?.InstanceId == ability.SourceInstanceId);
                if (found.HasValue)
                {
                    def = cardDb.GetDefinition(found.Value.DefinitionId);
                    break;
                }
            }
            if (def != null) break;
        }

        if (def != null && ability.AbilityIndex >= 0 && ability.AbilityIndex < def.Abilities.Count)
        {
            var abDef = def.Abilities[ability.AbilityIndex];
            string triggerText = abDef.Trigger switch
            {
                AbilityTrigger.Order => "Order",
                AbilityTrigger.SpendOrder => "Spend Order",
                AbilityTrigger.React => "React",
                AbilityTrigger.SpendReact => "Spend React",
                _ => abDef.Trigger.ToString()
            };

            string desc = !string.IsNullOrWhiteSpace(abDef.Description) ? abDef.Description : triggerText;
            string targetStr = "";
            if (ability.TargetInstanceIds != null && ability.TargetInstanceIds.Length > 0)
            {
                string tNames = string.Join(", ", ability.TargetInstanceIds.Select(id => FindCardName(state, cardDb, id)));
                targetStr = $" targeting {tNames}";
            }
            return $"{triggerText}: \"{desc}\" [on {cardName}]{targetStr}";
        }

        return $"Use ability #{ability.AbilityIndex + 1} on {cardName}";
    }

    private static string FormatAttack(AttackAction atk, GameState state, CardDatabase cardDb)
    {
        string attacker = FindCardName(state, cardDb, atk.AttackerInstanceId);
        string target = FindCardName(state, cardDb, atk.TargetInstanceId);
        return $"Attack with {attacker} against {target}";
    }

    private static string FormatDeclareAttack(DeclareAttackAction dec, GameState state, CardDatabase cardDb)
    {
        string attacker = FindCardName(state, cardDb, dec.AttackerInstanceId);
        string defender = FindCardName(state, cardDb, dec.DefenderInstanceId);
        return $"Declare attack: {attacker} against {defender}";
    }

    private static string FormatPlayCard(PlayCardAction play, GameState state, CardDatabase cardDb)
    {
        var player = state.Players.ElementAtOrDefault(play.PlayerIndex);
        var card = player?.Hand.Cast<CardInstance?>().FirstOrDefault(c => c?.InstanceId == play.CardInstanceId);
        var def = card.HasValue ? cardDb.GetDefinition(card.Value.DefinitionId) : null;
        string cardName = def?.Name ?? (card.HasValue ? card.Value.DefinitionId : $"Card #{play.CardInstanceId}");

        if (def?.Type == CardType.Action)
        {
            string performerStr = play.PerformerInstanceId.HasValue
                ? FindCardName(state, cardDb, play.PerformerInstanceId.Value)
                : "None";

            if (play.TargetCharacterId.HasValue)
            {
                string targetName = FindCardName(state, cardDb, play.TargetCharacterId.Value);
                return $"Play Action '{cardName}' [Performer: {performerStr} -> Target: {targetName}]";
            }
            return $"Play Action '{cardName}' [Performer: {performerStr}]";
        }

        if (play.TargetRank.HasValue)
            return $"Play {cardName} into Rank {play.TargetRank.Value}";
        if (play.TargetCharacterId.HasValue)
        {
            string targetName = FindCardName(state, cardDb, play.TargetCharacterId.Value);
            return $"Play {cardName} targeting {targetName}";
        }
        return $"Play {cardName}";
    }

    private static CardInstance? FindBoardCard(GameState state, int instanceId)
    {
        foreach (var player in state.Players)
        {
            foreach (var cards in player.Ranks.Values)
            {
                foreach (var card in cards)
                {
                    if (card.InstanceId == instanceId)
                        return card;
                }
            }
        }
        return null;
    }

    private static string FindCardName(GameState state, CardDatabase cardDb, int instanceId)
    {
        foreach (var player in state.Players)
        {
            foreach (var (rank, cards) in player.Ranks)
            {
                foreach (var card in cards)
                {
                    if (card.InstanceId == instanceId)
                    {
                        var def = cardDb.GetDefinition(card.DefinitionId);
                        string name = def?.Name ?? card.DefinitionId;
                        return $"{name} (Rank {rank}, ID {instanceId})";
                    }
                }
            }
        }
        return $"Card #{instanceId}";
    }

    private static string FindCardSimpleName(GameState state, CardDatabase cardDb, int instanceId)
    {
        foreach (var player in state.Players)
        {
            foreach (var cards in player.Ranks.Values)
            {
                foreach (var card in cards)
                {
                    if (card.InstanceId == instanceId)
                    {
                        var def = cardDb.GetDefinition(card.DefinitionId);
                        return def?.Name ?? card.DefinitionId;
                    }
                }
            }
        }
        return $"Card #{instanceId}";
    }

    public sealed class ActionGroup
    {
        public required string DisplayTitle { get; init; }
        public required List<(IPlayerAction Action, string SubLabel)> Items { get; init; }
    }

    public static List<ActionGroup> GroupLegalActions(IReadOnlyList<IPlayerAction> legalActions, GameState state, CardDatabase cardDb)
    {
        var groups = new List<ActionGroup>();
        var dict = new Dictionary<string, (string TitlePrefix, List<(IPlayerAction Action, string SubLabel)> Items)>();

        foreach (var action in legalActions)
        {
            switch (action)
            {
                case PassAction:
                case PassPriorityAction:
                {
                    string label = FormatAction(action, state, cardDb);
                    groups.Add(new ActionGroup
                    {
                        DisplayTitle = label,
                        Items = [(action, label)]
                    });
                    break;
                }

                case AttackAction atk:
                {
                    string key = $"attack_{atk.AttackerInstanceId}";
                    string attackerName = FindCardName(state, cardDb, atk.AttackerInstanceId);
                    string targetName = FindCardName(state, cardDb, atk.TargetInstanceId);
                    if (!dict.TryGetValue(key, out var entry))
                    {
                        entry = ($"Attack with {attackerName}", new List<(IPlayerAction, string)>());
                        dict[key] = entry;
                    }
                    entry.Items.Add((atk, $"Target: {targetName}"));
                    break;
                }

                case DeclareAttackAction dec:
                {
                    string key = $"declare_{dec.AttackerInstanceId}";
                    string attackerName = FindCardName(state, cardDb, dec.AttackerInstanceId);
                    string targetName = FindCardName(state, cardDb, dec.DefenderInstanceId);
                    if (!dict.TryGetValue(key, out var entry))
                    {
                        entry = ($"Declare attack: {attackerName}", new List<(IPlayerAction, string)>());
                        dict[key] = entry;
                    }
                    entry.Items.Add((dec, $"Defender: {targetName}"));
                    break;
                }

                case PlayCardAction play:
                {
                    var player = state.Players.ElementAtOrDefault(play.PlayerIndex);
                    var card = player?.Hand.FirstOrDefault(c => c.InstanceId == play.CardInstanceId);
                    var def = card.HasValue ? cardDb.GetDefinition(card.Value.DefinitionId) : null;
                    string cardName = def?.Name ?? $"Card #{play.CardInstanceId}";

                    if (def?.Type == CardType.Item)
                    {
                        string key = $"play_item_{play.CardInstanceId}";
                        string targetName = play.TargetCharacterId.HasValue ? FindCardName(state, cardDb, play.TargetCharacterId.Value) : "No target";
                        if (!dict.TryGetValue(key, out var entry))
                        {
                            entry = ($"Play Item '{cardName}'", new List<(IPlayerAction, string)>());
                            dict[key] = entry;
                        }
                        entry.Items.Add((play, $"Equip to {targetName}"));
                    }
                    else if (def?.Type == CardType.Action)
                    {
                        string key = $"play_action_{play.CardInstanceId}";
                        string perfStr = play.PerformerInstanceId.HasValue ? FindCardName(state, cardDb, play.PerformerInstanceId.Value) : "None";
                        string targetStr = play.TargetCharacterId.HasValue ? $" -> Target: {FindCardName(state, cardDb, play.TargetCharacterId.Value)}" : "";
                        if (!dict.TryGetValue(key, out var entry))
                        {
                            entry = ($"Play Action '{cardName}'", new List<(IPlayerAction, string)>());
                            dict[key] = entry;
                        }
                        entry.Items.Add((play, $"Performer: {perfStr}{targetStr}"));
                    }
                    else
                    {
                        string key = $"play_char_{play.CardInstanceId}";
                        string rankStr = play.TargetRank.HasValue ? $"into Rank {play.TargetRank.Value}" : "";
                        if (!dict.TryGetValue(key, out var entry))
                        {
                            entry = ($"Play {cardName}", new List<(IPlayerAction, string)>());
                            dict[key] = entry;
                        }
                        entry.Items.Add((play, $"Deploy {rankStr}"));
                    }
                    break;
                }

                case MarchAction march:
                {
                    string cardName = FindCardSimpleName(state, cardDb, march.CardInstanceId);
                    string key = $"march_{march.CardInstanceId}";
                    if (!dict.TryGetValue(key, out var entry))
                    {
                        entry = ($"March {cardName} (Rank {march.SourceRank}, ID {march.CardInstanceId})", new List<(IPlayerAction, string)>());
                        dict[key] = entry;
                    }
                    entry.Items.Add((march, $"March from Rank {march.SourceRank} to Rank {march.DestinationRank}"));
                    break;
                }

                case UseAbilityAction ab:
                {
                    string sourceName = FindCardName(state, cardDb, ab.SourceInstanceId);
                    string key = $"ability_{ab.SourceInstanceId}_{ab.AbilityIndex}";
                    string fullLabel = FormatUseAbility(ab, state, cardDb);
                    if (!dict.TryGetValue(key, out var entry))
                    {
                        entry = (fullLabel, new List<(IPlayerAction, string)>());
                        dict[key] = entry;
                    }
                    string subTarget = (ab.TargetInstanceIds != null && ab.TargetInstanceIds.Length > 0)
                        ? $"Target: {string.Join(", ", ab.TargetInstanceIds.Select(id => FindCardName(state, cardDb, id)))}"
                        : "Activate";
                    entry.Items.Add((ab, subTarget));
                    break;
                }

                default:
                {
                    string label = FormatAction(action, state, cardDb);
                    groups.Add(new ActionGroup
                    {
                        DisplayTitle = label,
                        Items = [(action, label)]
                    });
                    break;
                }
            }
        }

        foreach (var (key, (prefix, items)) in dict)
        {
            string title;
            if (items.Count == 1)
            {
                if (key.StartsWith("attack_") && items[0].Action is AttackAction singleAtk)
                {
                    title = FormatAttack(singleAtk, state, cardDb);
                }
                else if (key.StartsWith("march_") && items[0].Action is MarchAction singleMarch)
                {
                    title = FormatAction(singleMarch, state, cardDb);
                }
                else if (key.StartsWith("play_item_") && items[0].Action is PlayCardAction singleItem)
                {
                    title = FormatPlayCard(singleItem, state, cardDb);
                }
                else if (key.StartsWith("play_action_") && items[0].Action is PlayCardAction singleAct)
                {
                    title = FormatPlayCard(singleAct, state, cardDb);
                }
                else
                {
                    title = prefix;
                }
            }
            else
            {
                if (key.StartsWith("attack_"))
                {
                    title = $"{prefix} ({items.Count} targets)";
                }
                else if (key.StartsWith("play_item_"))
                {
                    title = $"{prefix} ({items.Count} valid characters)";
                }
                else if (key.StartsWith("play_action_"))
                {
                    var distinctPerformers = items.Select(i => ((PlayCardAction)i.Action).PerformerInstanceId).Distinct().Count();
                    title = distinctPerformers > 1 ? $"{prefix} ({distinctPerformers} performers)" : $"{prefix} ({items.Count} options)";
                }
                else if (key.StartsWith("march_"))
                {
                    title = $"{prefix} ({items.Count} options)";
                }
                else
                {
                    title = $"{prefix} ({items.Count} options)";
                }
            }

            groups.Add(new ActionGroup
            {
                DisplayTitle = title,
                Items = items
            });
        }

        return groups;
    }

    private static string? FindCardsJsonPath()
    {
        string[] candidates =
        [
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataParser", "cards.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "DataParser", "cards.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "DataParser", "cards.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DataParser", "cards.json"),
            @"C:\Users\iamfr\Documents\WarlordCCG\DataParser\cards.json"
        ];

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }

        return null;
    }
}
