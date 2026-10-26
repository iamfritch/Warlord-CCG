using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Actions;

public sealed record DeployStartingArmyAction : PlayerAction, IPlayerAction
{
	public required IReadOnlyList<StartingCharacterDeployment> Deployments { get; init; } = Array.Empty<StartingCharacterDeployment>();

	[CompilerGenerated]
	[SetsRequiredMembers]
	private DeployStartingArmyAction(DeployStartingArmyAction original)
		: base(original)
	{
		Deployments = original.Deployments;
	}
}
