using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Events;

namespace WarlordEngine.Engine;

public sealed partial class GameEngine
{
	private static CardDatabase? _masterCardDb;

	private static CardDatabase? _lastActiveCardDb;

	private readonly GameState _state;

	private readonly EventResolver _resolver;

	private readonly CardDatabase _cardDb;

	private DiceRoller _diceRoller;

	public static readonly DiceRoller DefaultDiceRoller = (int count, int sides) => 15;

	public DiceRoller DiceRoller
	{
		get
		{
			return _diceRoller;
		}
		set
		{
			_diceRoller = value ?? DefaultDiceRoller;
		}
	}

	public bool AutoPassWhenNoReactsAvailable
	{
		get
		{
			return _state.AutoPassWhenNoReactsAvailable;
		}
		set
		{
			_state.AutoPassWhenNoReactsAvailable = value;
		}
	}

	public GameState State => _state;

	public CardDatabase CardDb => _cardDb;

	public EventResolver Resolver => _resolver;

	public (int WinnerIndex, int Roll0, int Roll1)? LastInitiativeRoll { get; private set; }

	public List<(int Roll0, int Roll1)> InitiativeTieHistory { get; } = new List<(int, int)>();

	public Action<AttackResolutionDetails>? OnAttackResolved { get; set; }

	public Func<StrikeTargetPrompt, int?>? SelectStrikeTarget { get; set; }

	public static CardDatabase GetActiveCardDb()
	{
		if (_masterCardDb != null)
		{
			return _masterCardDb;
		}
		string text3 = DataPaths.CardsJsonPath;
		if (File.Exists(text3))
		{
			_masterCardDb = DatabaseLoader.LoadFromFile(text3);
			return _masterCardDb;
		}
		if (_lastActiveCardDb != null)
		{
			return _lastActiveCardDb;
		}
		throw new InvalidOperationException("CardDatabase not initialized and Data/cards.json could not be found.");
	}

	public static CardDatabase GetLastActiveCardDb() => _lastActiveCardDb ?? GetActiveCardDb();

	public void SetDiceRoller(DiceRoller roller)
	{
		ArgumentNullException.ThrowIfNull(roller, "roller");
		_diceRoller = roller;
	}

	public void SetRandomSeed(int seed)
	{
		Random rng = new Random(seed);
		_diceRoller = (int count, int sides) =>
		{
			int total = 0;
			for (int i = 0; i < count; i++)
			{
				total += rng.Next(1, sides + 1);
			}
			return total;
		};
	}

	public GameEngine(GameState state, CardDatabase cardDb, DiceRoller? diceRoller = null)
	{
		ArgumentNullException.ThrowIfNull(state, "state");
		ArgumentNullException.ThrowIfNull(cardDb, "cardDb");
		_state = state;
		_cardDb = cardDb;
		_lastActiveCardDb = cardDb;
		_diceRoller = diceRoller ?? DefaultDiceRoller;
		_resolver = new EventResolver();
		if (_state.PendingEvents.Count > 0)
		{
			_resolver.RestoreStack(_state.PendingEvents);
		}
		_resolver.RegisterPostHandler(new MagicDamageResistanceHandler(this));
		_resolver.RegisterPostHandler(new DeathCheckHandler(_cardDb));
		_resolver.RegisterPostHandler(new AttachedItemDefeatHandler());
		_resolver.RegisterPostHandler(new WarlordEliminationHandler());
		_resolver.RegisterPostHandler(new AbilityEffectHandler(_cardDb, (int min, int max) => _diceRoller(1, max), () => OnAttackResolved));
		_resolver.RegisterPostHandler(new AttackResolutionHandler(this));
	}

