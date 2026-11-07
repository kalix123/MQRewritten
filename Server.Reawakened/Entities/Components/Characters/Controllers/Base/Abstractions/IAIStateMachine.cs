using Server.Reawakened.Entities.Enemies.EnemyTypes;
using Server.Reawakened.Players;

namespace Server.Reawakened.Entities.Components.Characters.Controllers.Base.Abstractions;
public interface IAIStateMachine
{
    void AddNextState<AiState>() where AiState : class, IAIState;
    void AddNextState(Type t);
    void GoToNextState();
    void SendCurrentStates(Player player);

    void SetAIStateEnemy(AIStateEnemy enemy);

    int GetForceDirectionX();
    void SetForceDirectionX(int directionX);
}
