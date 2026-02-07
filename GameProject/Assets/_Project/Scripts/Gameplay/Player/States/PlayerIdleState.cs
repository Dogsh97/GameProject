using Game.Core.StateMachine;

namespace Game.Player
{
    public class PlayerIdleState : IState
    {
        private readonly PlayerStateMachine ctx;
        public PlayerIdleState(PlayerStateMachine ctx) => this.ctx = ctx;

        public void Enter()
        {
            // Idle 진입 시 필요한 플래그만
            if (ctx.NodeMover != null) ctx.NodeMover.InputEnabled = true;
        }

        public void Tick()
        {
            // 노드 이동이 시작되면 Moving으로 전환
            if (ctx.NodeMover != null && ctx.NodeMover.IsBusy)
                ctx.ToMoving();
        }

        public void Exit() { }
    }
}
