using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WarlordEngine.Events;

namespace WarlordEngine.Engine;

/// <summary>Context handed to card scripts for enter-play, ability, action and strike hooks.</summary>
public sealed class CardScriptContext
{
	public required GameState State { get; init; }

	public required CardDatabase CardDb { get; init; }

	/// <summary>The scripted card (the character entering play, ability source, played action, or the attacker).</summary>
	public CardInstance Card { get; init; }

	public CardDefinition? Definition { get; init; }

	/// <summary>Character performing the ability / action (or the attacker for strike hooks).</summary>
	public int PerformerId { get; init; }

	public int PlayerIndex { get; init; }

	public int Rank { get; init; }

	public int? TargetInstanceId { get; init; }

	public int PerformerLevel { get; init; }

	public PlayerState? ActingPlayer { get; init; }
}

/// <summary>Mutable accumulator that card passive scripts add their bonuses to.</summary>
public sealed class PassiveAccumulator
{
	public int Atk;

	public int Ac;

	public int Hp;

	public int Skill;

	public int ExtraWounds;
}

/// <summary>Context for continuous passive scripts evaluated by <see cref="GameEngine.GetContinuousPassiveBonuses(GameState, CardInstance, CombatContext?, CardDatabase?)"/>.</summary>
public sealed class PassiveContext
{
	public required GameState State { get; init; }

	public required CardDatabase CardDb { get; init; }

	public required CardInstance Character { get; init; }

	public CardDefinition? Definition { get; init; }

	public CombatContext? Combat { get; init; }

	public int CharLevel { get; init; }

	public bool IsWounded { get; init; }

	public int PlayerIndex { get; init; }

	public int Rank { get; init; }

	public int SlotIndex { get; init; }

	public required PassiveAccumulator Acc { get; init; }
}

/// <summary>Where a passive source sits relative to the character being evaluated.</summary>
public enum PassiveSourceRelation
{
	Adjacent,
	RankMate,
	RankMateItem,
	RankAny,
	RearRank,
	NearbyEnemy
}

/// <summary>Modular handler for a card whose rules text is bespoke. All hooks default to no-ops.</summary>
public abstract class CardScript
{
	/// <summary>Character entered a rank.</summary>
	public virtual void OnEnterPlay(CardScriptContext ctx) { }

	/// <summary>Character (or item bearer) activated an ability.</summary>
	public virtual void OnAbilityUsed(CardScriptContext ctx) { }

	/// <summary>Action card was played.</summary>
	public virtual void OnActionPlayed(CardScriptContext ctx) { }

	/// <summary>Attacker with this script landed a hit.</summary>
	public virtual void OnStrikeHit(CardScriptContext ctx) { }

	/// <summary>Attacker with this script killed its defender.</summary>
	public virtual IEnumerable<GameEvent> OnDefenderKilled(CardScriptContext ctx) => Array.Empty<GameEvent>();

	/// <summary>Item is never destroyed when its ability is used.</summary>
	public virtual bool PreservesSourceOnUse => false;

	/// <summary>Item is always destroyed when its ability is used.</summary>
	public virtual bool DestroysSourceOnUse => false;

	/// <summary>Ability uses the "lower level character of the same faction" targeting validation.</summary>
	public virtual bool UsesLowerLevelFactionTargeting => false;

	/// <summary>Ability uses the "adjacent character moves forward one rank" targeting validation.</summary>
	public virtual bool UsesAdjacentMoveForwardTargeting => false;

	/// <summary>Action may only target a spent or stunned character.</summary>
	public virtual bool RequiresSpentOrStunnedTarget => false;

	/// <summary>Bearer is treated as one level higher with the item's class when equipping (class errors ignored).</summary>
	public virtual bool BearerIgnoresClassWhenEquipping => false;

	/// <summary>Character must satisfy an end-of-turn survival condition (e.g. wounded an opponent) or die.</summary>
	public virtual bool RequiresEndOfTurnSurvivalCheck => false;

	/// <summary>Defender killed by this card is banished instead of sent to discard.</summary>
	public virtual bool BanishesOnKill => false;

	/// <summary>Passive bonuses from this card as the evaluated character, applied together with board relations.</summary>
	public virtual void ApplySelfPassive(PassiveContext ctx) { }

