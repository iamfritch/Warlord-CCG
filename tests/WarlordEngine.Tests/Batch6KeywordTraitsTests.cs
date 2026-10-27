using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class Batch6KeywordTraitsTests
{
    private readonly CardDatabase _cardDb;

    public Batch6KeywordTraitsTests()
    {
        _cardDb = DatabaseLoader.LoadFromFile(DataPaths.CardsJsonPath);
    }

    private static PlayerState CreatePlayer(int index, string name)
    {
        return new PlayerState
        {
            PlayerIndex = index,
            DisplayName = name,
            IsSimulation = true,
            Hand = new List<CardInstance>(),
            Deck = new List<CardInstance>(),
            DiscardPile = new List<CardInstance>(),
            BanishPile = new List<CardInstance>(),
            AttachedItems = new List<CardInstance>(),
            Ranks = new SortedDictionary<int, List<CardInstance>>
            {
                [1] = new List<CardInstance>(),
                [2] = new List<CardInstance>(),
                [3] = new List<CardInstance>()
            }
        };
    }

    private static GameState CreateOrderState(PlayerState p0, PlayerState p1)
    {
        return new GameState
        {
            Players = new List<PlayerState> { p0, p1 },
            ActivePlayerIndex = 0,
            TurnNumber = 1,
            CurrentPhase = TurnPhase.Order,
            TurnState = new TurnState { CurrentPhase = TurnPhase.Order },
            Priority = new PriorityState { ActivePlayerIndex = 0, IsReactWindowOpen = false },
            IsSimulation = true,
            AutoPassWhenNoReactsAvailable = true
        };
    }

    private static CardInstance MakeChar(CardDefinition def, int id, int owner, int rank)
    {
        return new CardInstance
        {
            InstanceId = id,
            DefinitionId = def.DefinitionId,
            OwnerPlayerIndex = owner,
            Rank = rank,
            Zone = CardZone.Board,
            ReadyState = ReadyState.Ready,
            CurrentHitPoints = def.BaseHitPoints ?? 3,
            CurrentArmorClass = def.ArmorClass ?? 12,
            CurrentSkill = def.Skill ?? def.Level,
            SkillRatings = new Dictionary<string, int>(def.SkillRatings, StringComparer.OrdinalIgnoreCase),
            AttachedItemIds = new List<int>()
        };
    }

    [Fact]
    public void Stealth_DetectionAndProtectionCheck()
    {
        CardDefinition stealthDef = new CardDefinition
        {
            DefinitionId = "test_stealth_char",
            Name = "Shadow Stalker",
            Type = CardType.Character,
            Level = 2,
            BaseHitPoints = 3,
            ArmorClass = 12,
            Traits = new List<string> { "Stealth" },
            RulesText = "Stealth",
            SkillRatings = new Dictionary<string, int> { { "Stealth", 1 } }
        };

        var p0 = CreatePlayer(0, "P0");
        var p1 = CreatePlayer(1, "P1");
        var stealthChar = MakeChar(stealthDef, 101, 1, 1);
        p1.Ranks[1].Add(stealthChar);

        var state = CreateOrderState(p0, p1);
        var engine = new GameEngine(state, _cardDb);

        Assert.True(RulesParser.HasStealth(stealthDef));
        Assert.Equal(1, RulesParser.GetStealthRating(stealthDef));
        Assert.True(GameEngine.CharacterHasStealth(stealthChar, state, _cardDb));

        var attacker = MakeChar(new CardDefinition { DefinitionId = "atk", Name = "Attacker", Type = CardType.Character, Level = 2, BaseHitPoints = 3, SkillRatings = new Dictionary<string, int>() }, 102, 0, 1);
        p0.Ranks[1].Add(attacker);

        bool isProtectedRanged = GameEngine.IsTargetProtectedByStealth(state, attacker, stealthChar, isRanged: true, _cardDb);
        Assert.True(isProtectedRanged);
    }

    [Fact]
    public void Riposte_TraitDetectionAndRating()
    {
        CardDefinition riposteDef = new CardDefinition
        {
            DefinitionId = "test_riposte_char",
            Name = "Master Fencer",
            Type = CardType.Character,
            Level = 3,
            BaseHitPoints = 3,
            ArmorClass = 13,
            Traits = new List<string> { "Riposte +2" },
            RulesText = "Riposte +2",
            SkillRatings = new Dictionary<string, int> { { "Riposte", 2 } }
        };

        var p0 = CreatePlayer(0, "P0");
        var p1 = CreatePlayer(1, "P1");
        var ripChar = MakeChar(riposteDef, 201, 1, 1);
        p1.Ranks[1].Add(ripChar);

        var state = CreateOrderState(p0, p1);

        Assert.True(RulesParser.HasRiposte(riposteDef));
        Assert.Equal(2, RulesParser.GetRiposteRating(riposteDef));
        Assert.True(GameEngine.CharacterHasRiposte(ripChar, state, _cardDb));
    }

    [Fact]
    public void MagicResistance_TraitDetectionAndMitigation()
    {
        CardDefinition mrDef = new CardDefinition
        {
            DefinitionId = "test_mr_char",
            Name = "Runebound Warder",
            Type = CardType.Character,
            Level = 3,
            BaseHitPoints = 4,
            ArmorClass = 12,
            Traits = new List<string> { "Magic Resistance 2" },
            RulesText = "Magic Resistance 2",
            SkillRatings = new Dictionary<string, int> { { "Magic Resistance", 2 } }
        };

        var p0 = CreatePlayer(0, "P0");
        var p1 = CreatePlayer(1, "P1");
        var mrChar = MakeChar(mrDef, 301, 1, 1);
        p1.Ranks[1].Add(mrChar);

        var state = CreateOrderState(p0, p1);

        Assert.True(RulesParser.HasMagicResistance(mrDef));
        Assert.Equal(2, RulesParser.GetMagicResistanceRating(mrDef));
        Assert.True(GameEngine.CharacterHasMagicResistance(mrChar, state, _cardDb));
    }
}