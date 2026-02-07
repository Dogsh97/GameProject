using Game.Core.StateMachine;

namespace Game.Player
{
    public class PlayerGimmickState : IState
    {
        private readonly PlayerStateMachine ctx;
        public PlayerGimmickState(PlayerStateMachine ctx) => this.ctx = ctx;

        public void Enter()
        {
            if (ctx.NodeMover != null) ctx.NodeMover.InputEnabled = false;
        }

        public void Tick()
        {
            // 2.2에서 "스킬체크 종료"되면 Idle로 복귀
        }

        public void Exit()
        {
            if (ctx.NodeMover != null) ctx.NodeMover.InputEnabled = true;
        }
    }
}
