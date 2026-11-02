using System;
using System.Collections.Generic;

namespace WarlordEngine.Actions;

public sealed record DeployStartingArmyAction : IPlayerAction
{
	public required int PlayerIndex { get; init; }

	public required IReadOnlyList<StartingCharacterDeployment> Deployments { get; init; } = Array.Empty<StartingCharacterDeployment>();
}
