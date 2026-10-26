# Starter Deck Completeness Audit Report

**Total Unique Cards Audited:** 114
- **Category 1 (100% Fully Implemented):** 20 cards (17.5%)
- **Category 2 (Partially Implemented):** 44 cards (38.6%)
- **Category 3 (Completely Unimplemented):** 50 cards (43.9%)

---

## Category 1: 100% Fully Implemented Cards

**Count:** 20

| Card Name | Type | Rules Text Summary / Status |
| :--- | :--- | :--- |
| **Aureliane** | Character | Spend Order: Heal an adjacent higher level Deverenian Empire character. |
| **Bardiche** | Item | Vanilla (Combat stats & strikes modeled) |
| **Blast** | Action | Order: Perform a +3 ranged strike. |
| **Brine Fiend** | Character | Vanilla (Combat stats & strikes modeled) |
| **Bruntor's Helm** | Item | +1 HP |
| **Catapult Crew** | Character | Spend Order: Perform a +5 ranged strike that may target an additional rank away. |
| **Church Inquisitor** | Character | Vanilla (Combat stats & strikes modeled) |
| **Cleave** | Action | React: Before this character makes a melee strike roll: The strike inflicts an additional wound. |
| **Flame Jester** | Character | Vanilla (Combat stats & strikes modeled) |
| **Heresy Huntress** | Character | Vanilla (Combat stats & strikes modeled) |
| **Jigoral** | Character | Spend Order: Perform a +0 ranged strike. |
| **Jump** | Action | Order: Journey. |
| **Keen Eared Sentry** | Character | Vanilla (Combat stats & strikes modeled) |
| **Novice Pyro** | Character | Spend Order: Perform a +2 ranged strike. |
| **Riding Horse** | Item | Order: Spend Riding Horse: Move forward one rank. |
| **Sneak Attack** | Action | Order: Perform a +4 melee strike that may only target a spent character. |
| **Swift** | Action | Order: Journey. |
| **Tyren Ruskin** | Character | Spend React: Before target character in this rank suffers wounds, wound Tyren: Reduce the wounds the target would suffer by one. |
| **Vactus, Spirit's Champion** | Character | Limited Order: Move forward one rank. |
| **Yscarite Berserker** | Character | Vanilla (Combat stats & strikes modeled) |

---

## Category 2: Partially Implemented Cards

**Count:** 44

These cards have base triggers or primary strikes parsed into concrete `IEffect`s, but secondary clauses (stun/spend on hit, discard/spend costs, dynamic stat formulas, retaliatory strikes, or sub-actions) are silently ignored or omitted.

### Adathan, Celestial Messenger (Character)
- **Full RulesText:** `Limited Order: Target up to two of your level 1 or 2 characters: The targets gain +2 ATK and AC and an additional melee strike (base +2) while attacking this turn.`
- **Missing / Ignored Clause:** Parses +2 ATK/AC stat buff, but 'target up to two characters' and 'additional melee strike (base +2)' are ignored.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Albrecht Novice (Character)
- **Full RulesText:** `Enter React: Adjacent characters gain +2 ATK and AC this turn.`
- **Missing / Ignored Clause:** Parses +2 ATK/AC stat buff, but EnterReact trigger on deployment is not wired to army entry events.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Alrayn Gaunt (Character)
- **Full RulesText:** `React: After Alrayn Gaunt moves: Perform a melee strike.`
- **Missing / Ignored Clause:** Triggers melee strike on move, but strike reach and target validation in GameEngine do not restrict or validate the free strike context.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Arcane Approach (Action)
- **Full RulesText:** `Order: If this character is a wizard, your target character moves forward one rank. If this character is a fighter, perform a +3 melee strike.`
- **Missing / Ignored Clause:** RulesParser only parses the fighter branch (+3 melee strike); the wizard branch ('your target character moves forward one rank') is ignored.
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Assassin's Strike (Action)
- **Full RulesText:** `React: Before this character makes a strike roll against a spent character: This character gains advantage for the strike. The strike inflicts an additional wound.`
- **Missing / Ignored Clause:** Inflicts additional wound on spent target, but 'advantage for the strike' (roll 2d20 take higher) is not modeled in PerformStrikeEffect.
- **Mechanic Bucket:** `Bucket A: Die-Roll, Advantage & Save Checks`

### Burning Scroll (Item)
- **Full RulesText:** `Order: Spend Burning Scroll: Perform a +3 ranged strike.`
- **Missing / Ignored Clause:** Parses as SpendOrder +3 ranged strike, but item spend cost is not deducted/tracked if performed through certain action paths.
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Ciane (Character)
- **Full RulesText:** `React: Before Ciane makes a melee strike roll: He gains +5 ATK for the strike and the strike inflicts an additional wound. Inflict a wound on him after the strike.`
- **Missing / Ignored Clause:** Grants +5 ATK and additional wound, but 'Inflict a wound on him after the strike' is ignored.
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Cloudracer (Item)
- **Full RulesText:** `Order: Discard a card: Journey.
Order: Spend Cloudracer: Journey.`
- **Missing / Ignored Clause:** Order: Spend Cloudracer: Journey is parsed, but the second ability 'Order: Discard a card: Journey' and its discard cost are ignored.
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Crownland Strategist (Character)
- **Full RulesText:** `Spend Order: Discard the top card of an opponent's deck: Perform a ranged strike with a bonus equal to the card's level.`
- **Missing / Ignored Clause:** Parses PerformStrikeEffect, but 'Discard the top card of an opponent's deck' and 'bonus equal to the card's level' are not evaluated.
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Crush (Action)
- **Full RulesText:** `React: Before this character makes a melee strike roll: Gain +5 ATK for the strike. If the strike hits, destroy one item equipped to the target of the strike.`
- **Missing / Ignored Clause:** Grants +5 ATK, but the conditional 'If the strike hits, destroy one item equipped to the target' is partially modeled in PerformStrikeEffect without targeting choice.
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Cunning Shot (Action)
- **Full RulesText:** `Order: Perform a +4 ranged strike that does not inflict wounds. If the strike hits, spend the target and move them forward one rank.`
- **Missing / Ignored Clause:** Performs +4 non-wounding strike and spends target on hit, but 'move them forward one rank' on hit is ignored.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Doo'Run (Character)
- **Full RulesText:** `Limited React: Before Doo'run suffers wounds: Perform a melee strike.`
- **Missing / Ignored Clause:** Parses as React: Perform a melee strike, but 'Before Doo'run suffers wounds' timing interrupt is not wired into wound prevention / retaliation pipeline.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Even the Odds (Action)
- **Full RulesText:** `Order: Gain +1 ATK and AC for each opposing character within one rank this turn. If there are five or more opposing characters within one rank, gain Ward this turn.`
- **Missing / Ignored Clause:** Parses static +1 ATK/AC, but dynamic count 'for each opposing character within one rank' and 'Ward if 5+' are ignored.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Fateshift (Action)
- **Full RulesText:** `Spend React: Before target character suffers wounds, reveal the top card of your deck: Reduce the wounds by one. The target gains a bonus to AC this turn equal to the level of the card revealed this way this turn.`
- **Missing / Ignored Clause:** Reduces wounds by one, but 'reveal top card of your deck' and 'target gains bonus to AC equal to level of revealed card' are ignored.
- **Mechanic Bucket:** `Bucket A: Die-Roll, Advantage & Save Checks`