	/// <summary>Passive bonuses from this card as an item attached to the evaluated character. Return true to skip generic "+N Skill" text parsing.</summary>
	public virtual bool ApplyItemPassive(PassiveContext ctx, CardDefinition itemDef) => false;

	/// <summary>Passive bonuses applied after items, from this card as the evaluated character.</summary>
	public virtual void ApplyLatePassive(PassiveContext ctx) { }

	/// <summary>Passive bonuses granted by this card to the evaluated character from <paramref name="relation"/>.</summary>
	public virtual void ApplySourcePassive(PassiveContext ctx, PassiveSourceRelation relation, CardInstance source) { }

	/// <summary>Relations from which this card grants passive bonuses.</summary>
	public virtual PassiveSourceRelation[] SourceRelations => Array.Empty<PassiveSourceRelation>();

	/// <summary>When true a source fires at most once per relation scan (e.g. Adjacent), not once per matching card.</summary>
	public virtual bool SourceFiresOnce => false;
}

/// <summary>Registers bespoke card scripts by card name, replacing name branches scattered through the engine.</summary>
public static class CardScriptRegistry
{
	private static readonly Dictionary<string, CardScript> Scripts = Build();

	private static readonly List<(string Name, CardScript Script)> SourceScripts = Scripts
		.Where(kv => kv.Value.SourceRelations.Length > 0)
		.Select(kv => (kv.Key, kv.Value))
		.ToList();

	public static CardScript? Get(CardDefinition? def) => def != null && Scripts.TryGetValue(def.Name.Trim(), out var script) ? script : null;

	public static bool PreservesSourceOnUse(CardDefinition? def) => Get(def)?.PreservesSourceOnUse ?? false;

	public static bool DestroysSourceOnUse(CardDefinition? def) => Get(def)?.DestroysSourceOnUse ?? false;

	public static bool UsesLowerLevelFactionTargeting(CardDefinition? def) => Get(def)?.UsesLowerLevelFactionTargeting ?? false;

	public static bool UsesAdjacentMoveForwardTargeting(CardDefinition? def) => Get(def)?.UsesAdjacentMoveForwardTargeting ?? false;

	public static bool RequiresSpentOrStunnedTarget(CardDefinition? def) => Get(def)?.RequiresSpentOrStunnedTarget ?? false;

	public static bool BearerIgnoresClassWhenEquipping(CardDefinition? def) => Get(def)?.BearerIgnoresClassWhenEquipping ?? false;

	public static bool RequiresEndOfTurnSurvivalCheck(CardDefinition? def) => Get(def)?.RequiresEndOfTurnSurvivalCheck ?? false;

	public static bool BanishesOnKill(CardDefinition? def) => Get(def)?.BanishesOnKill ?? false;

	public static void OnEnterPlay(CardScriptContext ctx) => Get(ctx.Definition)?.OnEnterPlay(ctx);

	public static void OnAbilityUsed(CardScriptContext ctx) => Get(ctx.Definition)?.OnAbilityUsed(ctx);

	public static void OnActionPlayed(CardScriptContext ctx) => Get(ctx.Definition)?.OnActionPlayed(ctx);

	public static void OnStrikeHit(CardScriptContext ctx) => Get(ctx.Definition)?.OnStrikeHit(ctx);

	public static IEnumerable<GameEvent> OnDefenderKilled(CardScriptContext ctx) => Get(ctx.Definition)?.OnDefenderKilled(ctx) ?? Array.Empty<GameEvent>();

	/// <summary>Item passive stats that rules text alone cannot express (stats are handled by scripts instead).</summary>
	public static bool ApplyItemPassive(PassiveContext ctx, CardDefinition itemDef) => Get(itemDef)?.ApplyItemPassive(ctx, itemDef) ?? false;

	public static void ApplySelfPassive(PassiveContext ctx) => Get(ctx.Definition)?.ApplySelfPassive(ctx);

	public static void ApplyLatePassive(PassiveContext ctx) => Get(ctx.Definition)?.ApplyLatePassive(ctx);

