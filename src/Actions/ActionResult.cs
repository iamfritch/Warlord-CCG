using System;
using System.Collections.Generic;

namespace WarlordEngine.Actions;

public sealed record ActionResult
{
	public bool IsSuccess { get; init; }

	public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

	public static ActionResult Success()
	{
		return new ActionResult
		{
			IsSuccess = true
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
