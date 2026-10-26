using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public class Batch2SpecRemediationTests
{
	private static CardDatabase CreateTestCardDb()
	{
		return new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase)
			{
				["fighter"] = new CardDefinition
				{
					DefinitionId = "fighter",
					Name = "Fierce Fighter",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 14,
					BaseHitPoints = 10,
					Skill = 4,
					Attack = new AttackValue
					{
						Strikes = new List<StrikeDescriptor> { new StrikeDescriptor { Modifier = 2, IsRanged = false } },
						RawText = "+2"
					}
				},
				["warlord-hero"] = new CardDefinition
				{
					DefinitionId = "warlord-hero",
					Name = "Grand Warlord",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 3,
					ArmorClass = 16,
					BaseHitPoints = 15,
					IsWarlord = true,
					Skill = 6,
					Traits = new List<string> { "+2 Initiative" }
				},
				["unique-hero"] = new CardDefinition
				{
					DefinitionId = "unique-hero-1",
					Name = "Lord Valen",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 15,
					BaseHitPoints = 10,
					Traits = new List<string> { "Unique" }
				},
				["unique-hero-alt"] = new CardDefinition
				{
					DefinitionId = "unique-hero-2",
					Name = "Lord Valen",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 15,
					BaseHitPoints = 10,
					Traits = new List<string> { "Unique" }
				},
				["unique-sword"] = new CardDefinition
				{
					DefinitionId = "unique-sword",
					Name = "Sunblade",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Level = 1,
					Traits = new List<string> { "Unique", "Weapon" }
				},
				["regular-sword"] = new CardDefinition
				{
					DefinitionId = "regular-sword",
					Name = "Iron Sword",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Level = 1,
					Traits = new List<string> { "Weapon" }
				},
				["epic-spell"] = new CardDefinition
				{
					DefinitionId = "epic-spell",
					Name = "Wrath of the Gods",
					Type = CardType.Action,
					Class = CharacterClass.Cleric,
					Level = 1,
					Traits = new List<string> { "Epic" }
				},
				["regular-action"] = new CardDefinition
				{
					DefinitionId = "regular-action",
					Name = "Charge",
					Type = CardType.Action,
					Class = CharacterClass.Fighter,
					Level = 1
				}
			}
		};
	}

	#region Fix 1: AttachedItems vs DiscardPile

	[Fact]
	public void Fix1_EquippedItem_IsStoredInAttachedItems_NotDiscardPile()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState state = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			Players = new List<PlayerState>
			{
				new PlayerState
				{
					PlayerIndex = 0,
					Hand = new List<CardInstance>
					{
						new CardInstance
						{
							InstanceId = 10,
							DefinitionId = "regular-sword",
							Zone = CardZone.Hand,
							OwnerPlayerIndex = 0
						}
					},
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 1,
								DefinitionId = "fighter",
								Zone = CardZone.Board,
								Rank = 1,
								OwnerPlayerIndex = 0,
								CurrentHitPoints = 10,
								CurrentArmorClass = 14
							}
						}
					}
				},
				new PlayerState { PlayerIndex = 1 }
			}
		};

		GameEngine engine = new GameEngine(state, cardDb);
		PlayCardAction playAction = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			TargetCharacterId = 1
		};

		ActionResult result = engine.Execute(playAction);
		Assert.True(result.IsSuccess, string.Join("; ", result.Errors));

		PlayerState p0 = state.Players[0];
		Assert.Empty(p0.DiscardPile);
		Assert.Single(p0.AttachedItems);
		Assert.Equal(10, p0.AttachedItems[0].InstanceId);
		Assert.Equal(CardZone.Attached, p0.AttachedItems[0].Zone);
		Assert.Equal(1, p0.AttachedItems[0].AttachedToInstanceId);

		// Verify CardLocator and GameEngine lookup
		CardInstance? foundLocator = Events.CardLocator.FindCard(state, 10);
		Assert.NotNull(foundLocator);
		Assert.Equal(10, foundLocator.Value.InstanceId);
		Assert.Equal(CardZone.Attached, foundLocator.Value.Zone);

		CardInstance? foundEngine = GameEngine.FindAttachedItem(state, 10);
		Assert.NotNull(foundEngine);
		Assert.Equal(10, foundEngine.Value.InstanceId);
	}

	[Fact]
	public void Fix1_DefeatedCharacter_MovesAttachedItem_ToOwnerDiscard_FromAttachedItems()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState state = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Combat,
			Players = new List<PlayerState>
			{
				new PlayerState
				{
					PlayerIndex = 0,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 1,
								DefinitionId = "fighter",
								Zone = CardZone.Board,
								Rank = 1,
								OwnerPlayerIndex = 0,
								CurrentHitPoints = 10,
								CurrentArmorClass = 14,
								ReadyState = ReadyState.Ready
							}
						}
					}
				},
				new PlayerState
				{
					PlayerIndex = 1,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 2,
								DefinitionId = "fighter",
								Zone = CardZone.Board,
								Rank = 1,
								OwnerPlayerIndex = 1,
								CurrentHitPoints = 1,
								CurrentArmorClass = 10,
								AttachedItemIds = new List<int> { 20 }
							}
						}
					},
					AttachedItems = new List<CardInstance>
					{
						new CardInstance
						{
							InstanceId = 20,
							DefinitionId = "regular-sword",
							Zone = CardZone.Attached,
							AttachedToInstanceId = 2,
							OwnerPlayerIndex = 1
						}
					}
				}
			}
		};

		GameEngine engine = new GameEngine(state, cardDb);
		engine.SetDiceRoller((int _, int _) => 15);

		AttackAction attack = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};

		ActionResult result = engine.Execute(attack);
		Assert.True(result.IsSuccess, string.Join("; ", result.Errors));

		PlayerState p1 = state.Players[1];
		Assert.Empty(p1.AttachedItems);
		Assert.Contains(p1.DiscardPile, c => c.InstanceId == 20 && c.Zone == CardZone.Discard && c.AttachedToInstanceId == null);
		Assert.Contains(p1.DiscardPile, c => c.InstanceId == 2 && c.Zone == CardZone.Discard);
	}

	[Fact]
	public void Fix1_PlayerState_DeepClone_Clones_AttachedItems()
	{
		PlayerState original = new PlayerState
		{
			PlayerIndex = 0,
			AttachedItems = new List<CardInstance>
			{
				new CardInstance
				{
					InstanceId = 99,
					DefinitionId = "regular-sword",
					Zone = CardZone.Attached,
					AttachedToInstanceId = 5
				}
			}
		};

		PlayerState clone = original.DeepClone();
		Assert.Single(clone.AttachedItems);
		Assert.Equal(99, clone.AttachedItems[0].InstanceId);

		clone.AttachedItems.Clear();
		Assert.Single(original.AttachedItems);
	}

	#endregion

	#region Fix 2: Empty Intermediate Ranks Falling Forward

	[Fact]
	public void Fix2_EmptyRank1_TriggersFallingForward_WithOrientationPenalty()
	{
		PlayerState player = new PlayerState
		{
			PlayerIndex = 0,
			Ranks = new SortedDictionary<int, List<CardInstance>>
			{
				[1] = new List<CardInstance>(), // empty rank 1
				[2] = new List<CardInstance>
				{
					new CardInstance
					{
						InstanceId = 1,
						DefinitionId = "fighter",
						Rank = 2,
						ReadyState = ReadyState.Ready
					},
					new CardInstance
					{
						InstanceId = 2,
						DefinitionId = "fighter",
						Rank = 2,
						ReadyState = ReadyState.Ready
					}
				}
			}
		};

		BoardGeometry.ResolveFallingForward(player);

		Assert.True(BoardGeometry.IsFormationLegal(player));
		Assert.True(player.Ranks.ContainsKey(1));
		Assert.Single(player.Ranks[1]);
		Assert.Equal(1, player.Ranks[1][0].InstanceId);
		Assert.Equal(1, player.Ranks[1][0].Rank);
		Assert.Equal(ReadyState.Spent, player.Ranks[1][0].ReadyState);

		Assert.True(player.Ranks.ContainsKey(2));
		Assert.Single(player.Ranks[2]);
		Assert.Equal(2, player.Ranks[2][0].InstanceId);
		Assert.Equal(ReadyState.Ready, player.Ranks[2][0].ReadyState);
	}

	[Fact]
	public void Fix2_SpentCharacter_FallingForward_BecomesStunned()
	{
		PlayerState player = new PlayerState
		{
			PlayerIndex = 0,
			Ranks = new SortedDictionary<int, List<CardInstance>>
			{
				[2] = new List<CardInstance>
				{
					new CardInstance
					{
						InstanceId = 1,
						DefinitionId = "fighter",
						Rank = 2,
						ReadyState = ReadyState.Spent
					}
				}
			}
		};

		BoardGeometry.ResolveFallingForward(player);

		Assert.True(player.Ranks.ContainsKey(1));
		Assert.Single(player.Ranks[1]);
		Assert.Equal(1, player.Ranks[1][0].Rank);
		Assert.Equal(ReadyState.Stunned, player.Ranks[1][0].ReadyState);
		Assert.False(player.Ranks.ContainsKey(2)); // trailing rank 2 removed
	}

	[Fact]
	public void Fix2_EmptyIntermediateRank_FallsForward_ThroughAllEmptyRanks()
	{
		PlayerState player = new PlayerState
		{
			PlayerIndex = 0,
			Ranks = new SortedDictionary<int, List<CardInstance>>
			{
				[1] = new List<CardInstance>(),
				[2] = new List<CardInstance>(),
				[3] = new List<CardInstance>
				{
					new CardInstance
					{
						InstanceId = 10,
						DefinitionId = "fighter",
						Rank = 3,
						ReadyState = ReadyState.Ready
					}
				}
			}
		};

		BoardGeometry.ResolveFallingForward(player);

		Assert.True(player.Ranks.ContainsKey(1));
		Assert.Single(player.Ranks[1]);
		Assert.Equal(1, player.Ranks[1][0].Rank);
		Assert.Equal(ReadyState.Stunned, player.Ranks[1][0].ReadyState); // Ready -> Spent -> Stunned
		Assert.False(player.Ranks.ContainsKey(2));
		Assert.False(player.Ranks.ContainsKey(3));
	}

	#endregion

	#region Fix 3: Initiative Winner Persistence, Nat 20 Rule & React Priority

	[Fact]
	public void Fix3_Natural20_AutomaticallyBeats_HigherModifiedRoll()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState state = new GameState
		{
			TurnNumber = 1,
			Players = new List<PlayerState>
			{
				new PlayerState
				{
					PlayerIndex = 0,
					Ranks = new SortedDictionary<int, List<CardInstance>>()
				},
				new PlayerState
				{
					PlayerIndex = 1,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 10,
								DefinitionId = "warlord-hero", // +2 Initiative trait
								Rank = 1
							}
						}
					}
				}
			}
		};

		// TurnBuff adding +5 more initiative to player 1
		GameEngine.AddTurnBuff(state, 10, "INITIATIVE", 5);

		// Player 0 rolls natural 20 (modifier 0, total 20).
		// Player 1 rolls 19 (modifier +7, total 26).
		// Per Rule 504: Nat 20 automatically beats non-Nat 20 regardless of modifiers.
		int callCount = 0;
		GameEngine engine = new GameEngine(state, cardDb, (int _, int _) =>
		{
			callCount++;
			return (callCount % 2 == 1) ? 20 : 19;
		});

		var initResult = engine.RollInitiative();
		Assert.Equal(0, initResult.WinnerIndex);
		Assert.Equal(20, initResult.Roll0);
		Assert.Equal(19, initResult.Roll1);
		Assert.Equal(0, state.InitiativeWinnerIndex);
	}

	[Fact]
	public void Fix3_InitiativeModifier_AppliesWhenNeitherIsNat20()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState state = new GameState
		{
			TurnNumber = 1,
			Players = new List<PlayerState>
			{
				new PlayerState
				{
					PlayerIndex = 0,
					Ranks = new SortedDictionary<int, List<CardInstance>>()
				},
				new PlayerState
				{
					PlayerIndex = 1,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 10,
								DefinitionId = "warlord-hero", // +2 Initiative trait
								Rank = 1
							}
						}
					}
				}
			}
		};

		// Player 0 rolls 15 (+0 = 15).
		// Player 1 rolls 14 (+2 = 16).
		int callCount = 0;
		GameEngine engine = new GameEngine(state, cardDb, (int _, int _) =>
		{
			callCount++;
			return (callCount % 2 == 1) ? 15 : 14;
		});

		var initResult = engine.RollInitiative();
		Assert.Equal(1, initResult.WinnerIndex);
		Assert.Equal(1, state.InitiativeWinnerIndex);
	}

	[Fact]
	public void Fix3_ReactPriority_StartsWithInitiativeWinner()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState state = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			InitiativeWinnerIndex = 1, // Player 1 won initiative
			CurrentPhase = TurnPhase.Main,
			AutoPassWhenNoReactsAvailable = false,
			Players = new List<PlayerState>
			{
				new PlayerState
				{
					PlayerIndex = 0,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[2] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 1,
								DefinitionId = "fighter",
								Rank = 2,
								ReadyState = ReadyState.Ready
							}
						}
					}
				},
				new PlayerState
				{
					PlayerIndex = 1,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 2,
								DefinitionId = "fighter",
								Rank = 1,
								ReadyState = ReadyState.Ready
							}
						}
					}
				}
			}
		};

		GameEngine engine = new GameEngine(state, cardDb);
		MarchAction march = new MarchAction
		{
			PlayerIndex = 0,
			CardInstanceId = 1,
			SourceRank = 2,
			DestinationRank = 1
		};

		ActionResult result = engine.Execute(march);
		Assert.True(result.IsSuccess, string.Join("; ", result.Errors));

		// React window should open with priority to InitiativeWinnerIndex (Player 1)
		Assert.True(state.Priority.IsReactWindowOpen);
		Assert.Equal(1, state.Priority.ActivePlayerIndex);
	}

	#endregion

	#region Fix 4: Death Check Actual Level <= 0 & Banish Suppresses Death Reacts

	[Fact]
	public void Fix4_DeathCheck_EffectiveLevelZeroOrLess_DiesImmediately()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState state = new GameState
		{
			TurnNumber = 1,
			Players = new List<PlayerState>
			{
				new PlayerState
				{
					PlayerIndex = 0,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 1,
								DefinitionId = "fighter", // Level 1
								BonusLevel = -1, // Effective level 0!
								CurrentHitPoints = 10,
								CurrentArmorClass = 14,
								Rank = 1
							}
						}
					}
				},
				new PlayerState { PlayerIndex = 1 }
			}
		};

		DeathCheckHandler handler = new DeathCheckHandler(cardDb);
		// Simulate a deal damage event or character defeated event triggering death check
		var events = handler.AfterResolve(new DealDamageEvent
		{
			TargetInstanceId = 1,
			Amount = 0,
			DamageType = "physical"
		}, state).ToList();

		Assert.Single(events);
		CharacterDefeatedEvent defeated = Assert.IsType<CharacterDefeatedEvent>(events[0]);
		Assert.Equal(1, defeated.DefeatedInstanceId);
	}

	[Fact]
	public void Fix4_BanishOnDeath_Suppresses_DeathAndKillReacts()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState state = new GameState
		{
			TurnNumber = 1,
			Players = new List<PlayerState>
			{
				new PlayerState
				{
					PlayerIndex = 0,
					Ranks = new SortedDictionary<int, List<CardInstance>>()
				}
			}
		};

		GameEngine engine = new GameEngine(state, cardDb);

		CharacterDefeatedEvent banishedDeath = new CharacterDefeatedEvent
		{
			DefeatedInstanceId = 1,
			OwnerPlayerIndex = 0,
			BanishInsteadOfDiscard = true
		};

		CardInstance reactingCard = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "fighter",
			OwnerPlayerIndex = 0
		};

		AbilityDefinition beforeDiesAbility = new AbilityDefinition
		{
			Trigger = AbilityTrigger.React,
			Effects = Array.Empty<IEffect>(),
			Timing = ReactTiming.BeforeDies
		};

		bool canTriggerBeforeDies = engine.CanTriggerReact(state, banishedDeath, reactingCard, beforeDiesAbility, out string reason1);
		Assert.False(canTriggerBeforeDies);
		Assert.Contains("banish", reason1, StringComparison.OrdinalIgnoreCase);

		AbilityDefinition killAbility = new AbilityDefinition
		{
			Trigger = AbilityTrigger.React,
			Effects = Array.Empty<IEffect>(),
			Timing = ReactTiming.Kill
		};

		bool canTriggerKill = engine.CanTriggerReact(state, banishedDeath, reactingCard, killAbility, out string reason2);
		Assert.False(canTriggerKill);
		Assert.Contains("banish", reason2, StringComparison.OrdinalIgnoreCase);
	}

	#endregion

	#region Fix 5: Advantage / Disadvantage Mutual Cancellation

	[Fact]
	public void Fix5_PerformStrikeEffect_AdvantageAndDisadvantage_CancelOut()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState state = new GameState
		{
			TurnNumber = 1,
			Players = new List<PlayerState>
			{
				new PlayerState
				{
					PlayerIndex = 0,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 1,
								DefinitionId = "fighter",
								Rank = 1
							}
						}
					}
				},
				new PlayerState
				{
					PlayerIndex = 1,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 2,
								DefinitionId = "fighter",
								Rank = 1,
								CurrentArmorClass = 15,
								CurrentHitPoints = 10
							}
						}
					}
				}
			}
		};

		// Both Advantage and Disadvantage active on attacker
		int diceCallCount = 0;
		PerformStrikeEffect effect = new PerformStrikeEffect(
			StrikeBonus: 0,
			HasAdvantage: true,
			HasDisadvantage: true
		);

		EffectContext context = new EffectContext
		{
			State = state,
			CardDb = cardDb,
			SourceInstanceId = 1,
			TargetInstanceId = 2,
			RollDice = (count, sides) =>
			{
				diceCallCount++;
				return 10;
			}
		};

		var events = effect.Evaluate(context).ToList();
		// Because Advantage and Disadvantage cancel out, only 1d20 was rolled!
		Assert.Equal(1, diceCallCount);
	}

	[Fact]
	public void Fix5_ResolveAttackEffect_AdvantageAndDisadvantage_CancelOut()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState state = new GameState
		{
			TurnNumber = 1,
			Players = new List<PlayerState>
			{
				new PlayerState { PlayerIndex = 0 },
				new PlayerState
				{
					PlayerIndex = 1,
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 2,
								DefinitionId = "fighter",
								Rank = 1,
								CurrentArmorClass = 15
							}
						}
					}
				}
			}
		};

		int diceCallCount = 0;
		ResolveAttackEffect effect = new ResolveAttackEffect
		{
			StrikeBonus = 0,
			HasAdvantage = true,
			HasDisadvantage = true
		};

		EffectContext context = new EffectContext
		{
			State = state,
			CardDb = cardDb,
			SourceInstanceId = 1,
			TargetInstanceId = 2,
			RollDice = (count, sides) =>
			{
				diceCallCount++;
				return 10;
			}
		};

		var events = effect.Evaluate(context).ToList();
		Assert.Equal(1, diceCallCount);
	}

	#endregion

	#region Fix 6: Unique Enters-Play Check & Deck Validation Rules

	[Fact]
	public void Fix6_CannotPlayUniqueCard_IfSameNameAlreadyControlled()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState state = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			Players = new List<PlayerState>
			{
				new PlayerState
				{
					PlayerIndex = 0,
					Hand = new List<CardInstance>
					{
						new CardInstance
						{
							InstanceId = 20,
							DefinitionId = "unique-hero-alt", // "Lord Valen" (Unique)
							Zone = CardZone.Hand,
							OwnerPlayerIndex = 0
						}
					},
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 1,
								DefinitionId = "unique-hero", // "Lord Valen" (Unique)
								Zone = CardZone.Board,
								Rank = 1,
								OwnerPlayerIndex = 0
							}
						}
					}
				},
				new PlayerState { PlayerIndex = 1 }
			}
		};

		GameEngine engine = new GameEngine(state, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 20,
			TargetRank = 1
		};

		ActionResult result = engine.CanExecute(action, state);
		Assert.False(result.IsSuccess);
		Assert.Contains(result.Errors, e => e.Contains("Unique") && e.Contains("already controls a card with the same name in play"));
	}

	[Fact]
	public void Fix6_CannotEquipUniqueItem_IfSameNameAlreadyControlled()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState state = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			Players = new List<PlayerState>
			{
				new PlayerState
				{
					PlayerIndex = 0,
					Hand = new List<CardInstance>
					{
						new CardInstance
						{
							InstanceId = 20,
							DefinitionId = "unique-sword", // "Sunblade" (Unique)
							Zone = CardZone.Hand,
							OwnerPlayerIndex = 0
						}
					},
					Ranks = new SortedDictionary<int, List<CardInstance>>
					{
						[1] = new List<CardInstance>
						{
							new CardInstance
							{
								InstanceId = 1,
								DefinitionId = "fighter",
								Zone = CardZone.Board,
								Rank = 1,
								OwnerPlayerIndex = 0,
								AttachedItemIds = new List<int> { 99 }
							},
							new CardInstance
							{
								InstanceId = 2,
								DefinitionId = "fighter",
								Zone = CardZone.Board,
								Rank = 1,
								OwnerPlayerIndex = 0
							}
						}
					},
					AttachedItems = new List<CardInstance>
					{
						new CardInstance
						{
							InstanceId = 99,
							DefinitionId = "unique-sword", // "Sunblade" (Unique)
							Zone = CardZone.Attached,
							AttachedToInstanceId = 1,
							OwnerPlayerIndex = 0
						}
					}
				},
				new PlayerState { PlayerIndex = 1 }
			}
		};

		GameEngine engine = new GameEngine(state, cardDb);
		// Attempt to equip second copy of Sunblade on character 2
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 20,
			TargetCharacterId = 2
		};

		ActionResult result = engine.CanExecute(action, state);
		Assert.False(result.IsSuccess);
		Assert.Contains(result.Errors, e => e.Contains("Unique") && e.Contains("already controls a card with the same name in play"));
	}

	[Fact]
	public void Fix6_DeckValidate_Rejects_MoreThanOneCopyOfEpicCard()
	{
		CardDatabase cardDb = CreateTestCardDb();
		List<string> cards = new List<string>();
		// 2 copies of epic-spell
		cards.Add("epic-spell");
		cards.Add("epic-spell");
		// 48 copies of regular fighter
		for (int i = 0; i < 16; i++)
		{
			cards.Add("fighter");
		}
		for (int i = 0; i < 32; i++)
		{
			cards.Add("regular-action");
		}

		Deck deck = new Deck
		{
			DeckName = "Epic Test Deck",
			WarlordDefinitionId = "warlord-hero",
			CardDefinitionIds = cards
		};

		ActionResult result = deck.Validate(cardDb, 50);
		Assert.False(result.IsSuccess);
		Assert.Contains(result.Errors, e => e.Contains("Epic") && e.Contains("exceeds the maximum limit of 1 copy"));
	}

	[Fact]
	public void Fix6_DeckValidate_Rejects_MoreThan50Percent_ActionsOrItems()
	{
		Dictionary<string, CardDefinition> dict = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			["warlord-hero"] = new CardDefinition
			{
				DefinitionId = "warlord-hero",
				Name = "Grand Warlord",
				Type = CardType.Character,
				IsWarlord = true,
				Level = 3
			}
		};
		List<string> cards = new List<string>();
		// 26 Actions out of 50 cards = 52% > 50%
		for (int i = 0; i < 26; i++)
		{
			// unique names to not trigger 3 copy limit
			dict[$"action_{i}"] = new CardDefinition
			{
				DefinitionId = $"action_{i}",
				Name = $"Action #{i}",
				Type = CardType.Action,
				Level = 1
			};
			cards.Add($"action_{i}");
		}
		for (int i = 0; i < 24; i++)
		{
			dict[$"fighter_{i}"] = new CardDefinition
			{
				DefinitionId = $"fighter_{i}",
				Name = $"Fighter #{i}",
				Type = CardType.Character,
				Level = 1
			};
			cards.Add($"fighter_{i}");
		}
		CardDatabase cardDb = new CardDatabase { Definitions = dict };

		Deck deck = new Deck
		{
			DeckName = "Too Many Actions",
			WarlordDefinitionId = "warlord-hero",
			CardDefinitionIds = cards
		};

		ActionResult result = deck.Validate(cardDb, 50);
		Assert.False(result.IsSuccess);
		Assert.Contains(result.Errors, e => e.Contains("Action cards") && e.Contains("exceeds the maximum limit of 50% of the deck"));
	}

	#endregion
}