### Fireball (Action)
- **Full RulesText:** `Destroy Order: Target your character within one rank: Unstun the target. The target gains Fire Resistance this turn.`
- **Missing / Ignored Clause:** Unstuns target and destroys source, but 'target gains Fire Resistance this turn' is ignored.
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Frenzy (Character)
- **Full RulesText:** `React: After Frenzy suffers wounds: He gains +6 ATK and his melee strikes inflict an additional wound this turn.`
- **Missing / Ignored Clause:** Parses +6 ATK, but 'his melee strikes inflict an additional wound this turn' is not tracked as a continuous buff for subsequent strikes.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Gapfinder (Item)
- **Full RulesText:** `During melee strikes against spent characters, this character gains +5 ATK.`
- **Missing / Ignored Clause:** GetItemPassiveStats gives unconditional +5 ATK, ignoring the condition 'During melee strikes against spent characters'.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Graveyard Librarian (Character)
- **Full RulesText:** `Spend Order: Perform a +3 ranged strike. If the strike would kill the target, banish it instead.`
- **Missing / Ignored Clause:** Parses SpendOrder +3 ranged strike, but 'If the strike would kill the target, banish it instead' is ignored.
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Griffon of Andover (Item)
- **Full RulesText:** `Limited Order: Spend Griffon of Andover: Journey. Opposing characters may not react to or during this action.`
- **Missing / Ignored Clause:** Parses Journey, but 'Opposing characters may not react to or during this action' (split-second / no priority) is ignored.
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Heroic Confrontation (Action)
- **Full RulesText:** `Order: Perform a melee strike that inflicts an additional wound against higher level characters.`
- **Missing / Ignored Clause:** Parses strike, but conditional '+1 wound against higher level characters' is unconditionally applied or unverified against defender level.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Holy Might (Action)
- **Full RulesText:** `Order: Target a character: The target gains either +3 ATK or +3 AC this turn. Heal the target.`
- **Missing / Ignored Clause:** Parses stats and heal, but does not allow player choice of '+3 ATK OR +3 AC' (applies both or hardcodes one).
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Kraig'dem (Character)
- **Full RulesText:** `Order: Spend your adjacent character: Perform a +4 ranged strike`
- **Missing / Ignored Clause:** Parses +4 ranged strike, but cost 'Spend your adjacent character' is not validated or paid.
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Leveled! (Action)
- **Full RulesText:** `React: After this character performs a strike or action that killed an opposing character of equal or higher level: This character permanently gains +1 level, +3 ATK, +3 AC, +2 skill, and +1 HP. Banish this card.`
- **Missing / Ignored Clause:** Parses stat buffs, but kill trigger condition ('killed opposing character of equal/higher level') and 'permanently gains +1 level, +2 skill' are not fully applied to CardInstance stats.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Lord's Assailant (Character)
- **Full RulesText:** `React: After your wizard casts a spell: Journey.`
- **Missing / Ignored Clause:** Parses Journey, but react trigger condition 'After your wizard casts a spell' is not connected to spell play events.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Meet at the Inn! (Action)
- **Full RulesText:** `Order: Each player may draw a card. You may draw an additional card (two total).`
- **Missing / Ignored Clause:** Parses DrawCardEffect(1), but 'Each player may draw a card. You may draw an additional card (two total)' is not modeled for opponent.
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Memoriam (Character)
- **Full RulesText:** `Limited Order: Target your non-stunned Dwarven Forges character in a front rank: Heal and ready the target.`
- **Missing / Ignored Clause:** Parses Heal and Ready effects, but targeting constraints ('non-stunned Dwarven Forges character in a front rank') are not enforced.
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Mistress Allandra (Character)
- **Full RulesText:** `Spend Order: Perform a +4 ranged strike.
Limited React: After Mistress Allandra performs a ranged strike that killed the target: Ready her.`
- **Missing / Ignored Clause:** Parses Spend Order: +4 ranged strike, but React: 'After Mistress Allandra performs a ranged strike that killed the target: Ready her' is not parsed due to multi-line issue.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Myerdeth Mare (Item)
- **Full RulesText:** `Limited Order: Spend Myerdeth Mare: Perform a +3 melee strike that may only target lower level characters.
Order: Spend Myerdeth Mare: Move forward one rank.`
- **Missing / Ignored Clause:** Order: Move forward is parsed, but the Limited Order +3 melee strike targeting only lower level characters is partially merged or lost.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Nothrog S'sike (Item)
- **Full RulesText:** `React: After target opposing character moves to within one rank of this character, spend Nothrog S'sike: Perform a melee strike against the target.`
- **Missing / Ignored Clause:** Parses melee strike, but trigger timing 'After target opposing character moves to within one rank' and spend item cost are not enforced.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Pounce (Action)
- **Full RulesText:** `Order: Move forward one rank.
Spend React: After target character performs a melee strike that missed: Inflict a wound on the target.`
- **Missing / Ignored Clause:** Parses Order: Move forward one rank, but second ability 'Spend React: After target character performs a melee strike that missed: Inflict a wound' is ignored.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Punitive Strike (Action)
- **Full RulesText:** `React: Before this character makes a melee strike roll: The strike gains +3 ATK.
Order: Perform a melee strike that may only target a stunned character.`
- **Missing / Ignored Clause:** React gives +3 ATK to strike, but Order: 'Perform a melee strike that may only target a stunned character' is not separate.
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Quick Strength (Action)
- **Full RulesText:** `React: Before target character within two ranks makes a melee strike roll: The target gains a bonus to ATK equal to this character's level this turn.`
- **Missing / Ignored Clause:** Parses strike effect, but 'target gains a bonus to ATK equal to this character's level this turn' is not injected as dynamic level-based buff.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Scale Mail (Item)
- **Full RulesText:** `This character gains +2 AC while targeted with a ranged strike.`
- **Missing / Ignored Clause:** GetItemPassiveStats gives unconditional +2 AC, ignoring condition 'while targeted with a ranged strike'.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Shadow Bolts (Action)
- **Full RulesText:** `Spend Order: Perform a +4 ranged strike. If this character is a rogue, this strike inflicts an additional wound against spent characters. If this character is a wizard, perform another +4 ranged strike.`
- **Missing / Ignored Clause:** Parses single +4 ranged strike; class conditionals ('if rogue: +1 wound vs spent', 'if wizard: perform another strike') are ignored.
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Spiritual Weapon (Action)
- **Full RulesText:** `Order: Perform a melee strike with a bonus equal to this character's level. This character gains Reach for the strike.`
- **Missing / Ignored Clause:** Parses melee strike with level bonus, but 'This character gains Reach for the strike' is not dynamically applied to strike reach check.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Squire Fendric (Character)
- **Full RulesText:** `Limited Order: Spend a character in this rank: Move forward one rank. If Squire Fendric moves, he may perform a melee strike.`
- **Missing / Ignored Clause:** Parses Move and Strike, but cost 'Spend a character in this rank' is not validated or paid.
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Starbinder (Character)
- **Full RulesText:** `Limited React: After your target character moves: The target gains your choice of +3 ATK or AC this turn.`
- **Missing / Ignored Clause:** Parses modal +3 ATK or AC, but react trigger 'After your target character moves' is not wired to movement events.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Steady Grip (Action)
- **Full RulesText:** `Order: This character gains +2 ATK and True Strike this turn.`
- **Missing / Ignored Clause:** Parses +2 ATK, but 'True Strike' trait (cannot miss or ignore AC) is not implemented in combat engine.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Stirges (Character)
- **Full RulesText:** `Limited React: After Stirges perform a melee strike that hit: Perform a melee strike.`
- **Missing / Ignored Clause:** Parses PerformStrikeEffect, but trigger 'After Stirges perform a melee strike that hit' is not connected to strike hit event chain.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Strongarm Stout (Item)
- **Full RulesText:** `Destroy Order: Each player draws a card. This turn, this character gains +2 ATK and +1 HP for each card drawn this way.`
- **Missing / Ignored Clause:** Destroys source and modifies stats, but 'Each player draws a card' and variable bonus based on drawn cards are not evaluated.
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Sunforge Smith (Character)
- **Full RulesText:** `Spend Order: Target your wounded Dwarven Forges character: Unstun the target, or the target performs a +2 melee strike.`
- **Missing / Ignored Clause:** Parses Unstun, but modal alternative 'or the target performs a +2 melee strike' is ignored.
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Tazian Dythanus (Character)
- **Full RulesText:** `Limited Order: Target up to two characters in any discard piles: Banish the targets. Perform a +3 ranged strike for each character banished this way.`
- **Missing / Ignored Clause:** Parses +3 ranged strike, but 'Banish up to two characters in any discard piles' cost/trigger and dynamic strike count are ignored.
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### The Mighty Mugg (Character)
- **Full RulesText:** `Limited Order: Target your adjacent equal or lower level character: Move the target forward up to two ranks. The target suffers a wound, then may perform a melee strike.`
- **Missing / Ignored Clause:** Parses melee strike, but 'Move target forward up to two ranks' and 'target suffers a wound' are not sequenced before strike.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### War Horse (Item)
- **Full RulesText:** `Order: Spend War Horse: Move forward one rank.
Order: Spend War Horse: Gain +2 ATK this turn.`
- **Missing / Ignored Clause:** Merges Move forward and +2 ATK into a single ability instead of distinct spend choices.
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

