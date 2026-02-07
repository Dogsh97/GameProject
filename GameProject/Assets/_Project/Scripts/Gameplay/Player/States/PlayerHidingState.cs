using Game.Core.StateMachine;

namespace Game.Player
{
    public class PlayerHidingState : IState
    {
        private readonly PlayerStateMachine ctx;
        public PlayerHidingState(PlayerStateMachine ctx) => this.ctx = ctx;

        public void Enter()
        {
            if (ctx.NodeMover != null) ctx.NodeMover.InputEnabled = false;
        }

        public void Tick()
        {
            // 2.3에서 "숨기 해제 조건" 구현되면 여기서 Idle로 복귀
        }

        public void Exit()
        {
            if (ctx.NodeMover != null) ctx.NodeMover.InputEnabled = true;
        }
    }
}
