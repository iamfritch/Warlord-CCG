Warlord CCG — Live Playtest \& Code Review Log



Session Info

&#x20;   Date: \[Insert Date]

&#x20;   Engine Version / Git Commit: \[Insert Commit Hash]

&#x20;   Focus Areas: Combat Resolution, React Windows, Target Filters, Unparsed Card Patterns



1\. Active Issue \& Bug Tracker

ID      Category        Description / Observed Behavior                             Expected Behavior                                   Associated Files / Cards                Status

B-01    Combat / Range  e.g., Ranged strike ignoring rank distance limits           Should validate max range against board geometry	GameEngine.cs, PerformStrikeEffect.cs	Open

B-02    React Window    e.g., Priority passing incorrectly during reaction timing   Should open react window for valid target/trigger   EventResolver.cs, GameEngine.cs         Open

B-03    Target Filter   e.g., Multi-target ability allowing duplicate targets       Should enforce distinct target selection            RulesParser.cs, GameEngine.cs           Open

B-04    Card Effect                                                                                                                                                             Open



2\. Unparsed Card Pattern Log

Log any text patterns from the startup diagnostics or unhandled card text encountered during playtesting:

&#x20;   \[ ] Pattern 1: \[Insert card text or pattern]

&#x20;       Affected Card(s):

&#x20;       Notes / Required Parser Rule:

&#x20;   \[ ] Pattern 2: \[Insert card text or pattern]

&#x20;       Affected Card(s):

&#x20;       Notes / Required Parser Rule:



3\. Manual Code Review Checklist (Guided by CODEBASE\_MAP.md)

Check off sections as you review them against your architectural map:

&#x20;   \[ ] RulesParser.cs \& Data Ingestion: Verify regex patterns and metadata parsing (ensuring no hardcoded card-name hacks crept back in).

&#x20;   \[ ] GameEngine.cs (Core Loop \& Validation): Audit action execution flows (CanExecute, ExecuteAction), multi-target validation, and state mutators.

&#x20;   \[ ] Combat \& Keyword Resolution: Confirm proper handling of Stealth, Riposte, Magic Resistance, and strike modifiers.

&#x20;   \[ ] EventResolver.cs \& Handlers: Review post-resolution and interrupt handlers (death checks, damage mitigation, react windows).