---

## Category 3: Completely Unimplemented Cards

**Count:** 50

These cards have `Effects.Count == 0`, are skipped due to unrecognized trigger formats (e.g. `Roll Order:`, `Kill React:`, literal escaped newlines `\n`), or feature continuous passive aura rules that are entirely unhandled by `GameEngine` or `GetItemPassiveStats`.

### 'Mad' Pat Carrik (Character)
- **Full RulesText:** `'Mad' Pat Carrik inflicts an additional wound with melee strikes.`
- **Why Unimplemented:** Passive 'inflicts an additional wound with melee strikes' is not checked in AttackResolutionHandler.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Ac'Vuk (Character)
- **Full RulesText:** `At end of turn, if Ac'vuk did not wound an opposing character this turn, kill him.`
- **Why Unimplemented:** End-of-turn trigger 'if did not wound an opposing character this turn, kill him' is not implemented in TurnManagement/AdvancePhase.
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Ambitious Squire (Character)
- **Full RulesText:** `Ambitious Squire gains +2 ATK and AC for each adjacent higher level Deverenian Empire character.`
- **Why Unimplemented:** Conditional passive '+2 ATK and AC for each adjacent higher level Deverenian character' is not evaluated anywhere.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Another Tavern Brawl (Action)
- **Full RulesText:** `Order: Target opponent chooses a character they control: The chosen character performs a melee strike. After the strike, this character performs a melee strike.`
- **Why Unimplemented:** Effects.Count == 0 (Complex two-player strike sequence completely unparsed).
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Ardian Greathawk (Item)
- **Full RulesText:** `+3 Skill
React: After an opposing character moves, spend Ardian Greathawk: Journey.`
- **Why Unimplemented:** Literal newline prevented ability parsing; spend item react to Journey is completely unparsed.
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Balian (Character)
- **Full RulesText:** `Mercenary Guilds characters adjacent to Balian gain +2 AC.`
- **Why Unimplemented:** Continuous passive aura 'Mercenary Guilds characters adjacent to Balian gain +2 AC' is not evaluated in GameEngine.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Blitzer (Character)
- **Full RulesText:** `Blitzer's melee strikes inflict an additional wound.
Spend Order: Perform a melee strike that may target an additional rank away.`
- **Why Unimplemented:** Passive +1 wound on melee strikes and Spend Order ranged-reach melee strike are both unparsed/unhandled.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Breastplate of Power (Item)
- **Full RulesText:** `Other characters in this rank gain +2 ATK and AC.`
- **Why Unimplemented:** Aura 'Other characters in this rank gain +2 ATK and AC' is not implemented (only gives hardcoded buff to equipped bearer).
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Callow Rowan (Character)
- **Full RulesText:** `Limited React: Before a character you control dies: Put a character into play. You may reduce the character's entering rank by one.`
- **Why Unimplemented:** Effects.Count == 0 (Limited React before character dies: Put character into play from hand with reduced rank).
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Church Enforcer Tancred (Character)
- **Full RulesText:** `Limited Order: Your target character performs a melee strike that inflicts an additional wound against characters that are equal or lower level than the target.`
- **Why Unimplemented:** Effects.Count == 0 (Limited Order: target performs melee strike with +1 wound against equal/lower level characters).
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Dissolve (Action)
- **Full RulesText:** `Spend Order: Target an item within two ranks that is lower level than this character's level +5: Destroy the item.`
- **Why Unimplemented:** Effects.Count == 0 (Spend Order: Destroy target item within two ranks lower level than caster level + 5).
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Divine Respite (Action)
- **Full RulesText:** `Roll Order: Target your other character,
1-9: Unstun the target.
10-19: Ready the target.
20: Ready the target. It gains +4 ATK and AC this turn.`
- **Why Unimplemented:** Effects.Count == 0 (Roll Order: d20 table check for Unstun / Ready / +4 ATK and AC).
- **Mechanic Bucket:** `Bucket A: Die-Roll, Advantage & Save Checks`

