namespace WarlordEngine.Events;

public sealed record FormationFallForwardEvent : GameEvent
{
	public required int PlayerIndex { get; init; }

	public int PlayerId
	{
		get => PlayerIndex;
		init => PlayerIndex = value;
	}

	public required int CharacterInstanceId { get; init; }

	public string DefinitionId { get; init; } = string.Empty;

	public required int FromRank { get; init; }

	public required int ToRank { get; init; }

	public ReadyState PreviousReadyState { get; init; }

	public ReadyState NewReadyState { get; init; }

	public FormationFallForwardEvent()
	{
	}

	[System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
	public FormationFallForwardEvent(int playerIndex, int characterInstanceId, int fromRank, int toRank, string definitionId = "", ReadyState previousReadyState = ReadyState.Ready, ReadyState newReadyState = ReadyState.Spent)
	{
		PlayerIndex = playerIndex;
		CharacterInstanceId = characterInstanceId;
		FromRank = fromRank;
		ToRank = toRank;
		DefinitionId = definitionId;
		PreviousReadyState = previousReadyState;
		NewReadyState = newReadyState;
	}

	public override GameState ApplyToState(GameState state)
	{
		return state;
	}
}
