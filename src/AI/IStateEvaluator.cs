namespace WarlordEngine.AI;

public interface IStateEvaluator
{
	double Evaluate(GameState state, int maximizingPlayerId);
}