### Dress (Character)
- **Full RulesText:** `Your Dwarven Forges characters in this rank gain +3 AC while they have one or more wounds.`
- **Why Unimplemented:** Continuous conditional aura 'Your Dwarven Forges characters in this rank gain +3 AC while wounded' is not implemented.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Dupe (Action)
- **Full RulesText:** `React: After this character is targeted by a melee strike, move to a different position in this rank: The strike is a critical failure and the character performing the strike must succeed with a dexterity save (DC 8 + this character's skill) or be unable to perform melee strikes this turn.`
- **Why Unimplemented:** Strike negation and dexterity saving throw (DC 8 + skill) to disarm attacker are completely unimplemented.
- **Mechanic Bucket:** `Bucket A: Die-Roll, Advantage & Save Checks`

### Flamelance (Action)
- **Full RulesText:** `Wounds from this action may not be reduced.
Order: Perform a +5 ranged strike.
Spend Order: Perform a +5 ranged strike that inflicts an additional wound.`
- **Why Unimplemented:** Effects.Count == 0 (Multi-line Order / Spend Order with unpreventable wounds clause unparsed).
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Frameseer (Character)
- **Full RulesText:** `Enter React: Survey.`
- **Why Unimplemented:** Effects.Count == 0 (Enter React: Survey keyword is unhandled).
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Ghed Bane (Character)
- **Full RulesText:** `Opposing lower level characters within one rank have -2 ATK and AC.
Limited Order: Move forward one rank.`
- **Why Unimplemented:** Continuous debuff aura 'Opposing lower level characters within one rank have -2 ATK and AC' and Limited Order move forward are unhandled.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Ghed Daucet (Character)
- **Full RulesText:** `Limited React: After target character within one rank attacks: If that character's ATK is lower than Daucet's level, stun the target.`
- **Why Unimplemented:** Effects.Count == 0 (React after character attacks: if ATK < Daucet level, stun target).
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Gift From Above (Action)
- **Full RulesText:** `Order: Target character gains a bonus to ATK and AC equal to this character's level this turn.`
- **Why Unimplemented:** Effects.Count == 0 (Order: Target gains ATK and AC equal to caster level).
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Goldengrove Mage (Character)
- **Full RulesText:** `Limited Order: Target character within two ranks must succeed with a will save (DC 9 + Goldengrove Mage's skill) or suffer a wound.`
- **Why Unimplemented:** Effects.Count == 0 (Limited Order: Target within two ranks must succeed with will save (DC 9 + skill) or suffer wound).
- **Mechanic Bucket:** `Bucket A: Die-Roll, Advantage & Save Checks`

### Grimfey Prowler (Character)
- **Full RulesText:** `Grimfey Prowler gains a bonus to ATK equal to his skill while performing melee strikes targeting spent characters.`
- **Why Unimplemented:** Conditional passive 'gains bonus to ATK equal to skill while performing melee strikes targeting spent characters' is ignored.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Hazahk of Shadow (Character)
- **Full RulesText:** `Hazahk of Shadow and adjacent characters gain +5 ATK while wounded.`
- **Why Unimplemented:** Continuous aura 'Hazahk and adjacent characters gain +5 ATK while wounded' is not implemented.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Hod's Legacy (Item)
- **Full RulesText:** `Unique
Spend Order: Spend Hod's Legacy: Ready target lower level non-stunned character of the same faction.
Order: Spend Hod's Legacy: Unstun target lower level character of the same faction.`
- **Why Unimplemented:** Effects.Count == 0 (Multi-line Spend Order ready / Order unstun item abilities unparsed).
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Hrun Instigator (Character)
- **Full RulesText:** `Limited Order: Target an opposing character within one rank: The target performs a melee strike. After the strike, Hrun Instigator performs a melee strike with advantage.`
- **Why Unimplemented:** Effects.Count == 0 (Limited Order: enemy strike followed by retaliatory strike with advantage).
- **Mechanic Bucket:** `Bucket A: Die-Roll, Advantage & Save Checks`

