\# Warlord: Saga of the Storm — Tabletop Rules Engine (.NET 9 / C#)



An automated rules engine, simulation testbed, and CLI implementation for \*\*Warlord: Saga of the Storm\*\* (Into the Accordlands / 5E tabletop collectible card game system). 



This project implements a complete, rule-accurate (in progress) state machine modeling formations, ranks, movement, equipping, deck validation, initiative, turn phasing, and tactical combat resolution in C#.



\---



\## Project Structure



The solution (`WarlordCCG.sln`) is organized into clean architectural layers under `src/`:

\* \*\*`src/Actions/`\*\*: Player action definitions (Play Card, Attack, March, Deploy, Pass).

\* \*\*`src/Effects/`\*\*: Effect resolution logic (Strikes, Spells, Saves, Healing, Rolls).

\* \*\*`src/Engine/`\*\*: Core game state machine, rules parser, board geometry, and death checks.

\* \*\*`src/Events/`\*\*: Event publishing and resolution pipeline.

\* \*\*`src/AI/`\*\*: Heuristic and Mcts (Monte Carlo Tree Search) AI agents for automated playtests and simulations.

\* \*\*`tests/WarlordEngine.Tests/`\*\*: Comprehensive xUnit test suite covering tabletop rules and edge cases.

\* \*\*`WarlordCli/`\*\*: Interactive command-line interface for running matches and observing AI vs. AI or human vs. AI play.



\---



\## Key Tabletop Features Implemented



\* \*\*Formation \& Rank Geometry:\*\* Formations supporting up to 4 ranks, illegal rank enforcement ($\\text{Count}(R) \\le \\text{Count}(R-1)$), falling-forward orientation penalties, and cross-board distance calculations.

\* \*\*Unified Order Phase:\*\* Alternating single-order mechanics (Deploy, Equip, Maneuver, Action Card, Attack) with two-consecutive-pass turn ending.

\* \*\*Combat \& Strike Resolution:\*\* Melee and ranged strike mechanics (including exact 2-rank ranged restrictions), Armor Class verification, Natural 20/1 handling on strikes and saving throws, and True Strike uncancelable/unredirectable rules.

\* \*\*Deck Validation:\*\* Minimum 50-card limits, card type distributions, faction matching for starting armies, Epic copy restrictions, and Unique card uniqueness constraints.



\---



\## Prerequisites



\* \*\*.NET 9 SDK\*\* (or compatible .NET 8/9 runtime environment).



\---



\## Getting Started \& Running Locally



1\. \*\*Clone the repository:\*\*

&#x20;  git clone \[https://github.com/iamfritch/Warlord-CCG.git](https://github.com/iamfritch/Warlord-CCG.git)

&#x20;  cd Warlord-CCG



2\. Restore dependencies and build the solution:

&#x20;   dotnet restore

&#x20;   dotnet build



3\. Run the Unit Test Suite: To verify all core engine rules and test coverage:

&#x20;   dotnet test tests/WarlordEngine.Tests/WarlordEngine.Tests.csproj



4\. Run the Interactive CLI: To launch the CLI application:

&#x20;   dotnet run --project WarlordCli/WarlordCli.csproj



License

This project is developed for personal, educational, and open-source tabletop simulation purposes. Card names, rules concepts, and universe references belong to their respective copyright holders.