	public static GameEngine CreateFromDecks(IReadOnlyList<Deck> playerDecks, CardDatabase cardDb, int minDeckSize = 1, DiceRoller? diceRoller = null)
	{
		ArgumentNullException.ThrowIfNull(playerDecks, "playerDecks");
		ArgumentNullException.ThrowIfNull(cardDb, "cardDb");
		if (playerDecks.Count < 2)
		{
			throw new ArgumentException("At least two player decks are required.", "playerDecks");
		}
		List<PlayerState> list = new List<PlayerState>();
		int nextInstanceId = 1;
		for (int i = 0; i < playerDecks.Count; i++)
		{
			Deck deck = playerDecks[i];
			ActionResult actionResult = deck.Validate(cardDb, minDeckSize);
			if (!actionResult.IsSuccess)
			{
				throw new InvalidOperationException($"Player {i} deck validation failed: {string.Join("; ", actionResult.Errors)}");
			}
			List<CardInstance> list2 = new List<CardInstance>();
			List<CardInstance> list3 = new List<CardInstance>();
			CardDefinition definition = cardDb.GetDefinition(deck.WarlordDefinitionId);
			list2.Add(new CardInstance
			{
				InstanceId = nextInstanceId++,
				DefinitionId = definition.DefinitionId,
				CurrentHitPoints = definition.BaseHitPoints.GetValueOrDefault(),
				CurrentArmorClass = definition.ArmorClass.GetValueOrDefault(),
				CurrentSkill = definition.Skill.GetValueOrDefault(),
				ReadyState = ReadyState.Ready,
				Zone = CardZone.Hand,
				OwnerPlayerIndex = i,
				SkillRatings = new Dictionary<string, int>(definition.SkillRatings, StringComparer.OrdinalIgnoreCase)
			});
			if (deck.StartingCharacterDefinitionIds.Count > 0)
			{
				foreach (string startingCharacterDefinitionId in deck.StartingCharacterDefinitionIds)
				{
					CardDefinition definition2 = cardDb.GetDefinition(startingCharacterDefinitionId);
					list2.Add(new CardInstance
					{
						InstanceId = nextInstanceId++,
						DefinitionId = definition2.DefinitionId,
						CurrentHitPoints = definition2.BaseHitPoints.GetValueOrDefault(),
						CurrentArmorClass = definition2.ArmorClass.GetValueOrDefault(),
						CurrentSkill = definition2.Skill.GetValueOrDefault(),
						ReadyState = ReadyState.Ready,
						Zone = CardZone.Hand,
						OwnerPlayerIndex = i,
						SkillRatings = new Dictionary<string, int>(definition2.SkillRatings, StringComparer.OrdinalIgnoreCase)
					});
				}
				foreach (string cardDefinitionId in deck.CardDefinitionIds)
				{
					CardDefinition definition3 = cardDb.GetDefinition(cardDefinitionId);
					list3.Add(new CardInstance
					{
						InstanceId = nextInstanceId++,
						DefinitionId = definition3.DefinitionId,
						CurrentHitPoints = definition3.BaseHitPoints.GetValueOrDefault(),
						CurrentArmorClass = definition3.ArmorClass.GetValueOrDefault(),
						CurrentSkill = definition3.Skill.GetValueOrDefault(),
						ReadyState = ReadyState.Ready,
						Zone = CardZone.Deck,
						OwnerPlayerIndex = i
					});
				}
			}
			else if (deck.CardDefinitionIds.Count <= 5)
			{
				foreach (string cardDefinitionId2 in deck.CardDefinitionIds)
				{
					CardDefinition definition4 = cardDb.GetDefinition(cardDefinitionId2);
					list2.Add(new CardInstance
					{
						InstanceId = nextInstanceId++,
						DefinitionId = definition4.DefinitionId,
						CurrentHitPoints = definition4.BaseHitPoints.GetValueOrDefault(),
						CurrentArmorClass = definition4.ArmorClass.GetValueOrDefault(),
						CurrentSkill = definition4.Skill.GetValueOrDefault(),
						ReadyState = ReadyState.Ready,
						Zone = CardZone.Hand,
						OwnerPlayerIndex = i,
						SkillRatings = new Dictionary<string, int>(definition4.SkillRatings, StringComparer.OrdinalIgnoreCase)
					});
				}
			}
			else
			{
				foreach (string cardDefinitionId3 in deck.CardDefinitionIds)
				{
					CardDefinition definition5 = cardDb.GetDefinition(cardDefinitionId3);
					list3.Add(new CardInstance
					{
						InstanceId = nextInstanceId++,
						DefinitionId = definition5.DefinitionId,
						CurrentHitPoints = definition5.BaseHitPoints.GetValueOrDefault(),
						CurrentArmorClass = definition5.ArmorClass.GetValueOrDefault(),
						CurrentSkill = definition5.Skill.GetValueOrDefault(),
						ReadyState = ReadyState.Ready,
						Zone = CardZone.Deck,
						OwnerPlayerIndex = i,
						SkillRatings = new Dictionary<string, int>(definition5.SkillRatings, StringComparer.OrdinalIgnoreCase)
					});
				}
			}
			list.Add(new PlayerState
			{
				PlayerIndex = i,
				DisplayName = $"Player {i + 1} ({deck.DeckName})",
				Hand = list2,
				Deck = list3,
				Ranks = new SortedDictionary<int, List<CardInstance>>()
			});
		}
		return new GameEngine(new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Setup,
			NextInstanceId = nextInstanceId,
			Players = list
		}, cardDb, diceRoller);
	}

	public void ShuffleDeck(int playerIndex, Random? rng = null)
	{
		if (playerIndex >= 0 && playerIndex < _state.Players.Count)
		{
			List<CardInstance> deck = _state.Players[playerIndex].Deck;
			Random random = rng ?? new Random();
			for (int i = deck.Count - 1; i > 0; i--)
			{
				int j = random.Next(i + 1);
				(deck[i], deck[j]) = (deck[j], deck[i]);
			}
		}
	}

	public void ShuffleAllDecks(Random? rng = null)
	{
		Random rng2 = rng ?? new Random();
		for (int i = 0; i < _state.Players.Count; i++)
		{
			ShuffleDeck(i, rng2);
		}
	}

	public void DiscardCardFromHand(int playerIndex, int cardInstanceId)
	{
		PlayerState player = _state.Players[playerIndex];
		int idx = player.Hand.FindIndex((CardInstance c) => c.InstanceId == cardInstanceId);
		if (idx >= 0)
		{
			CardInstance card = player.Hand[idx];
			player.Hand.RemoveAt(idx);
			player.DiscardPile.Add(card with { Zone = CardZone.Discard });
		}
	}

	public ActionResult Execute(IPlayerAction action)
	{
		ArgumentNullException.ThrowIfNull(action, "action");
		ActionResult actionResult = CanExecute(action, _state);
		if (!actionResult.IsSuccess)
		{
			return actionResult;
		}
		bool isReactWindowOpen = _state.Priority.IsReactWindowOpen;
		bool isPass = action is PassAction;
		PushEventsForAction(action);
		if (isReactWindowOpen)
		{
			if (isPass)
			{
				int requiredPasses = Math.Max(2, _state.Players.Count);
				if (_state.Priority.ConsecutivePasses >= requiredPasses)
				{
					ResolveReactWindowStack();
				}
				SyncPendingEvents();
				return ActionResult.Success();
			}
			_state.Priority.ConsecutivePasses = 0;
			_state.Priority.HasPassed = false;
			if (_state.Players.Count > 0)
			{
				_state.Priority.ActivePlayerIndex = (action.PlayerIndex + 1) % _state.Players.Count;
			}
			SyncPendingEvents();
			return ActionResult.Success();
		}
		DrainResolverStack(action.PlayerIndex);
		SyncPendingEvents();
		if (!_state.Priority.IsReactWindowOpen)
		{
			if (!isPass && !(action is DeployStartingArmyAction))
			{
				if (_state.CurrentPhase == TurnPhase.Order)
				{
					_state.TurnState.RecordAction();
				}
			}
			else if (isPass && _state.CurrentPhase == TurnPhase.Main && _state.Priority.ConsecutivePasses >= 2)
			{
				PerformTurnEndSurvivalChecks();
				_state.CurrentPhase = TurnPhase.Ready;
				_state.TurnNumber++;
				_state.Priority.ConsecutivePasses = 0;
				_state.Priority.HasPassed = false;
				UnspendAndUnstunCards();
			}
		}
		return ActionResult.Success();
	}

	public ActionResult ExecuteAction(IPlayerAction action)
	{
		return Execute(action);
	}

	private static List<int> GetFriendlyBoardCharacterIds(PlayerState player)
	{
		List<int> list = new List<int>();
		foreach (List<CardInstance> value in player.Ranks.Values)
		{
			foreach (CardInstance item in value)
			{
				list.Add(item.InstanceId);
			}
		}
		return list;
	}

	private static CardInstance? FindBoardCard(GameState state, int instanceId)
	{
		foreach (PlayerState player in state.Players)
		{
			foreach (List<CardInstance> value in player.Ranks.Values)
			{
				foreach (CardInstance item in value)
				{
					if (item.InstanceId == instanceId)
					{
						return item;
					}
				}
			}
		}
		return null;
	}

	public static CardInstance? FindAttachedItem(GameState state, int instanceId)
	{
		foreach (PlayerState player in state.Players)
		{
			foreach (CardInstance item in player.AttachedItems)
			{
				if (item.InstanceId == instanceId)
				{
					return item;
				}
			}
		}
		return null;
	}

	public int GetInitiativeModifier(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _state.Players.Count)
		{
			return 0;
		}
		PlayerState playerState = _state.Players[playerIndex];
		int totalInitiative = 0;
		foreach (List<CardInstance> rankCards in playerState.Ranks.Values)
		{
			foreach (CardInstance item in rankCards)
			{
				totalInitiative += GetTurnBuffValue(_state, item.InstanceId, "INITIATIVE");
				if (item.SkillRatings.TryGetValue("Initiative", out int initVal))
				{
					totalInitiative += initVal;
				}
				if (item.SkillRatings.TryGetValue("BonusInitiative", out int bonusInit))
				{
					totalInitiative += bonusInit;
				}
				CardDefinition definition = _cardDb.GetDefinition(item.DefinitionId);
				if (definition != null)
				{
					if (definition.SkillRatings.TryGetValue("Initiative", out int defInit) && !item.SkillRatings.ContainsKey("Initiative"))
					{
						totalInitiative += defInit;
					}
					foreach (string trait in definition.Traits)
					{
						Match match = Regex.Match(trait, @"^\+(\d+)\s+Initiative", RegexOptions.IgnoreCase);
						if (match.Success && int.TryParse(match.Groups[1].Value, out int traitVal))
						{
							totalInitiative += traitVal;
						}
					}
				}
			}
		}
		foreach (CardInstance attached in playerState.AttachedItems)
		{
			totalInitiative += GetTurnBuffValue(_state, attached.InstanceId, "INITIATIVE");
			if (attached.SkillRatings.TryGetValue("Initiative", out int initVal2))
			{
				totalInitiative += initVal2;
			}
			if (attached.SkillRatings.TryGetValue("BonusInitiative", out int bonusInit2))
			{
				totalInitiative += bonusInit2;
			}
		}
		return totalInitiative;
	}

	public (int WinnerIndex, int Roll0, int Roll1) RollInitiative()
	{
		InitiativeTieHistory.Clear();
		int attempts = 0;
		int raw0;
		int raw1;
		int winner = 0;
		do
		{
			raw0 = _diceRoller(1, 20);
			raw1 = _diceRoller(1, 20);
			attempts++;

			int mod0 = GetInitiativeModifier(0);
			int mod1 = GetInitiativeModifier(1);

			bool isNat20_0 = (raw0 == 20);
			bool isNat20_1 = (raw1 == 20);

			if (isNat20_0 && !isNat20_1)
			{
				winner = 0;
				break;
			}
			if (isNat20_1 && !isNat20_0)
			{
				winner = 1;
				break;
			}

			int total0 = raw0 + mod0;
			int total1 = raw1 + mod1;

			if (total0 == total1)
			{
				if (attempts < 100)
				{
					InitiativeTieHistory.Add((raw0, raw1));
				}
				continue;
			}

			winner = (total0 > total1) ? 0 : 1;
			break;
		}
		while (attempts < 100);

		_state.InitiativeWinnerIndex = winner;
		_state.ActivePlayerIndex = winner;
		_state.Priority.ActivePlayerIndex = winner;
		_state.Priority.ConsecutivePasses = 0;
		_state.Priority.HasPassed = false;
		LastInitiativeRoll = (winner, raw0, raw1);
		return (WinnerIndex: winner, Roll0: raw0, Roll1: raw1);
	}

	public void AdvancePhase()
	{
		TurnPhase currentPhase = _state.CurrentPhase;
		TurnPhase turnPhase;
		switch (currentPhase)
		{
		case TurnPhase.Setup:
			turnPhase = TurnPhase.Ready;
			break;
		case TurnPhase.Ready:
			turnPhase = TurnPhase.Draw;
			break;
		case TurnPhase.Draw:
			turnPhase = TurnPhase.Order;
			break;
		case TurnPhase.Order:
		case TurnPhase.Combat:
			turnPhase = TurnPhase.EndOfTurn;
			break;
		case TurnPhase.EndOfTurn:
			PerformTurnEndSurvivalChecks();
			_state.ActivePlayerIndex = (_state.ActivePlayerIndex + 1) % _state.Players.Count;
			_state.TurnNumber++;
			turnPhase = TurnPhase.Ready;
			break;
		default:
			throw new InvalidOperationException($"Unknown phase: {currentPhase}");
		}
		if (turnPhase == TurnPhase.Order)
		{
			RollInitiative();
		}
		if (turnPhase == TurnPhase.Ready)
		{
			UnspendAndUnstunCards();
		}
		if (turnPhase == TurnPhase.Draw)
		{
			foreach (PlayerState player in _state.Players)
			{
				int cardsToDraw = 5 - player.Hand.Count;
				for (int i = 0; i < cardsToDraw && i < player.Deck.Count; i++)
				{
					_resolver.Push(new DrawCardEvent
					{
						PlayerIndex = player.PlayerIndex
					});
				}
			}
		}
		_resolver.Push(new PhaseChangeEvent
		{
			FromPhase = currentPhase,
			ToPhase = turnPhase
		});
		DrainResolverStack();
	}

	private void UnspendAndUnstunCards()
	{
		_state.TurnState.UsedLimitedAbilityKeysThisTurn.Clear();
		foreach (PlayerState player in _state.Players)
		{
			foreach (List<CardInstance> value in player.Ranks.Values)
			{
				for (int i = 0; i < value.Count; i++)
				{
					CardInstance cardInstance = value[i];
					if (cardInstance.AbilitiesUsedThisTurn.Count > 0)
					{
						List<int> list = new List<int>(cardInstance.AbilitiesUsedThisTurn);
						list.Clear();
						cardInstance = cardInstance with
						{
							AbilitiesUsedThisTurn = list
						};
					}
					if (cardInstance.ReadyState == ReadyState.Spent)
					{
						value[i] = cardInstance with
						{
							ReadyState = ReadyState.Ready
						};
					}
					else if (cardInstance.ReadyState == ReadyState.Stunned)
					{
						value[i] = cardInstance with
						{
							ReadyState = ReadyState.Spent
						};
					}
					else
					{
						value[i] = cardInstance;
					}
				}
			}
		}
	}

	private void PerformTurnEndSurvivalChecks()
	{
		List<(int, int)> list = new List<(int, int)>();
		foreach (PlayerState player in _state.Players)
		{
			foreach (List<CardInstance> value in player.Ranks.Values)
			{
				foreach (CardInstance item in value)
				{
					CardDefinition definition = _cardDb.GetDefinition(item.DefinitionId);
					if (definition != null && definition.Name.Equals("Ac'Vuk", StringComparison.OrdinalIgnoreCase) && !HasTurnBuff(_state, item.InstanceId, "WOUNDED_OPPONENT_THIS_TURN"))
					{
						list.Add((player.PlayerIndex, item.InstanceId));
					}
				}
			}
		}
		foreach (var (ownerPlayerIndex, defeatedInstanceId) in list)
		{
			_resolver.Push(new CharacterDefeatedEvent
			{
				DefeatedInstanceId = defeatedInstanceId,
				OwnerPlayerIndex = ownerPlayerIndex
			});
		}
		if (list.Count > 0)
		{
			DrainResolverStack();
		}
	}

	public static int ResolvePerformerCharacterId(GameState state, int instanceId, CardDatabase? cardDb = null)
	{
		return CardLocator.ResolvePerformerCharacterId(state, instanceId, cardDb);
	}

	public static CardInstance? ResolvePerformerCharacter(GameState state, int instanceId, CardDatabase? cardDb = null)
	{
		return CardLocator.ResolvePerformerCharacter(state, instanceId, cardDb);
	}

	public static (int PlayerIndex, int Rank, int Index)? FindCharacterPosition(GameState state, int instanceId)
	{
		for (int i = 0; i < state.Players.Count; i++)
		{
			foreach (var (item, list2) in state.Players[i].Ranks)
			{
				for (int j = 0; j < list2.Count; j++)
				{
					if (list2[j].InstanceId == instanceId)
					{
						return (i, item, j);
					}
				}
			}
		}
		return null;
	}

}
