namespace Game.Core.StateMachine
{
    public interface IState
    {
        void Enter();
        void Tick();
        void Exit();
    }
}