	/// <summary>Evaluates board-relation passives (neighbors, rank mates, rear rank, nearby enemies) for the character in <paramref name="ctx"/>.</summary>
	public static void ApplyBoardPassives(PassiveContext ctx)
	{
		GameState state = ctx.State;
		List<CardInstance> rankCards = state.Players[ctx.PlayerIndex].Ranks[ctx.Rank];
		int slot = ctx.SlotIndex;
		foreach (var (name, script) in SourceScripts)
		{
			foreach (PassiveSourceRelation relation in script.SourceRelations)
			{
				bool fired = false;
				foreach (CardInstance source in EnumerateSources(ctx, rankCards, slot, relation))
				{
					if (fired && script.SourceFiresOnce)
					{
						break;
					}
					CardDefinition? sourceDef = relation == PassiveSourceRelation.RankMateItem ? null : ctx.CardDb.GetDefinition(source.DefinitionId);
					if (relation == PassiveSourceRelation.RankMateItem)
					{
						sourceDef = ctx.CardDb.GetDefinition(source.DefinitionId);
					}
					if (sourceDef == null || !sourceDef.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					int atk = ctx.Acc.Atk, ac = ctx.Acc.Ac, hp = ctx.Acc.Hp, skill = ctx.Acc.Skill;
					script.ApplySourcePassive(ctx, relation, source);
					fired = fired || ctx.Acc.Atk != atk || ctx.Acc.Ac != ac || ctx.Acc.Hp != hp || ctx.Acc.Skill != skill || script.SourceFiresOnce;
				}
			}
		}
	}

	private static IEnumerable<CardInstance> EnumerateSources(PassiveContext ctx, List<CardInstance> rankCards, int slot, PassiveSourceRelation relation)
	{
		GameState state = ctx.State;
		switch (relation)
		{
		case PassiveSourceRelation.Adjacent:
			if (slot > 0)
			{
				yield return rankCards[slot - 1];
			}
			if (slot + 1 < rankCards.Count)
			{
				yield return rankCards[slot + 1];
			}
			break;
		case PassiveSourceRelation.RankMate:
			for (int i = 0; i < rankCards.Count; i++)
			{
				if (i != slot)
				{
					yield return rankCards[i];
				}
			}
			break;
		case PassiveSourceRelation.RankMateItem:
			for (int i = 0; i < rankCards.Count; i++)
			{
				if (i == slot)
				{
					continue;
				}
				foreach (int itemId in rankCards[i].AttachedItemIds)
				{
					CardInstance? item = CardLocator.FindCard(state, itemId);
					if (item.HasValue)
					{
						yield return item.Value;
					}
				}
			}
			break;
		case PassiveSourceRelation.RankAny:
			foreach (CardInstance c in rankCards)
			{
				yield return c;
			}
			break;
		case PassiveSourceRelation.RearRank:
			if (state.Players[ctx.PlayerIndex].Ranks.TryGetValue(ctx.Rank + 1, out List<CardInstance>? rear))
			{
				foreach (CardInstance c in rear)
				{
					yield return c;
				}
			}
			break;
		case PassiveSourceRelation.NearbyEnemy:
			for (int opp = 0; opp < state.Players.Count; opp++)
			{
				if (opp == ctx.PlayerIndex)
				{
					continue;
				}
				foreach (var (oppRank, oppCards) in state.Players[opp].Ranks)
				{
					if (BoardGeometry.GetCrossBoardDistance(ctx.Rank, oppRank) > 1)
					{
						continue;
					}
					foreach (CardInstance c in oppCards)
					{
						yield return c;
					}
				}
			}
			break;
		}
	}

	private static Dictionary<string, CardScript> Build()
	{
		var map = new Dictionary<string, CardScript>(StringComparer.OrdinalIgnoreCase);
		void Add(string name, CardScript script) => map[name] = script;

		Add("Albrecht Novice", new AlbrechtNoviceScript());
		Add("Frameseer", new FrameseerScript());
		Add("Shannon the Daring", new ShannonScript());
		Add("Ciane", new CianeScript());
		Add("Stirges", new StirgesScript());
		Add("Mistress Allandra", new MistressAllandraScript());
		Add("Tunnel Worm", new TunnelWormScript());
		Add("Hod's Legacy", new HodsLegacyScript());
		Add("Hunting Staff", new HuntingStaffScript());
		Add("Strongarm Stout", new StrongarmStoutScript());
		Add("Tinker", new TinkerScript());
		Add("Ambitious Squire", new AmbitiousSquireScript());
		Add("Hazahk of Shadow", new HazahkScript());
		Add("Balian", new BalianScript());
		Add("Joleg", new JolegScript());
		Add("Makusog Steelhorn", new MakusogScript());
		Add("Tunnel Vanguard", new TunnelVanguardScript());
		Add("Breastplate of Power", new BreastplateOfPowerScript());
		Add("Dress", new DressScript());
		Add("Squire Argos", new SquireArgosScript());
		Add("Taumar the Protector", new TaumarScript());
		Add("Ghed Bane", new GhedBaneScript());
		Add("Shillelagh", new ShillelaghScript());
		Add("Gapfinder", new GapfinderScript());
		Add("Scale Mail", new ScaleMailScript());
		Add("Ardian Greathawk", new FlatSkillItemScript(3));
		Add("Unicorn of Thun", new FlatSkillItemScript(2));
		Add("'Mad' Pat Carrik", new MeleeAttackWoundScript());
		Add("Blitzer", new MeleeAttackWoundScript());
		Add("TFK Brigand", new TfkBrigandScript());
		Add("Grimfey Prowler", new GrimfeyProwlerScript());
		Add("Assassin's Strike", new AssassinsStrikeScript());
		Add("Cunning Shot", new CunningShotScript());
		Add("Dupe", new DupeScript());
		Add("Gift From Above", new LevelBuffActionScript(atk: true, ac: true));
		Add("Quick Strength", new LevelBuffActionScript(atk: true, ac: false));
		Add("Quick Shielding", new LevelBuffActionScript(atk: false, ac: true));
		Add("Even the Odds", new EvenTheOddsScript());
		Add("Steady Grip", new SimpleBuffActionScript("TRUE_STRIKE"));
		Add("Spiritual Weapon", new SimpleBuffActionScript("REACH"));
		Add("Leveled!", new LeveledScript());
		Add("Ac'Vuk", new AcVukScript());
		Add("Shambling Horror", new BanishOnKillScript());
		Add("Graveyard Librarian", new BanishOnKillScript());
		return map;
	}

	// ---- helpers -----------------------------------------------------------------------

	private static void BuffAdjacent(GameState state, List<CardInstance> rankCards, int slot)
	{
		if (slot > 0)
		{
			GameEngine.AddTurnBuff(state, rankCards[slot - 1].InstanceId, "ATK", 2);
			GameEngine.AddTurnBuff(state, rankCards[slot - 1].InstanceId, "AC", 2);
		}
		if (slot + 1 < rankCards.Count)
		{
			GameEngine.AddTurnBuff(state, rankCards[slot + 1].InstanceId, "ATK", 2);
			GameEngine.AddTurnBuff(state, rankCards[slot + 1].InstanceId, "AC", 2);
		}
	}

	private static void BuffLowLevelFreeKingdomsFrontRank(CardScriptContext ctx, int playerIndex)
	{
		if (!ctx.State.Players[playerIndex].Ranks.TryGetValue(1, out List<CardInstance>? rank1Cards))
		{
			return;
		}
		foreach (CardInstance ally in rank1Cards)
		{
			CardDefinition? allyDef = ctx.CardDb.GetDefinition(ally.DefinitionId);
			if (GameEngine.IsFreeKingdoms(allyDef) && GameEngine.GetEffectiveLevel(ctx.State, ally, ctx.CardDb) <= 2)
			{
				GameEngine.AddTurnBuff(ctx.State, ally.InstanceId, "ATK", 2);
				GameEngine.AddTurnBuff(ctx.State, ally.InstanceId, "AC", 2);
			}
		}
	}

	private static bool IsMeleeAttacking(CombatContext? c) => c != null && c.IsAttacking && c.IsMelee;

	private static bool IsOpponentSpent(CombatContext? c) => c?.OpponentCard?.ReadyState == ReadyState.Spent;

	// ---- enter-play / ability scripts ------------------------------------------------------

	private sealed class AlbrechtNoviceScript : CardScript
	{
		public override void OnEnterPlay(CardScriptContext ctx)
		{
			if (!ctx.State.Players[ctx.PlayerIndex].Ranks.TryGetValue(ctx.Rank, out List<CardInstance>? rankCards))
			{
				return;
			}
			int idx = rankCards.FindIndex(c => c.InstanceId == ctx.Card.InstanceId);
			if (idx >= 0)
			{
				BuffAdjacent(ctx.State, rankCards, idx);
			}
		}

		public override void OnAbilityUsed(CardScriptContext ctx)
		{
			var pos = GameEngine.FindCharacterPosition(ctx.State, ctx.PerformerId);
			if (pos.HasValue)
			{
				BuffAdjacent(ctx.State, ctx.State.Players[pos.Value.PlayerIndex].Ranks[pos.Value.Rank], pos.Value.Index);
			}
		}
	}

	private sealed class FrameseerScript : CardScript
	{
		public override void OnEnterPlay(CardScriptContext ctx) => GameEngine.AddTurnBuff(ctx.State, ctx.Card.InstanceId, "SURVEYED", 1);
	}

	private sealed class ShannonScript : CardScript
	{
		public override void OnEnterPlay(CardScriptContext ctx) => BuffLowLevelFreeKingdomsFrontRank(ctx, ctx.PlayerIndex);

		public override void OnAbilityUsed(CardScriptContext ctx)
		{
			var pos = GameEngine.FindCharacterPosition(ctx.State, ctx.PerformerId);
			if (pos.HasValue)
			{
				BuffLowLevelFreeKingdomsFrontRank(ctx, pos.Value.PlayerIndex);
			}
		}

		public override PassiveSourceRelation[] SourceRelations => new[] { PassiveSourceRelation.Adjacent };

		public override bool SourceFiresOnce => true;

		public override void ApplySourcePassive(PassiveContext ctx, PassiveSourceRelation relation, CardInstance source)
		{
			if (ctx.CharLevel <= 2)
			{
				ctx.Acc.Atk += 2;
			}
		}
	}

	private sealed class CianeScript : CardScript
	{
		public override void OnAbilityUsed(CardScriptContext ctx) => GameEngine.AddTurnBuff(ctx.State, ctx.Card.InstanceId, "CIANE_SELF_WOUND", 1);
	}

	private sealed class StirgesScript : CardScript
	{
		public override void OnStrikeHit(CardScriptContext ctx)
		{
			if (!GameEngine.HasTurnBuff(ctx.State, ctx.PerformerId, "STIRGES_BONUS_STRIKE"))
			{
				GameEngine.AddTurnBuff(ctx.State, ctx.PerformerId, "STIRGES_BONUS_STRIKE", 1);
			}
		}
	}

	private sealed class MistressAllandraScript : CardScript
	{
		public override IEnumerable<GameEvent> OnDefenderKilled(CardScriptContext ctx)
		{
			yield return new ChangeReadyStateEvent
			{
				TargetInstanceId = ctx.PerformerId,
				NewReadyState = ReadyState.Ready
			};
		}
	}

	private sealed class TunnelWormScript : CardScript
	{
		public override IEnumerable<GameEvent> OnDefenderKilled(CardScriptContext ctx)
		{
			var pos = GameEngine.FindCharacterPosition(ctx.State, ctx.PerformerId);
			if (!pos.HasValue)
			{
				yield break;
			}
			List<CardInstance> rank = ctx.State.Players[pos.Value.PlayerIndex].Ranks[pos.Value.Rank];
			if (rank.Count > 1)
			{
				yield return new CharacterMovedEvent
				{
					PlayerIndex = pos.Value.PlayerIndex,
					CardInstanceId = ctx.PerformerId,
					SourceRank = pos.Value.Rank,
					DestinationRank = pos.Value.Rank,
					TargetSlotIndex = pos.Value.Index == 0 ? rank.Count - 1 : 0
				};
			}
		}
	}

	private sealed class HodsLegacyScript : CardScript
	{
		public override bool PreservesSourceOnUse => true;

		public override bool UsesLowerLevelFactionTargeting => true;
	}

	private sealed class HuntingStaffScript : CardScript
	{
		public override bool PreservesSourceOnUse => true;

		public override bool UsesAdjacentMoveForwardTargeting => true;
	}

	private sealed class StrongarmStoutScript : CardScript
	{
		public override bool DestroysSourceOnUse => true;
	}

	private sealed class TinkerScript : CardScript
	{
		public override bool BearerIgnoresClassWhenEquipping => true;
	}

	// ---- continuous passive scripts ----------------------------------------------------------

	private sealed class AmbitiousSquireScript : CardScript
	{
		public override void ApplySelfPassive(PassiveContext ctx)
		{
			List<CardInstance> rank = ctx.State.Players[ctx.PlayerIndex].Ranks[ctx.Rank];
			foreach (int i in new[] { ctx.SlotIndex - 1, ctx.SlotIndex + 1 })
			{
				if (i < 0 || i >= rank.Count)
				{
					continue;
				}
				CardInstance neighbor = rank[i];
				CardDefinition? neighborDef = ctx.CardDb.GetDefinition(neighbor.DefinitionId);
				int neighborLevel = (neighborDef?.Level ?? 0) + (neighbor.SkillRatings.TryGetValue("BonusLevel", out var bonus) ? bonus : 0);
				if (GameEngine.IsDeverenian(neighborDef) && neighborLevel > ctx.CharLevel)
				{
					ctx.Acc.Atk += 2;
					ctx.Acc.Ac += 2;
				}
			}
		}
	}

	private sealed class HazahkScript : CardScript
	{
		private static bool IsWoundedChar(PassiveContext ctx) => ctx.Character.Wounds > 0 || ctx.IsWounded;

		public override void ApplySelfPassive(PassiveContext ctx)
		{
			if (IsWoundedChar(ctx))
			{
				ctx.Acc.Atk += 5;
			}
		}

		public override PassiveSourceRelation[] SourceRelations => new[] { PassiveSourceRelation.Adjacent };

		public override bool SourceFiresOnce => true;

		public override void ApplySourcePassive(PassiveContext ctx, PassiveSourceRelation relation, CardInstance source)
		{
			bool selfIsHazahk = ctx.Definition != null && ctx.Definition.Name.Equals("Hazahk of Shadow", StringComparison.OrdinalIgnoreCase);
			if (!selfIsHazahk && IsWoundedChar(ctx))
			{
				ctx.Acc.Atk += 5;
			}
		}
	}

	private sealed class BalianScript : CardScript
	{
		public override PassiveSourceRelation[] SourceRelations => new[] { PassiveSourceRelation.Adjacent };

		public override bool SourceFiresOnce => true;

		public override void ApplySourcePassive(PassiveContext ctx, PassiveSourceRelation relation, CardInstance source)
		{
			if (GameEngine.IsMercenary(ctx.Definition))
			{
				ctx.Acc.Ac += 2;
			}
		}
	}

	private sealed class JolegScript : CardScript
	{
		public override PassiveSourceRelation[] SourceRelations => new[] { PassiveSourceRelation.Adjacent };

		public override bool SourceFiresOnce => true;

		public override void ApplySourcePassive(PassiveContext ctx, PassiveSourceRelation relation, CardInstance source)
		{
			if (GameEngine.IsNothrog(ctx.Definition))
			{
				ctx.Acc.Atk += 2;
			}
		}
	}

	private sealed class MakusogScript : CardScript
	{
		public override PassiveSourceRelation[] SourceRelations => new[] { PassiveSourceRelation.Adjacent };

		public override bool SourceFiresOnce => true;

		public override void ApplySourcePassive(PassiveContext ctx, PassiveSourceRelation relation, CardInstance source)
		{
			if (GameEngine.IsSiege(ctx.Definition))
			{
				ctx.Acc.Atk += 2;
			}
		}
	}

	private sealed class TunnelVanguardScript : CardScript
	{
		public override PassiveSourceRelation[] SourceRelations => new[] { PassiveSourceRelation.Adjacent };

		public override void ApplySourcePassive(PassiveContext ctx, PassiveSourceRelation relation, CardInstance source)
		{
			if (GameEngine.IsDwarf(ctx.Definition))
			{
				ctx.Acc.Hp++;
			}
		}
	}

	private sealed class BreastplateOfPowerScript : CardScript
	{
		public override PassiveSourceRelation[] SourceRelations => new[] { PassiveSourceRelation.RankMateItem };

		public override void ApplySourcePassive(PassiveContext ctx, PassiveSourceRelation relation, CardInstance source)
		{
			ctx.Acc.Atk += 2;
			ctx.Acc.Ac += 2;
		}
	}

	private sealed class DressScript : CardScript
	{
		public override PassiveSourceRelation[] SourceRelations => new[] { PassiveSourceRelation.RankAny };

		public override bool SourceFiresOnce => true;

		public override void ApplySourcePassive(PassiveContext ctx, PassiveSourceRelation relation, CardInstance source)
		{
			if (GameEngine.IsDwarf(ctx.Definition) && ctx.IsWounded)
			{
				ctx.Acc.Ac += 3;
			}
		}
	}

	private sealed class SquireArgosScript : CardScript
	{
		public override PassiveSourceRelation[] SourceRelations => new[] { PassiveSourceRelation.RankMate };

		public override void ApplySourcePassive(PassiveContext ctx, PassiveSourceRelation relation, CardInstance source)
		{
			if (!GameEngine.IsFreeKingdoms(ctx.Definition))
			{
				return;
			}
			CardDefinition? sourceDef = ctx.CardDb.GetDefinition(source.DefinitionId);
			int argosLevel = (sourceDef?.Level ?? 0) + (source.SkillRatings.TryGetValue("BonusLevel", out var bonus) ? bonus : 0);
			if (ctx.CharLevel <= argosLevel)
			{
				ctx.Acc.Atk += 2;
				ctx.Acc.Ac++;
			}
		}
	}

	private sealed class TaumarScript : CardScript
	{
		public override PassiveSourceRelation[] SourceRelations => new[] { PassiveSourceRelation.RearRank };

		public override bool SourceFiresOnce => true;

		public override void ApplySourcePassive(PassiveContext ctx, PassiveSourceRelation relation, CardInstance source)
		{
			if (GameEngine.IsDeverenian(ctx.Definition) && GameEngine.IsFighter(ctx.Definition))
			{
				ctx.Acc.Ac += 2;
				ctx.Acc.Skill += 2;
			}
		}
	}

	private sealed class GhedBaneScript : CardScript
	{
		public override PassiveSourceRelation[] SourceRelations => new[] { PassiveSourceRelation.NearbyEnemy };

		public override void ApplySourcePassive(PassiveContext ctx, PassiveSourceRelation relation, CardInstance source)
		{
			if (GameEngine.GetEffectiveLevel(ctx.State, ctx.Character, ctx.CardDb) < GameEngine.GetEffectiveLevel(ctx.State, source, ctx.CardDb))
			{
				ctx.Acc.Atk -= 2;
				ctx.Acc.Ac -= 2;
			}
		}
	}

	private sealed class ShillelaghScript : CardScript
	{
		public override bool ApplyItemPassive(PassiveContext ctx, CardDefinition itemDef)
		{
			int effectiveSkill = ctx.Character.CurrentSkill + ctx.Acc.Skill;
			ctx.Acc.Atk += Math.Max(0, (effectiveSkill + 1) / 2);
			return false;
		}
	}

	private sealed class GapfinderScript : CardScript
	{
		public override bool ApplyItemPassive(PassiveContext ctx, CardDefinition itemDef)
		{
			if (IsMeleeAttacking(ctx.Combat) && IsOpponentSpent(ctx.Combat))
			{
				ctx.Acc.Atk += 5;
			}
			return false;
		}
	}

	private sealed class ScaleMailScript : CardScript
	{
		public override bool ApplyItemPassive(PassiveContext ctx, CardDefinition itemDef)
		{
			if (ctx.Combat != null && ctx.Combat.IsDefending && ctx.Combat.IsRanged)
			{
				ctx.Acc.Ac += 2;
			}
			return false;
		}
	}

	private sealed class FlatSkillItemScript(int skill) : CardScript
	{
		public override bool ApplyItemPassive(PassiveContext ctx, CardDefinition itemDef)
		{
			ctx.Acc.Skill += skill;
			return true;
		}
	}

	private sealed class MeleeAttackWoundScript : CardScript
	{
		public override void ApplyLatePassive(PassiveContext ctx)
		{
			if (IsMeleeAttacking(ctx.Combat))
			{
				ctx.Acc.ExtraWounds++;
			}
		}
	}

	private sealed class TfkBrigandScript : CardScript
	{
		public override void ApplyLatePassive(PassiveContext ctx)
		{
			if (ctx.Combat != null && ctx.Combat.IsDefending)
			{
				ctx.Acc.ExtraWounds++;
			}
		}
	}

	private sealed class GrimfeyProwlerScript : CardScript
	{
		public override void ApplyLatePassive(PassiveContext ctx)
		{
			if (IsMeleeAttacking(ctx.Combat) && IsOpponentSpent(ctx.Combat))
			{
				ctx.Acc.Atk += ctx.Character.CurrentSkill + ctx.Acc.Skill;
			}
		}
	}

	// ---- action card scripts -------------------------------------------------------------------

	private sealed class AssassinsStrikeScript : CardScript
	{
		public override bool RequiresSpentOrStunnedTarget => true;

		public override void OnActionPlayed(CardScriptContext ctx)
		{
			GameEngine.AddTurnBuff(ctx.State, ctx.PerformerId, "ADVANTAGE", 1);
			GameEngine.AddTurnBuff(ctx.State, ctx.PerformerId, "WOUND_BONUS", 1);
		}
	}

	private sealed class CunningShotScript : CardScript
	{
		public override void OnActionPlayed(CardScriptContext ctx) => GameEngine.AddTurnBuff(ctx.State, ctx.PerformerId, "CUNNING_SHOT_PULL", 1);
	}

	private sealed class DupeScript : CardScript
	{
		public override void OnActionPlayed(CardScriptContext ctx)
		{
			var pos = GameEngine.FindCharacterPosition(ctx.State, ctx.PerformerId);
			if (!pos.HasValue)
			{
				return;
			}
			List<CardInstance> rank = ctx.State.Players[pos.Value.PlayerIndex].Ranks[pos.Value.Rank];
			if (rank.Count > 1)
			{
				int oldIdx = pos.Value.Index;
				int newIdx = (oldIdx + 1) % rank.Count;
				CardInstance moved = rank[oldIdx];
				rank.RemoveAt(oldIdx);
				rank.Insert(newIdx, moved);
				GameEngine.AddTurnBuff(ctx.State, ctx.PerformerId, "DUPE_ACTIVE", 1);
			}
		}
	}

	private sealed class LevelBuffActionScript(bool atk, bool ac) : CardScript
	{
		public override void OnActionPlayed(CardScriptContext ctx)
		{
			int target = ctx.TargetInstanceId ?? ctx.PerformerId;
			if (atk)
			{
				GameEngine.AddTurnBuff(ctx.State, target, "ATK", ctx.PerformerLevel);
			}
			if (ac)
			{
				GameEngine.AddTurnBuff(ctx.State, target, "AC", ctx.PerformerLevel);
			}
		}
	}

	private sealed class EvenTheOddsScript : CardScript
	{
		public override void OnActionPlayed(CardScriptContext ctx)
		{
			int nearby = 0;
			var pos = GameEngine.FindCharacterPosition(ctx.State, ctx.PerformerId);
			if (pos.HasValue)
			{
				for (int opp = 0; opp < ctx.State.Players.Count; opp++)
				{
					if (opp == ctx.PlayerIndex)
					{
						continue;
					}
					foreach (var (oppRank, oppCards) in ctx.State.Players[opp].Ranks)
					{
						if (BoardGeometry.GetCrossBoardDistance(pos.Value.Rank, oppRank) <= 1)
						{
							nearby += oppCards.Count;
						}
					}
				}
			}
			GameEngine.AddTurnBuff(ctx.State, ctx.PerformerId, "ATK", nearby);
			GameEngine.AddTurnBuff(ctx.State, ctx.PerformerId, "AC", nearby);
			if (nearby >= 5)
			{
				GameEngine.AddTurnBuff(ctx.State, ctx.PerformerId, "WARD", 1);
			}
		}
	}

	private sealed class SimpleBuffActionScript(string buff) : CardScript
	{
		public override void OnActionPlayed(CardScriptContext ctx) => GameEngine.AddTurnBuff(ctx.State, ctx.PerformerId, buff, 1);
	}

	private sealed class LeveledScript : CardScript
	{
		public override void OnActionPlayed(CardScriptContext ctx)
		{
			PlayerState player = ctx.ActingPlayer!;
			player.DiscardPile.RemoveAll(c => c.InstanceId == ctx.Card.InstanceId);
			player.BanishPile.Add(ctx.Card with { Zone = CardZone.Banished });
		}
	}

	private sealed class AcVukScript : CardScript
	{
		public override bool RequiresEndOfTurnSurvivalCheck => true;
	}

	private sealed class BanishOnKillScript : CardScript
	{
		public override bool BanishesOnKill => true;
	}
}
