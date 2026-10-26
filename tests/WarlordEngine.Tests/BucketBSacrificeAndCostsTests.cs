using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public class BucketBSacrificeAndCostsTests
{
	private readonly CardDatabase _cardDb;

	private static CardDatabase LoadDb()
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory);
		while (directoryInfo != null && !File.Exists(Path.Combine(directoryInfo.FullName, "WarlordCCG.sln")))
		{
			directoryInfo = directoryInfo.Parent;
		}
		string path = directoryInfo?.FullName ?? "C:\\Users\\iamfr\\Documents\\WarlordCCG";
		string jsonFilePath = Path.Combine(path, "DataParser", "cards.json");
		return DatabaseLoader.LoadFromFile(jsonFilePath);
	}

	public BucketBSacrificeAndCostsTests()
	{
		_cardDb = LoadDb();
	}

	private CardDefinition GetDef(string name)
	{
		CardDefinition cardDefinition = _cardDb.Definitions.Values.FirstOrDefault((CardDefinition d) => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
		if (cardDefinition == null)
		{
			string definitionId = name.ToLowerInvariant().Replace(" ", "_").Replace("'", "")
				.Replace(",", "");
			cardDefinition = _cardDb.GetDefinition(definitionId);
		}
		Assert.NotNull(cardDefinition);
		return cardDefinition;
	}

	private CardInstance CreateCharacterInstance(CardDefinition def, int instanceId, int ownerIndex, int rank)
	{
		return new CardInstance
		{
			InstanceId = instanceId,
			DefinitionId = def.DefinitionId,
			CurrentHitPoints = (def.BaseHitPoints ?? 2),
			CurrentArmorClass = (def.ArmorClass ?? 12),
			CurrentSkill = def.Level,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = ownerIndex,
			Rank = rank,
			SkillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
			AttachedItemIds = new List<int>()
		};
	}

	private CardInstance CreateItemInstance(CardDefinition def, int instanceId, int ownerIndex, int hostCharacterId)
	{
		return new CardInstance
		{
			InstanceId = instanceId,
			DefinitionId = def.DefinitionId,
			CurrentHitPoints = 1,
			CurrentArmorClass = 10,
			CurrentSkill = 0,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Attached,
			OwnerPlayerIndex = ownerIndex,
			Rank = 0,
			AttachedToInstanceId = hostCharacterId,
			SkillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
			AttachedItemIds = new List<int>()
		};
	}

	private static CardInstance? FindCardInState(GameState state, int instanceId)
	{
		foreach (PlayerState player in state.Players)
		{
			foreach (List<CardInstance> value4 in player.Ranks.Values)
			{
				CardInstance value = value4.FirstOrDefault((CardInstance x) => x.InstanceId == instanceId);
				if (value.InstanceId == instanceId)
				{
					return value;
				}
			}
			CardInstance value2 = player.Hand.FirstOrDefault((CardInstance x) => x.InstanceId == instanceId);
			if (value2.InstanceId == instanceId)
			{
				return value2;
			}
			CardInstance value3 = player.DiscardPile.FirstOrDefault((CardInstance x) => x.InstanceId == instanceId);
			if (value3.InstanceId == instanceId)
			{
				return value3;
			}
		}
		return null;
	}

	private static void MutateCard(GameState state, int instanceId, Action<CardInstance> mutate)
	{
		foreach (PlayerState player in state.Players)
		{
			foreach (List<CardInstance> value in player.Ranks.Values)
			{
				for (int i = 0; i < value.Count; i++)
				{
					if (value[i].InstanceId == instanceId)
					{
						CardInstance obj = value[i];
						mutate(obj);
						return;
					}
				}
			}
		}
	}

	private GameState CreateInitialState()
	{
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0,
			DisplayName = "Player 1",
			Deck = new List<CardInstance>(),
			Hand = new List<CardInstance>(),
			DiscardPile = new List<CardInstance>(),
			Ranks = new SortedDictionary<int, List<CardInstance>>
			{
				[1] = new List<CardInstance>(),
				[2] = new List<CardInstance>(),
				[3] = new List<CardInstance>()
			}
		};
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Player 2",
			Deck = new List<CardInstance>(),
			Hand = new List<CardInstance>(),
			DiscardPile = new List<CardInstance>(),
			Ranks = new SortedDictionary<int, List<CardInstance>>
			{
				[1] = new List<CardInstance>(),
				[2] = new List<CardInstance>(),
				[3] = new List<CardInstance>()
			}
		};
		GameState gameState = new GameState
		{
			CurrentPhase = TurnPhase.Main,
			ActivePlayerIndex = 0
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		span[0] = playerState;
		span[1] = playerState2;
		gameState.Players = list;
		return gameState;
	}

	[Fact]
	public void BurningScroll_DestroysItemOnSpend_AndPerformsRangedStrike()
	{
		GameState gameState = CreateInitialState();
		CardDefinition def = GetDef("Novice Pyro");
		CardDefinition def2 = GetDef("Burning Scroll");
		CardDefinition def3 = GetDef("Church Inquisitor");
		CardInstance item = CreateCharacterInstance(def, 10, 0, 1);
		CardInstance item2 = CreateItemInstance(def2, 11, 0, 10);
		item.AttachedItemIds.Add(11);
		CardInstance item3 = CreateCharacterInstance(def3, 20, 1, 1);
		gameState.Players[0].Ranks[1].Add(item);
		gameState.Players[0].DiscardPile.Add(item2);
		gameState.Players[1].Ranks[1].Add(item3);
		GameEngine gameEngine = new GameEngine(gameState, _cardDb, (int count, int sides) => 20);
		Assert.NotEmpty(def2.Abilities);
		AbilityDefinition abilityDefinition = def2.Abilities[0];
		Assert.True(abilityDefinition.DestroysSource);
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 11,
			AbilityIndex = 0,
			TargetInstanceIds = new int[1] { 20 }
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.True(actionResult.IsSuccess, string.Join(", ", actionResult.Errors));
		ActionResult actionResult2 = gameEngine.Execute(action);
		Assert.True(actionResult2.IsSuccess, string.Join(", ", actionResult2.Errors));
		CardInstance? value = FindCardInState(gameState, 11);
		Assert.NotNull(value);
		Assert.Equal(CardZone.Discard, value.Value.Zone);
		Assert.Null(value.Value.AttachedToInstanceId);
		CardInstance? value2 = FindCardInState(gameState, 10);
		Assert.NotNull(value2);
		Assert.DoesNotContain(11, value2.Value.AttachedItemIds);
		Assert.Contains(gameState.Players[0].DiscardPile, (CardInstance c) => c.InstanceId == 11);
	}

	[Fact]
	public void HodsLegacy_MultiLineAbilities_DestroysItemOnSpend()
	{
		GameState gameState = CreateInitialState();
		CardDefinition def = GetDef("Church Inquisitor");
		CardDefinition def2 = GetDef("Hod's Legacy");
		CardDefinition def3 = GetDef("Ambitious Squire");
		Assert.True(def2.Abilities.Count >= 2, $"Expected at least 2 abilities on Hod's Legacy, found {def2.Abilities.Count}");
		CardInstance item = CreateCharacterInstance(def, 10, 0, 1);
		CardInstance item2 = CreateItemInstance(def2, 11, 0, 10);
		item.AttachedItemIds.Add(11);
		CardInstance item3 = CreateCharacterInstance(def3, 12, 0, 1)with
		{
			ReadyState = ReadyState.Spent
		};
		gameState.Players[0].Ranks[1].Add(item);
		gameState.Players[0].Ranks[1].Add(item3);
		gameState.Players[0].DiscardPile.Add(item2);
		GameEngine gameEngine = new GameEngine(gameState, _cardDb);
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 11,
			AbilityIndex = 0,
			TargetInstanceIds = new int[1] { 12 }
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.True(actionResult.IsSuccess, string.Join(", ", actionResult.Errors));
		ActionResult actionResult2 = gameEngine.Execute(action);
		Assert.True(actionResult2.IsSuccess, string.Join(", ", actionResult2.Errors));
		CardInstance? value = FindCardInState(gameState, 11);
		Assert.NotNull(value);
		Assert.Equal(CardZone.Discard, value.Value.Zone);
		Assert.Null(value.Value.AttachedToInstanceId);
		CardInstance? value2 = FindCardInState(gameState, 12);
		Assert.NotNull(value2);
		Assert.Equal(ReadyState.Ready, value2.Value.ReadyState);
	}

	[Fact]
	public void Cloudracer_MultiLineAbilities_DiscardCardCostEnforcedAndPaid()
	{
		GameState gameState = CreateInitialState();
		CardDefinition def = GetDef("Church Inquisitor");
		CardDefinition def2 = GetDef("Cloudracer");
		Assert.True(def2.Abilities.Count >= 2, $"Expected at least 2 abilities on Cloudracer, found {def2.Abilities.Count}");
		Assert.Equal(1, def2.Abilities[0].DiscardHandCost);
		CardInstance item = CreateCharacterInstance(def, 10, 0, 2)with
		{
			ReadyState = ReadyState.Spent
		};
		CardInstance item2 = CreateItemInstance(def2, 11, 0, 10);
		item.AttachedItemIds.Add(11);
		gameState.Players[0].Ranks[2].Add(item);
		gameState.Players[0].DiscardPile.Add(item2);
		GameEngine gameEngine = new GameEngine(gameState, _cardDb);
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 11,
			AbilityIndex = 0,
			TargetInstanceIds = Array.Empty<int>()
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("discard cost", StringComparison.OrdinalIgnoreCase));
		CardInstance item3 = new CardInstance
		{
			InstanceId = 99,
			DefinitionId = "cleave",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready,
			SkillRatings = new Dictionary<string, int>()
		};
		gameState.Players[0].Hand.Add(item3);
		ActionResult actionResult2 = gameEngine.CanExecute(action);
		Assert.True(actionResult2.IsSuccess, string.Join(", ", actionResult2.Errors));
		ActionResult actionResult3 = gameEngine.Execute(action);
		Assert.True(actionResult3.IsSuccess, string.Join(", ", actionResult3.Errors));
		Assert.Empty(gameState.Players[0].Hand);
		Assert.Contains(gameState.Players[0].DiscardPile, (CardInstance c) => c.InstanceId == 99);
		CardInstance? value = FindCardInState(gameState, 10);
		Assert.NotNull(value);
		Assert.Equal(ReadyState.Ready, value.Value.ReadyState);
	}

	[Fact]
	public void Kraigdem_SpendAdjacentAllyCostEnforcedAndPaid()
	{
		GameState gameState = CreateInitialState();
		CardDefinition def = GetDef("Kraig'dem");
		CardDefinition def2 = GetDef("Ambitious Squire");
		CardDefinition def3 = GetDef("Church Inquisitor");
		CardInstance item = CreateCharacterInstance(def, 10, 0, 1);
		CardInstance item2 = CreateCharacterInstance(def2, 11, 0, 1);
		CardInstance item3 = CreateCharacterInstance(def3, 20, 1, 1);
		gameState.Players[0].Ranks[1].Add(item);
		gameState.Players[1].Ranks[1].Add(item3);
		GameEngine gameEngine = new GameEngine(gameState, _cardDb, (int count, int sides) => 15);
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 10,
			AbilityIndex = 0,
			TargetInstanceIds = new int[1] { 20 }
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("adjacent ready ally", StringComparison.OrdinalIgnoreCase));
		item2.ReadyState = ReadyState.Spent;
		gameState.Players[0].Ranks[1].Add(item2);
		ActionResult actionResult2 = gameEngine.CanExecute(action);
		Assert.False(actionResult2.IsSuccess);
		gameState.Players[0].Ranks[1][1] = gameState.Players[0].Ranks[1][1]with
		{
			ReadyState = ReadyState.Ready
		};
		ActionResult actionResult3 = gameEngine.CanExecute(action);
		Assert.True(actionResult3.IsSuccess, string.Join(", ", actionResult3.Errors));
		ActionResult actionResult4 = gameEngine.Execute(action);
		Assert.True(actionResult4.IsSuccess, string.Join(", ", actionResult4.Errors));
		CardInstance? value = FindCardInState(gameState, 11);
		Assert.NotNull(value);
		Assert.Equal(ReadyState.Spent, value.Value.ReadyState);
	}

	[Fact]
	public void CrownlandStrategist_DeckMillCostAndStrikeBonusCalculated()
	{
		GameState gameState = CreateInitialState();
		CardDefinition def = GetDef("Crownland Strategist");
		CardDefinition def2 = GetDef("Church Inquisitor");
		CardInstance item = CreateCharacterInstance(def, 10, 0, 1);
		CardInstance item2 = CreateCharacterInstance(def2, 20, 1, 1);
		CardDefinition def3 = GetDef("Church Inquisitor");
		CardInstance item3 = new CardInstance
		{
			InstanceId = 55,
			DefinitionId = def3.DefinitionId,
			Zone = CardZone.Deck,
			OwnerPlayerIndex = 1,
			ReadyState = ReadyState.Ready,
			SkillRatings = new Dictionary<string, int>()
		};
		gameState.Players[1].Deck.Add(item3);
		gameState.Players[0].Ranks[1].Add(item);
		gameState.Players[1].Ranks[1].Add(item2);
		int recordedBonus = 0;
		ResolveAttackEffect.GlobalOnAttackResolved = (AttackResolutionDetails details) =>
		{
			recordedBonus = details.StrikeBonus;
		};
		GameEngine gameEngine = new GameEngine(gameState, _cardDb, (int count, int sides) => 15);
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 10,
			AbilityIndex = 0,
			TargetInstanceIds = new int[1] { 20 }
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.True(actionResult.IsSuccess, string.Join(", ", actionResult.Errors));
		ActionResult actionResult2 = gameEngine.Execute(action);
		Assert.True(actionResult2.IsSuccess, string.Join(", ", actionResult2.Errors));
		Assert.Empty(gameState.Players[1].Deck);
		Assert.Contains(gameState.Players[1].DiscardPile, (CardInstance c) => c.InstanceId == 55 && c.Zone == CardZone.Discard);
		Assert.Equal(2 + def3.Level, recordedBonus);
		ResolveAttackEffect.GlobalOnAttackResolved = null;
	}

	[Fact]
	public void UnicornOfThun_MultiLine_PassivesAndBanishActionCost()
	{
		GameState gameState = CreateInitialState();
		CardDefinition def = GetDef("Church Inquisitor");
		CardDefinition def2 = GetDef("Unicorn of Thun");
		Assert.True(def2.Abilities.Count >= 2, $"Expected at least 2 abilities on Unicorn of Thun, found {def2.Abilities.Count}");
		Assert.Equal(2, def2.Abilities[1].BanishActionFromDiscardCost);
		CardInstance cardInstance = CreateCharacterInstance(def, 10, 0, 1)with
		{
			CurrentHitPoints = 1
		};
		CardInstance item = CreateItemInstance(def2, 11, 0, 10);
		cardInstance.AttachedItemIds.Add(11);
		gameState.Players[0].Ranks[1].Add(cardInstance);
		gameState.Players[0].DiscardPile.Add(item);
		int effectiveSkill = GameEngine.GetEffectiveSkill(gameState, cardInstance, _cardDb);
		Assert.Equal(def.Level + 2, effectiveSkill);
		GameEngine gameEngine = new GameEngine(gameState, _cardDb);
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 11,
			AbilityIndex = 1,
			TargetInstanceIds = new int[1] { 10 }
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("action card(s) in discard pile", StringComparison.OrdinalIgnoreCase));
		gameState.Players[0].DiscardPile.Add(new CardInstance
		{
			InstanceId = 91,
			DefinitionId = "blast",
			Zone = CardZone.Discard,
			OwnerPlayerIndex = 0,
			SkillRatings = new Dictionary<string, int>()
		});
		gameState.Players[0].DiscardPile.Add(new CardInstance
		{
			InstanceId = 92,
			DefinitionId = "cleave",
			Zone = CardZone.Discard,
			OwnerPlayerIndex = 0,
			SkillRatings = new Dictionary<string, int>()
		});
		ActionResult actionResult2 = gameEngine.CanExecute(action);
		Assert.True(actionResult2.IsSuccess, string.Join(", ", actionResult2.Errors));
		ActionResult actionResult3 = gameEngine.Execute(action);
		Assert.True(actionResult3.IsSuccess, string.Join(", ", actionResult3.Errors));
		Assert.DoesNotContain(gameState.Players[0].DiscardPile, (CardInstance c) => c.InstanceId == 91 || c.InstanceId == 92);
		CardInstance? cardInstance2 = FindCardInState(gameState, 10);
		Assert.Equal(2, cardInstance2?.CurrentHitPoints);
	}

	[Fact]
	public void Dissolve_DestroysEquippedItemWithinTwoRanks()
	{
		GameState gameState = CreateInitialState();
		CardDefinition def = GetDef("Adathan, Celestial Messenger");
		CardDefinition def2 = GetDef("Dissolve");
		CardDefinition def3 = GetDef("Church Inquisitor");
		CardDefinition def4 = GetDef("Bardiche");
		CardInstance item = CreateCharacterInstance(def, 10, 0, 1);
		CardInstance item2 = CreateCharacterInstance(def3, 20, 1, 2);
		CardInstance item3 = CreateItemInstance(def4, 21, 1, 20);
		item2.AttachedItemIds.Add(21);
		gameState.Players[0].Ranks[1].Add(item);
		gameState.Players[1].Ranks[2].Add(item2);
		gameState.Players[1].DiscardPile.Add(item3);
		CardInstance item4 = new CardInstance
		{
			InstanceId = 99,
			DefinitionId = def2.DefinitionId,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0,
			SkillRatings = new Dictionary<string, int>()
		};
		gameState.Players[0].Hand.Add(item4);
		GameEngine gameEngine = new GameEngine(gameState, _cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 99,
			PerformerInstanceId = 10,
			TargetCharacterId = 21
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.True(actionResult.IsSuccess, string.Join(", ", actionResult.Errors));
		ActionResult actionResult2 = gameEngine.Execute(action);
		Assert.True(actionResult2.IsSuccess, string.Join(", ", actionResult2.Errors));
		CardInstance? value = FindCardInState(gameState, 21);
		Assert.NotNull(value);
		Assert.Equal(CardZone.Discard, value.Value.Zone);
		Assert.Null(value.Value.AttachedToInstanceId);
		Assert.DoesNotContain(21, FindCardInState(gameState, 20)?.AttachedItemIds ?? new List<int>());
	}
}
