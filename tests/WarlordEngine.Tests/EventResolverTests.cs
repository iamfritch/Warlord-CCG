using System.Collections.Generic;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class EventResolverTests
{
	[Fact]
	public void Cancelled_DealDamage_Does_Not_Reduce_HP()
	{
		(GameState State, CardInstance Card) tuple = TestStateFactory.CreateWithOneCharacterOnBoard();
		GameState item = tuple.State;
		CardInstance item2 = tuple.Card;
		EventResolver eventResolver = new EventResolver();
		eventResolver.RegisterInterceptor(new CancelAllDamageInterceptor());
		eventResolver.Push(new DealDamageEvent
		{
			TargetInstanceId = item2.InstanceId,
			Amount = 5,
			DamageType = "melee"
		});
		ResolveResult resolveResult = eventResolver.Resolve(item);
		Assert.NotNull(resolveResult);
		Assert.Equal(ResolutionStatus.Cancelled, resolveResult.Status);
		Assert.False(resolveResult.WasApplied);
		Assert.Equal(10, item.Players[0].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void Unintercepted_DealDamage_Reduces_HP()
	{
		(GameState State, CardInstance Card) tuple = TestStateFactory.CreateWithOneCharacterOnBoard();
		GameState item = tuple.State;
		CardInstance item2 = tuple.Card;
		EventResolver eventResolver = new EventResolver();
		eventResolver.Push(new DealDamageEvent
		{
			TargetInstanceId = item2.InstanceId,
			Amount = 3,
			DamageType = "melee"
		});
		ResolveResult resolveResult = eventResolver.Resolve(item);
		Assert.NotNull(resolveResult);
		Assert.Equal(ResolutionStatus.Allowed, resolveResult.Status);
		Assert.True(resolveResult.WasApplied);
		Assert.Equal(7, item.Players[0].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void Modified_DealDamage_Applies_Reduced_Amount()
	{
		(GameState State, CardInstance Card) tuple = TestStateFactory.CreateWithOneCharacterOnBoard();
		GameState item = tuple.State;
		CardInstance item2 = tuple.Card;
		EventResolver eventResolver = new EventResolver();
		eventResolver.RegisterInterceptor(new HalveDamageInterceptor());
		eventResolver.Push(new DealDamageEvent
		{
			TargetInstanceId = item2.InstanceId,
			Amount = 6,
			DamageType = "melee"
		});
		ResolveResult resolveResult = eventResolver.Resolve(item);
		Assert.NotNull(resolveResult);
		Assert.Equal(ResolutionStatus.Modified, resolveResult.Status);
		Assert.True(resolveResult.WasApplied);
		DealDamageEvent dealDamageEvent = Assert.IsType<DealDamageEvent>(resolveResult.FinalEvent);
		Assert.Equal(3, dealDamageEvent.Amount);
		Assert.Equal(7, item.Players[0].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void Modified_Event_Is_New_Instance_Not_Mutated_Original()
	{
		DealDamageEvent dealDamageEvent = new DealDamageEvent
		{
			TargetInstanceId = 1,
			Amount = 10,
			DamageType = "melee"
		};
		HalveDamageInterceptor halveDamageInterceptor = new HalveDamageInterceptor();
		GameState item = TestStateFactory.CreateWithOneCharacterOnBoard().State;
		EventResolutionResult eventResolutionResult = halveDamageInterceptor.Intercept(dealDamageEvent, item);
		Assert.Equal(ResolutionStatus.Modified, eventResolutionResult.Status);
		DealDamageEvent dealDamageEvent2 = Assert.IsType<DealDamageEvent>(eventResolutionResult.ModifiedEvent);
		Assert.Equal(10, dealDamageEvent.Amount);
		Assert.Equal(5, dealDamageEvent2.Amount);
		Assert.Equal(dealDamageEvent.EventId, dealDamageEvent2.EventId);
	}

	[Fact]
	public void ResolveAll_Processes_Events_In_LIFO_Order()
	{
		GameState item = TestStateFactory.CreateWithOneCharacterOnBoard(20).State;
		EventResolver eventResolver = new EventResolver();
		DealDamageEvent gameEvent = new DealDamageEvent
		{
			TargetInstanceId = 1,
			Amount = 3,
			DamageType = "melee"
		};
		DealDamageEvent gameEvent2 = new DealDamageEvent
		{
			TargetInstanceId = 1,
			Amount = 7,
			DamageType = "ranged"
		};
		eventResolver.Push(gameEvent);
		eventResolver.Push(gameEvent2);
		List<ResolveResult> list = eventResolver.ResolveAll(item);
		Assert.Equal(2, list.Count);
		DealDamageEvent dealDamageEvent = Assert.IsType<DealDamageEvent>(list[0].FinalEvent);
		Assert.Equal(7, dealDamageEvent.Amount);
		DealDamageEvent dealDamageEvent2 = Assert.IsType<DealDamageEvent>(list[1].FinalEvent);
		Assert.Equal(3, dealDamageEvent2.Amount);
		Assert.Equal(10, item.Players[0].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void Cancel_Interceptor_Short_Circuits_Chain()
	{
		GameState item = TestStateFactory.CreateWithOneCharacterOnBoard().State;
		CancelAllDamageInterceptor interceptor = new CancelAllDamageInterceptor();
		AllowAllInterceptor allowAllInterceptor = new AllowAllInterceptor();
		EventResolver eventResolver = new EventResolver();
		eventResolver.RegisterInterceptor(interceptor);
		eventResolver.RegisterInterceptor(allowAllInterceptor);
		eventResolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 1,
			Amount = 5,
			DamageType = "melee"
		});
		eventResolver.Resolve(item);
		Assert.Equal(0, allowAllInterceptor.CallCount);
	}

	[Fact]
	public void Resolve_On_Empty_Stack_Returns_Null()
	{
		GameState item = TestStateFactory.CreateWithOneCharacterOnBoard().State;
		EventResolver eventResolver = new EventResolver();
		ResolveResult resolveResult = eventResolver.Resolve(item);
		Assert.Null(resolveResult);
	}

	[Fact]
	public void PhaseChangeEvent_Updates_CurrentPhase()
	{
		GameState item = TestStateFactory.CreateWithOneCharacterOnBoard().State;
		item.CurrentPhase = TurnPhase.Main;
		EventResolver eventResolver = new EventResolver();
		eventResolver.Push(new PhaseChangeEvent
		{
			FromPhase = TurnPhase.Main,
			ToPhase = TurnPhase.Combat
		});
		eventResolver.Resolve(item);
		Assert.Equal(TurnPhase.Combat, item.CurrentPhase);
	}

	[Fact]
	public void DeclareAttackEvent_Spends_Attacker()
	{
		var (gameState, cardInstance) = TestStateFactory.CreateWithOneCharacterOnBoard();
		Assert.Equal(ReadyState.Ready, gameState.Players[0].Ranks[1][0].ReadyState);
		EventResolver eventResolver = new EventResolver();
		eventResolver.Push(new DeclareAttackEvent
		{
			AttackerInstanceId = cardInstance.InstanceId,
			DefenderInstanceId = 99
		});
		eventResolver.Resolve(gameState);
		Assert.Equal(ReadyState.Spent, gameState.Players[0].Ranks[1][0].ReadyState);
	}

	[Fact]
	public void Unregister_Interceptor_Removes_It_From_Chain()
	{
		GameState item = TestStateFactory.CreateWithOneCharacterOnBoard().State;
		CancelAllDamageInterceptor interceptor = new CancelAllDamageInterceptor();
		EventResolver eventResolver = new EventResolver();
		eventResolver.RegisterInterceptor(interceptor);
		bool condition = eventResolver.UnregisterInterceptor(interceptor);
		Assert.True(condition);
		eventResolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 1,
			Amount = 5,
			DamageType = "melee"
		});
		ResolveResult resolveResult = eventResolver.Resolve(item);
		Assert.NotNull(resolveResult);
		Assert.True(resolveResult.WasApplied);
		Assert.Equal(5, item.Players[0].Ranks[1][0].CurrentHitPoints);
	}
}
