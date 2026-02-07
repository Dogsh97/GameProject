using UnityEngine;
using Game.NodeSystem;
using Game.Hiding;

namespace Game.Player
{
    [DisallowMultipleComponent]
    public class PlayerHideController : MonoBehaviour
    {
        [SerializeField] private KeyCode hideKey = KeyCode.E;

        private PlayerNodeMover nodeMover;
        private PlayerStateMachine psm;

        public bool IsHiding { get; private set; }
        public HideSpot CurrentSpot { get; private set; }

        private void Awake()
        {
            nodeMover = GetComponent<PlayerNodeMover>();
            psm = GetComponent<PlayerStateMachine>();
        }

        private void Update()
        {
            if (nodeMover == null || psm == null) return;

            // 이동 중이면 숨기 시작/해제 불가
            if (nodeMover.IsBusy) return;

            if (Input.GetKeyDown(hideKey))
            {
                if (!IsHiding) TryEnterHide();
                else ExitHide();
            }
        }

        private void TryEnterHide()
        {
            Node cur = nodeMover.CurrentNode;
            if (cur == null) return;

            HideSpot spot = cur.GetComponent<HideSpot>();
            if (spot == null || !spot.IsEnabled) return;

            IsHiding = true;
            CurrentSpot = spot;

            psm.ToHiding();

            // (선택) 이벤트
            EventBus.RaiseHideEntered();
        }

        private void ExitHide()
        {
            IsHiding = false;
            CurrentSpot = null;

            psm.ToIdle();

            // (선택) 이벤트
            EventBus.RaiseHideExited();
        }
    }
}