### Hunting Staff (Item)
- **Full RulesText:** `Order: Spend Hunting Staff and target an adjacent character: The target moves forward one rank.`
- **Why Unimplemented:** Effects.Count == 0 (Order: Spend Hunting Staff: Move adjacent target forward one rank).
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### I Have Your Back (Action)
- **Full RulesText:** `React: After another character in this rank is targeted with a strike: Redirect the strike to this character.`
- **Why Unimplemented:** Effects.Count == 0 (React after ally targeted with strike: Redirect strike to this character).
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Joleg (Character)
- **Full RulesText:** `Adjacent Nothrog Legions characters gain +2 ATK.`
- **Why Unimplemented:** Continuous aura 'Adjacent Nothrog Legions characters gain +2 ATK' is not implemented.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Leonora (Character)
- **Full RulesText:** `Limited Order: Leonora gains an additional melee strike (base +0) while attacking this turn.`
- **Why Unimplemented:** Effects.Count == 0 (Limited Order: Leonora gains an additional melee strike (base +0) while attacking this turn).
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Magic Missiles (Action)
- **Full RulesText:** `Spend Order: Perform a number of +0 ranged strikes equal to this wizard's Level. These strikes may not receive bonuses to the die roll.`
- **Why Unimplemented:** Effects.Count == 0 (Spend Order: Perform number of +0 ranged strikes equal to wizard Level without roll bonuses).
- **Mechanic Bucket:** `Bucket A: Die-Roll, Advantage & Save Checks`

### Makusog Steelhorn (Character)
- **Full RulesText:** `Adjacent Siege characters gain +2 to strike rolls.
Limited Order: Your target Siege moves forward one rank.`
- **Why Unimplemented:** Continuous aura '+2 to strike rolls for adjacent Siege characters' and Limited Order move target Siege forward are unparsed.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Melt Metal (Action)
- **Full RulesText:** `Order: Target a character within two ranks: For each item the target has equipped, they must succeed with a fortitude save (DC 13) or suffer a wound.`
- **Why Unimplemented:** Effects.Count == 0 (Order: Fortitude save DC 13 per equipped item or suffer wound).
- **Mechanic Bucket:** `Bucket A: Die-Roll, Advantage & Save Checks`

### Pack Tactics (Action)
- **Full RulesText:** `Spend Order: Your two target characters attack (spending as normal).`
- **Why Unimplemented:** Effects.Count == 0 (Spend Order: Your two target characters attack simultaneously).
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Prime Positioning (Action)
- **Full RulesText:** `Roll Order:
1-9: Move forward one rank.
10-19: This character gains +5 ATK this turn. Move forward one rank.
20: This character gains +5 ATK this turn. Move forward one rank. Perform a +15 melee strike.`
- **Why Unimplemented:** Effects.Count == 0 (Roll Order: d20 table check for move, ATK buffs, and +15 strike).
- **Mechanic Bucket:** `Bucket A: Die-Roll, Advantage & Save Checks`

### Quick Shielding (Action)
- **Full RulesText:** `React: After target character within two ranks is targeted with a strike: The target gains a bonus to AC equal to this character's level this turn.`
- **Why Unimplemented:** Effects.Count == 0 (React: Target within two ranks gains AC equal to caster level).
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Rapid Dash (Action)
- **Full RulesText:** `Roll Order:
1-9: Journey.
10-19: Journey. Perform an Order.
20: Move to the first rank of any formation. Gain Stealth +10 this turn.`
- **Why Unimplemented:** Effects.Count == 0 (Roll Order: d20 table check for Journey, Order, and Rank 1 move + Stealth).
- **Mechanic Bucket:** `Bucket A: Die-Roll, Advantage & Save Checks`

### Revenger (Item)
- **Full RulesText:** `This character gains Dual Wield.
This character inflicts an additional wound with melee strikes while they have two or more Weapons equipped.`
- **Why Unimplemented:** Passive traits 'Gains Dual Wield' and '+1 wound with melee strikes while two weapons equipped' are unhandled.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Salvage (Character)
- **Full RulesText:** `Limited React: Before target Dwarven Forges character is killed: Return an Armor, Shield, or Weapon equipped to the target to its owner's hand.`
- **Why Unimplemented:** Effects.Count == 0 (Limited React before Dwarven character killed: Return equipped item to hand).
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Shambling Horror (Character)
- **Full RulesText:** `Characters killed by Shambling Horror are banished instead.`
- **Why Unimplemented:** Passive 'Characters killed by Shambling Horror are banished instead' is not hooked into DeathCheckHandler.
- **Mechanic Bucket:** `Bucket E: Modal / Conditional Branching Actions`

### Shannon the Daring (Character)
- **Full RulesText:** `Adjacent level 1 and 2 characters gain +2 ATK.
Limited React: After Shannon enters play or performs a melee strike: Level 1 and 2 Free Kingdoms characters in your first rank gain +2 ATK and AC this turn.`
- **Why Unimplemented:** Passive aura '+2 ATK to adjacent level 1 and 2 characters' and Limited React on enter/strike are unparsed.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Shield of Spurning (Item)
- **Full RulesText:** `React: After target character performs a melee strike targeting this character, spend Shield of Spurning: Move the target backward one rank.`
- **Why Unimplemented:** Effects.Count == 0 (React: Spend Shield of Spurning to push attacker backward one rank).
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Shillelagh (Item)
- **Full RulesText:** `This character gains a bonus to ATK equal to half their skill, rounded up.`
- **Why Unimplemented:** Continuous passive 'gains bonus to ATK equal to half skill, rounded up' is not evaluated in GetItemPassiveStats.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Sister Kiren (Character)
- **Full RulesText:** `Limited Order: Your target higher level Deverenian Empire character Journeys.`
- **Why Unimplemented:** Effects.Count == 0 (Limited Order: Target higher level Deverenian Empire character Journeys).
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Squire Argos (Character)
- **Full RulesText:** `Other equal or lower level Free Kingdoms characters in this rank gain +2 ATK and +1 AC.`
- **Why Unimplemented:** Continuous aura 'Other equal or lower level Free Kingdoms characters in this rank gain +2 ATK and +1 AC' is not implemented.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Taumar the Protector (Character)
- **Full RulesText:** `Each Deverenian Empire fighter in the rank in front of Taumar the Protector gains +2 AC and skill.`
- **Why Unimplemented:** Continuous aura 'Deverenian Empire fighters in rank in front gain +2 AC and skill' is not implemented.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### TFK Brigand (Character)
- **Full RulesText:** `Strikes targeting TFK Brigand inflict an additional wound.
Spend Order: Stun TFK Brigand: Perform a +0 ranged strike.`
- **Why Unimplemented:** Passive vulnerability (+1 wound from strikes) and Spend Order stun self to strike are unparsed.
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Tinker (Character)
- **Full RulesText:** `Tinker may equip items regardless of class restrictions and gains +1 level for equipping them.`
- **Why Unimplemented:** Passive rules (equips items ignoring class restrictions and gains +1 level per item) are not implemented.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Tunnel Vanguard (Character)
- **Full RulesText:** `Adjacent Dwarven Forges characters gain +1 HP.`
- **Why Unimplemented:** Continuous aura 'Adjacent Dwarven Forges characters gain +1 HP' is not implemented.
- **Mechanic Bucket:** `Bucket C: Character Aura & Conditional Stat Passives`

