using UnityEngine;
using Game.Core.StateMachine;

namespace Game.Player
{
    [DisallowMultipleComponent]
    public class PlayerStateMachine : MonoBehaviour
    {
        public StateMachine SM { get; private set; } = new StateMachine();

        // 의존 컴포넌트들(필요한 것만 늘려감)
        public PlayerNodeMover NodeMover { get; private set; }

        private IState idle;
        private IState moving;
        private IState hiding;
        private IState gimmick;

        [Header("Debug")]
        [SerializeField] private string currentStateName;

        private void Awake()
        {
            NodeMover = GetComponent<PlayerNodeMover>();

            idle = new PlayerIdleState(this);
            moving = new PlayerMovingState(this);
            hiding = new PlayerHidingState(this);
            gimmick = new PlayerGimmickState(this);
        }

        private void Start()
        {
            SM.Set(idle);
        }

        private void Update()
        {
            SM.Tick();
            
            //디버그용
            if (SM.Current != null)
            {
                currentStateName = SM.Current.GetType().Name;
            }
            else
            {
                currentStateName = "None";
            }
        }

        // 외부(노드/기믹/숨기)에서 상태 전환 호출
        public void ToIdle() => SM.Set(idle);
        public void ToMoving() => SM.Set(moving);
        public void ToHiding() => SM.Set(hiding);
        public void ToGimmick() => SM.Set(gimmick);
    }
}
