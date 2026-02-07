using Game.Core.StateMachine;

namespace Game.Player
{
    public class PlayerMovingState : IState
    {
        private readonly PlayerStateMachine ctx;
        public PlayerMovingState(PlayerStateMachine ctx) => this.ctx = ctx;

        public void Enter()
        {
            // 이동 중에는 추가 입력 잠금(취소 입력은 NodeMover 내부에서 처리해도 됨)
            if (ctx.NodeMover != null) ctx.NodeMover.InputEnabled = true;
        }

        public void Tick()
        {
            // 이동이 끝나면 Idle로
            if (ctx.NodeMover != null && !ctx.NodeMover.IsBusy)
                ctx.ToIdle();
        }

        public void Exit() { }
    }
}
