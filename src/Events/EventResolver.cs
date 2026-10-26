using System;
using System.Collections.Generic;
using System.Linq;

namespace WarlordEngine.Events;

public sealed class EventResolver
{
	private readonly Stack<GameEvent> _eventStack = new Stack<GameEvent>();

	private readonly List<IEventInterceptor> _interceptors = new List<IEventInterceptor>();

	private readonly List<IPostResolutionHandler> _postHandlers = new List<IPostResolutionHandler>();

	public int PendingCount => _eventStack.Count;

	public IReadOnlyCollection<GameEvent> PendingEvents => _eventStack;

	public int InterceptorCount => _interceptors.Count;

	public void Push(GameEvent gameEvent)
	{
		ArgumentNullException.ThrowIfNull(gameEvent, "gameEvent");
		_eventStack.Push(gameEvent);
	}

	public GameEvent? Peek()
	{
		if (_eventStack.Count <= 0)
		{
			return null;
		}
		return _eventStack.Peek();
	}

	public void RestoreStack(IEnumerable<GameEvent> eventsTopToBottom)
	{
		ArgumentNullException.ThrowIfNull(eventsTopToBottom, "eventsTopToBottom");
		List<GameEvent> list = eventsTopToBottom.ToList();
		_eventStack.Clear();
		for (int num = list.Count - 1; num >= 0; num--)
		{
			_eventStack.Push(list[num]);
		}
	}

	public static bool RequiresReactWindow(GameEvent gameEvent)
	{
		if (gameEvent is DeclareAttackEvent || gameEvent is AbilityActivatedEvent || gameEvent is MarchEvent || gameEvent is DealDamageEvent)
		{
			return true;
		}
		return false;
	}

	public void RegisterInterceptor(IEventInterceptor interceptor)
	{
		ArgumentNullException.ThrowIfNull(interceptor, "interceptor");
		_interceptors.Add(interceptor);
	}

	public bool UnregisterInterceptor(IEventInterceptor interceptor)
	{
		return _interceptors.Remove(interceptor);
	}

	public void RegisterPostHandler(IPostResolutionHandler handler)
	{
		ArgumentNullException.ThrowIfNull(handler, "handler");
		_postHandlers.Add(handler);
	}

	public bool UnregisterPostHandler(IPostResolutionHandler handler)
	{
		return _postHandlers.Remove(handler);
	}

	public ResolveResult? Resolve(GameState state)
	{
		ArgumentNullException.ThrowIfNull(state, "state");
		if (_eventStack.Count == 0)
		{
			return null;
		}
		GameEvent gameEvent = _eventStack.Pop();
		ResolutionStatus status = ResolutionStatus.Allowed;
		foreach (IEventInterceptor interceptor in _interceptors)
		{
			EventResolutionResult eventResolutionResult = interceptor.Intercept(gameEvent, state);
			switch (eventResolutionResult.Status)
			{
			case ResolutionStatus.Cancelled:
				return new ResolveResult
				{
					OriginalEvent = gameEvent,
					FinalEvent = gameEvent,
					Status = ResolutionStatus.Cancelled,
					WasApplied = false
				};
			case ResolutionStatus.Modified:
				gameEvent = eventResolutionResult.ModifiedEvent ?? throw new InvalidOperationException("Interceptor " + interceptor.GetType().Name + " returned Modified status but ModifiedEvent was null.");
				status = ResolutionStatus.Modified;
				break;
			}
		}
		gameEvent.ApplyToState(state);
		foreach (IPostResolutionHandler postHandler in _postHandlers)
		{
			foreach (GameEvent item in postHandler.AfterResolve(gameEvent, state))
			{
				_eventStack.Push(item);
			}
		}
		return new ResolveResult
		{
			OriginalEvent = gameEvent,
			FinalEvent = gameEvent,
			Status = status,
			WasApplied = true
		};
	}

	public List<ResolveResult> ResolveAll(GameState state)
	{
		List<ResolveResult> list = new List<ResolveResult>();
		while (_eventStack.Count > 0)
		{
			ResolveResult resolveResult = Resolve(state);
			if ((object)resolveResult != null)
			{
				list.Add(resolveResult);
			}
		}
		return list;
	}

	public void ClearStack()
	{
		_eventStack.Clear();
	}
}
