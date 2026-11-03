using System;
using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Actions;

public sealed record ActionResult
{
	public bool IsSuccess { get; init; }

	public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

	public IReadOnlyList<GameEvent> ResolvedEvents { get; init; } = Array.Empty<GameEvent>();

	public static ActionResult Success(IReadOnlyList<GameEvent>? resolvedEvents = null)
	{
		return new ActionResult
		{
			IsSuccess = true,
			ResolvedEvents = resolvedEvents ?? Array.Empty<GameEvent>()
		};
	}

	public static ActionResult Failure(params string[] errors)
	{
		return new ActionResult
		{
			IsSuccess = false,
			Errors = errors
		};
	}

	public static ActionResult Failure(List<string> errors)
	{
		return new ActionResult
		{
			IsSuccess = false,
			Errors = errors
		};
	}
}