### Tunnel Worm (Character)
- **Full RulesText:** `Kill React: Move your target character in this rank to a different position in this rank.`
- **Why Unimplemented:** Effects.Count == 0 (Kill React trigger to reposition target character in same rank is unhandled).
- **Mechanic Bucket:** `Bucket D: Rank Movement / Position Swap & Action Triggers`

### Unicorn of Thun (Item)
- **Full RulesText:** `+2 Skill
Order: Spend Unicorn of Thun: Journey.
Order: Spend Unicorn of Thun and banish two action cards from your discard pile: Heal this character.`
- **Why Unimplemented:** Multi-line spend item abilities (Journey / banish two actions from discard to heal) are unparsed.
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

### Well Supplied (Action)
- **Full RulesText:** `Spend Order: Reveal the top 10 cards of your deck. Each of your characters may equip one revealed item that they may legally equip. Then shuffle.`
- **Why Unimplemented:** Effects.Count == 0 (Spend Order: Reveal top 10 cards and equip revealed items).
- **Mechanic Bucket:** `Bucket B: Sacrifice, Item Spend & Discard Costs`

---

## Grouped Mechanic Buckets (The Action Plan)

To efficiently implement all 94 missing and partial cards across starter decks without ad-hoc single-card patching, the remaining gaps are grouped into 5 concrete mechanic buckets:

### Bucket A: Die-Roll, Advantage & Save Checks (10 Cards)

| Card Name | Type | Category | Core Missing Mechanic |
| :--- | :--- | :--- | :--- |
| **Assassin's Strike** | Action | Cat 2 | Inflicts additional wound on spent target, but 'advantage for the strike' (roll 2d20 take higher) is not modeled in PerformStrikeEffect. |
| **Divine Respite** | Action | Cat 3 | Effects.Count == 0 (Roll Order: d20 table check for Unstun / Ready / +4 ATK and AC). |
| **Dupe** | Action | Cat 3 | Strike negation and dexterity saving throw (DC 8 + skill) to disarm attacker are completely unimplemented. |
| **Fateshift** | Action | Cat 2 | Reduces wounds by one, but 'reveal top card of your deck' and 'target gains bonus to AC equal to level of revealed card' are ignored. |
| **Goldengrove Mage** | Character | Cat 3 | Effects.Count == 0 (Limited Order: Target within two ranks must succeed with will save (DC 9 + skill) or suffer wound). |
| **Hrun Instigator** | Character | Cat 3 | Effects.Count == 0 (Limited Order: enemy strike followed by retaliatory strike with advantage). |
| **Magic Missiles** | Action | Cat 3 | Effects.Count == 0 (Spend Order: Perform number of +0 ranged strikes equal to wizard Level without roll bonuses). |
| **Melt Metal** | Action | Cat 3 | Effects.Count == 0 (Order: Fortitude save DC 13 per equipped item or suffer wound). |
| **Prime Positioning** | Action | Cat 3 | Effects.Count == 0 (Roll Order: d20 table check for move, ATK buffs, and +15 strike). |
| **Rapid Dash** | Action | Cat 3 | Effects.Count == 0 (Roll Order: d20 table check for Journey, Order, and Rank 1 move + Stealth). |

### Bucket B: Sacrifice, Item Spend & Discard Costs (17 Cards)

| Card Name | Type | Category | Core Missing Mechanic |
| :--- | :--- | :--- | :--- |
| **Ardian Greathawk** | Item | Cat 3 | Literal newline prevented ability parsing; spend item react to Journey is completely unparsed. |
| **Burning Scroll** | Item | Cat 2 | Parses as SpendOrder +3 ranged strike, but item spend cost is not deducted/tracked if performed through certain action paths. |
| **Ciane** | Character | Cat 2 | Grants +5 ATK and additional wound, but 'Inflict a wound on him after the strike' is ignored. |
| **Cloudracer** | Item | Cat 2 | Order: Spend Cloudracer: Journey is parsed, but the second ability 'Order: Discard a card: Journey' and its discard cost are ignored. |
| **Crownland Strategist** | Character | Cat 2 | Parses PerformStrikeEffect, but 'Discard the top card of an opponent's deck' and 'bonus equal to the card's level' are not evaluated. |
| **Crush** | Action | Cat 2 | Grants +5 ATK, but the conditional 'If the strike hits, destroy one item equipped to the target' is partially modeled in PerformStrikeEffect without targeting choice. |
| **Dissolve** | Action | Cat 3 | Effects.Count == 0 (Spend Order: Destroy target item within two ranks lower level than caster level + 5). |
| **Hod's Legacy** | Item | Cat 3 | Effects.Count == 0 (Multi-line Spend Order ready / Order unstun item abilities unparsed). |
| **Hunting Staff** | Item | Cat 3 | Effects.Count == 0 (Order: Spend Hunting Staff: Move adjacent target forward one rank). |
| **Kraig'dem** | Character | Cat 2 | Parses +4 ranged strike, but cost 'Spend your adjacent character' is not validated or paid. |
| **Salvage** | Character | Cat 3 | Effects.Count == 0 (Limited React before Dwarven character killed: Return equipped item to hand). |
| **Squire Fendric** | Character | Cat 2 | Parses Move and Strike, but cost 'Spend a character in this rank' is not validated or paid. |
| **Strongarm Stout** | Item | Cat 2 | Destroys source and modifies stats, but 'Each player draws a card' and variable bonus based on drawn cards are not evaluated. |
| **Tazian Dythanus** | Character | Cat 2 | Parses +3 ranged strike, but 'Banish up to two characters in any discard piles' cost/trigger and dynamic strike count are ignored. |
| **TFK Brigand** | Character | Cat 3 | Passive vulnerability (+1 wound from strikes) and Spend Order stun self to strike are unparsed. |
| **Unicorn of Thun** | Item | Cat 3 | Multi-line spend item abilities (Journey / banish two actions from discard to heal) are unparsed. |
| **Well Supplied** | Action | Cat 3 | Effects.Count == 0 (Spend Order: Reveal top 10 cards and equip revealed items). |

