using System.Collections.Generic;
using WarlordEngine.Actions;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class DeckManagementTests
{
	private static CardDatabase CreateTestCardDb()
	{
		return new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["warlord-hero"] = new CardDefinition
				{
					DefinitionId = "warlord-hero",
					Name = "Warlord Hero",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 3,
					ArmorClass = 15,
					BaseHitPoints = 12,
					IsWarlord = true
				},
				["level-1-troop"] = new CardDefinition
				{
					DefinitionId = "level-1-troop",
					Name = "Foot Soldier",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 12,
					BaseHitPoints = 6
				}
			}
		};
	}

	[Fact]
	public void Deck_Validate_Returns_Success_For_Valid_Deck()
	{
		CardDatabase cardDb = CreateTestCardDb();
		Deck deck = new Deck
		{
			DeckName = "Vanguard Deck",
			WarlordDefinitionId = "warlord-hero",
			CardDefinitionIds = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "level-1-troop", "level-1-troop" })
		};
		ActionResult actionResult = deck.Validate(cardDb, 2);
		Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
	}

	[Fact]
	public void Deck_Validate_Returns_Error_When_Warlord_Missing_Or_Non_Warlord()
	{
		CardDatabase cardDb = CreateTestCardDb();
		Deck deck = new Deck
		{
			DeckName = "Invalid Deck",
			WarlordDefinitionId = "level-1-troop",
			CardDefinitionIds = new _003C_003Ez__ReadOnlySingleElementList<string>("level-1-troop")
		};
		ActionResult actionResult = deck.Validate(cardDb, 1);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("not a Warlord"));
	}

	[Fact]
	public void Deck_Validate_Returns_Error_When_Deck_Size_Too_Small()
	{
		CardDatabase cardDb = CreateTestCardDb();
		Deck deck = new Deck
		{
			DeckName = "Tiny Deck",
			WarlordDefinitionId = "warlord-hero",
			CardDefinitionIds = new _003C_003Ez__ReadOnlySingleElementList<string>("level-1-troop")
		};
		ActionResult actionResult = deck.Validate(cardDb, 5);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("below the minimum required size"));
	}

	[Fact]
	public void GameEngine_CreateFromDecks_Populates_Player_State_And_Hands()
	{
		CardDatabase cardDb = CreateTestCardDb();
		Deck deck = new Deck
		{
			DeckName = "Player 1 Vanguard",
			WarlordDefinitionId = "warlord-hero",
			CardDefinitionIds = new _003C_003Ez__ReadOnlySingleElementList<string>("level-1-troop")
		};
		Deck deck2 = new Deck
		{
			DeckName = "Player 2 Vanguard",
			WarlordDefinitionId = "warlord-hero",
			CardDefinitionIds = new _003C_003Ez__ReadOnlySingleElementList<string>("level-1-troop")
		};
		GameEngine gameEngine = GameEngine.CreateFromDecks(new _003C_003Ez__ReadOnlyArray<Deck>(new Deck[2] { deck, deck2 }), cardDb);
		Assert.NotNull(gameEngine);
		Assert.Equal(2, gameEngine.State.Players.Count);
		Assert.Equal(TurnPhase.Setup, gameEngine.State.CurrentPhase);
		Assert.Equal("Player 1 (Player 1 Vanguard)", gameEngine.State.Players[0].DisplayName);
		Assert.Equal(2, gameEngine.State.Players[0].Hand.Count);
	}
}
