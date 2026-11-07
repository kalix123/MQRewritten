using Server.Reawakened.Entities.Enemies.EnemyTypes;
using static A2m.Server.ExtLevelEditor;

namespace Server.Reawakened.Entities.Components.Characters.Controllers.Base.Abstractions;

public interface IAIState
{
    string StateName { get; }
    float StateStartTime { get; }

    void StartState(float time);
    void UpdateState();
    void StopState();

    ComponentSettings GetFullSettings(float time);
    ComponentSettings GetJoinSettings();
    void SetStateMachine(IAIStateMachine machine);
    void SetEnemyController(AIStateEnemy enemyController);
}