### Bucket C: Character Aura & Conditional Stat Passives (31 Cards)

| Card Name | Type | Category | Core Missing Mechanic |
| :--- | :--- | :--- | :--- |
| **'Mad' Pat Carrik** | Character | Cat 3 | Passive 'inflicts an additional wound with melee strikes' is not checked in AttackResolutionHandler. |
| **Adathan, Celestial Messenger** | Character | Cat 2 | Parses +2 ATK/AC stat buff, but 'target up to two characters' and 'additional melee strike (base +2)' are ignored. |
| **Ambitious Squire** | Character | Cat 3 | Conditional passive '+2 ATK and AC for each adjacent higher level Deverenian character' is not evaluated anywhere. |
| **Balian** | Character | Cat 3 | Continuous passive aura 'Mercenary Guilds characters adjacent to Balian gain +2 AC' is not evaluated in GameEngine. |
| **Blitzer** | Character | Cat 3 | Passive +1 wound on melee strikes and Spend Order ranged-reach melee strike are both unparsed/unhandled. |
| **Breastplate of Power** | Item | Cat 3 | Aura 'Other characters in this rank gain +2 ATK and AC' is not implemented (only gives hardcoded buff to equipped bearer). |
| **Dress** | Character | Cat 3 | Continuous conditional aura 'Your Dwarven Forges characters in this rank gain +3 AC while wounded' is not implemented. |
| **Even the Odds** | Action | Cat 2 | Parses static +1 ATK/AC, but dynamic count 'for each opposing character within one rank' and 'Ward if 5+' are ignored. |
| **Frenzy** | Character | Cat 2 | Parses +6 ATK, but 'his melee strikes inflict an additional wound this turn' is not tracked as a continuous buff for subsequent strikes. |
| **Gapfinder** | Item | Cat 2 | GetItemPassiveStats gives unconditional +5 ATK, ignoring the condition 'During melee strikes against spent characters'. |
| **Ghed Bane** | Character | Cat 3 | Continuous debuff aura 'Opposing lower level characters within one rank have -2 ATK and AC' and Limited Order move forward are unhandled. |
| **Gift From Above** | Action | Cat 3 | Effects.Count == 0 (Order: Target gains ATK and AC equal to caster level). |
| **Grimfey Prowler** | Character | Cat 3 | Conditional passive 'gains bonus to ATK equal to skill while performing melee strikes targeting spent characters' is ignored. |
| **Hazahk of Shadow** | Character | Cat 3 | Continuous aura 'Hazahk and adjacent characters gain +5 ATK while wounded' is not implemented. |
| **Heroic Confrontation** | Action | Cat 2 | Parses strike, but conditional '+1 wound against higher level characters' is unconditionally applied or unverified against defender level. |
| **Joleg** | Character | Cat 3 | Continuous aura 'Adjacent Nothrog Legions characters gain +2 ATK' is not implemented. |
| **Leonora** | Character | Cat 3 | Effects.Count == 0 (Limited Order: Leonora gains an additional melee strike (base +0) while attacking this turn). |
| **Leveled!** | Action | Cat 2 | Parses stat buffs, but kill trigger condition ('killed opposing character of equal/higher level') and 'permanently gains +1 level, +2 skill' are not fully applied to CardInstance stats. |
| **Makusog Steelhorn** | Character | Cat 3 | Continuous aura '+2 to strike rolls for adjacent Siege characters' and Limited Order move target Siege forward are unparsed. |
| **Quick Shielding** | Action | Cat 3 | Effects.Count == 0 (React: Target within two ranks gains AC equal to caster level). |
| **Quick Strength** | Action | Cat 2 | Parses strike effect, but 'target gains a bonus to ATK equal to this character's level this turn' is not injected as dynamic level-based buff. |
| **Revenger** | Item | Cat 3 | Passive traits 'Gains Dual Wield' and '+1 wound with melee strikes while two weapons equipped' are unhandled. |
| **Scale Mail** | Item | Cat 2 | GetItemPassiveStats gives unconditional +2 AC, ignoring condition 'while targeted with a ranged strike'. |
| **Shannon the Daring** | Character | Cat 3 | Passive aura '+2 ATK to adjacent level 1 and 2 characters' and Limited React on enter/strike are unparsed. |
| **Shillelagh** | Item | Cat 3 | Continuous passive 'gains bonus to ATK equal to half skill, rounded up' is not evaluated in GetItemPassiveStats. |
| **Spiritual Weapon** | Action | Cat 2 | Parses melee strike with level bonus, but 'This character gains Reach for the strike' is not dynamically applied to strike reach check. |
| **Squire Argos** | Character | Cat 3 | Continuous aura 'Other equal or lower level Free Kingdoms characters in this rank gain +2 ATK and +1 AC' is not implemented. |
| **Steady Grip** | Action | Cat 2 | Parses +2 ATK, but 'True Strike' trait (cannot miss or ignore AC) is not implemented in combat engine. |
| **Taumar the Protector** | Character | Cat 3 | Continuous aura 'Deverenian Empire fighters in rank in front gain +2 AC and skill' is not implemented. |
| **Tinker** | Character | Cat 3 | Passive rules (equips items ignoring class restrictions and gains +1 level per item) are not implemented. |
| **Tunnel Vanguard** | Character | Cat 3 | Continuous aura 'Adjacent Dwarven Forges characters gain +1 HP' is not implemented. |

### Bucket D: Rank Movement / Position Swap & Action Triggers (20 Cards)

| Card Name | Type | Category | Core Missing Mechanic |
| :--- | :--- | :--- | :--- |
| **Ac'Vuk** | Character | Cat 3 | End-of-turn trigger 'if did not wound an opposing character this turn, kill him' is not implemented in TurnManagement/AdvancePhase. |
| **Albrecht Novice** | Character | Cat 2 | Parses +2 ATK/AC stat buff, but EnterReact trigger on deployment is not wired to army entry events. |
| **Alrayn Gaunt** | Character | Cat 2 | Triggers melee strike on move, but strike reach and target validation in GameEngine do not restrict or validate the free strike context. |
| **Callow Rowan** | Character | Cat 3 | Effects.Count == 0 (Limited React before character dies: Put character into play from hand with reduced rank). |
| **Cunning Shot** | Action | Cat 2 | Performs +4 non-wounding strike and spends target on hit, but 'move them forward one rank' on hit is ignored. |
| **Doo'Run** | Character | Cat 2 | Parses as React: Perform a melee strike, but 'Before Doo'run suffers wounds' timing interrupt is not wired into wound prevention / retaliation pipeline. |
| **Frameseer** | Character | Cat 3 | Effects.Count == 0 (Enter React: Survey keyword is unhandled). |
| **Ghed Daucet** | Character | Cat 3 | Effects.Count == 0 (React after character attacks: if ATK < Daucet level, stun target). |
| **I Have Your Back** | Action | Cat 3 | Effects.Count == 0 (React after ally targeted with strike: Redirect strike to this character). |
| **Lord's Assailant** | Character | Cat 2 | Parses Journey, but react trigger condition 'After your wizard casts a spell' is not connected to spell play events. |
| **Mistress Allandra** | Character | Cat 2 | Parses Spend Order: +4 ranged strike, but React: 'After Mistress Allandra performs a ranged strike that killed the target: Ready her' is not parsed due to multi-line issue. |
| **Myerdeth Mare** | Item | Cat 2 | Order: Move forward is parsed, but the Limited Order +3 melee strike targeting only lower level characters is partially merged or lost. |
| **Nothrog S'sike** | Item | Cat 2 | Parses melee strike, but trigger timing 'After target opposing character moves to within one rank' and spend item cost are not enforced. |
| **Pounce** | Action | Cat 2 | Parses Order: Move forward one rank, but second ability 'Spend React: After target character performs a melee strike that missed: Inflict a wound' is ignored. |
| **Shield of Spurning** | Item | Cat 3 | Effects.Count == 0 (React: Spend Shield of Spurning to push attacker backward one rank). |
| **Sister Kiren** | Character | Cat 3 | Effects.Count == 0 (Limited Order: Target higher level Deverenian Empire character Journeys). |
| **Starbinder** | Character | Cat 2 | Parses modal +3 ATK or AC, but react trigger 'After your target character moves' is not wired to movement events. |
| **Stirges** | Character | Cat 2 | Parses PerformStrikeEffect, but trigger 'After Stirges perform a melee strike that hit' is not connected to strike hit event chain. |
| **The Mighty Mugg** | Character | Cat 2 | Parses melee strike, but 'Move target forward up to two ranks' and 'target suffers a wound' are not sequenced before strike. |
| **Tunnel Worm** | Character | Cat 3 | Effects.Count == 0 (Kill React trigger to reposition target character in same rank is unhandled). |

### Bucket E: Modal / Conditional Branching Actions (16 Cards)

| Card Name | Type | Category | Core Missing Mechanic |
| :--- | :--- | :--- | :--- |
| **Another Tavern Brawl** | Action | Cat 3 | Effects.Count == 0 (Complex two-player strike sequence completely unparsed). |
| **Arcane Approach** | Action | Cat 2 | RulesParser only parses the fighter branch (+3 melee strike); the wizard branch ('your target character moves forward one rank') is ignored. |
| **Church Enforcer Tancred** | Character | Cat 3 | Effects.Count == 0 (Limited Order: target performs melee strike with +1 wound against equal/lower level characters). |
| **Fireball** | Action | Cat 2 | Unstuns target and destroys source, but 'target gains Fire Resistance this turn' is ignored. |
| **Flamelance** | Action | Cat 3 | Effects.Count == 0 (Multi-line Order / Spend Order with unpreventable wounds clause unparsed). |
| **Graveyard Librarian** | Character | Cat 2 | Parses SpendOrder +3 ranged strike, but 'If the strike would kill the target, banish it instead' is ignored. |
| **Griffon of Andover** | Item | Cat 2 | Parses Journey, but 'Opposing characters may not react to or during this action' (split-second / no priority) is ignored. |
| **Holy Might** | Action | Cat 2 | Parses stats and heal, but does not allow player choice of '+3 ATK OR +3 AC' (applies both or hardcodes one). |
| **Meet at the Inn!** | Action | Cat 2 | Parses DrawCardEffect(1), but 'Each player may draw a card. You may draw an additional card (two total)' is not modeled for opponent. |
| **Memoriam** | Character | Cat 2 | Parses Heal and Ready effects, but targeting constraints ('non-stunned Dwarven Forges character in a front rank') are not enforced. |
| **Pack Tactics** | Action | Cat 3 | Effects.Count == 0 (Spend Order: Your two target characters attack simultaneously). |
| **Punitive Strike** | Action | Cat 2 | React gives +3 ATK to strike, but Order: 'Perform a melee strike that may only target a stunned character' is not separate. |
| **Shadow Bolts** | Action | Cat 2 | Parses single +4 ranged strike; class conditionals ('if rogue: +1 wound vs spent', 'if wizard: perform another strike') are ignored. |
| **Shambling Horror** | Character | Cat 3 | Passive 'Characters killed by Shambling Horror are banished instead' is not hooked into DeathCheckHandler. |
| **Sunforge Smith** | Character | Cat 2 | Parses Unstun, but modal alternative 'or the target performs a +2 melee strike' is ignored. |
| **War Horse** | Item | Cat 2 | Merges Move forward and +2 ATK into a single ability instead of distinct spend choices. |

