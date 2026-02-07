using UnityEngine;
using Game.NodeSystem;
using Game.Gimmick;
using Game.UI;

namespace Game.Player
{
    [DisallowMultipleComponent]
    public class PlayerGimmickController : MonoBehaviour
    {
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private SkillCheckUIController skillCheckUI;

        private PlayerNodeMover nodeMover;
        private PlayerStateMachine psm;

        private void Awake()
        {
            nodeMover = GetComponent<PlayerNodeMover>();
            psm = GetComponent<PlayerStateMachine>();

            if (skillCheckUI == null)
                skillCheckUI = FindAnyObjectByType<SkillCheckUIController>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (psm == null || nodeMover == null) return;

            // 이동 중이면 기믹 시작 불가
            if (nodeMover.IsBusy) return;

            if (Input.GetKeyDown(interactKey))
                TryBeginGimmick();
        }

        private void TryBeginGimmick()
        {
            Node cur = nodeMover.CurrentNode;
            if (cur == null) return;

            GimmickNode gimmick = cur.GetComponent<GimmickNode>();
            if (gimmick == null) return;
            if (gimmick.IsCompleted) return;
            if (skillCheckUI == null) return;

            // 상태 전환: Gimmick
            psm.ToGimmick();

            // 스킬체크 시작
            skillCheckUI.Begin(success =>
            {
                if (success)
                {
                    gimmick.AddSuccess(1);
                    Debug.Log($"[Gimmick] Success {gimmick.CurrentSuccess}/{gimmick.RequiredSuccess}");

                    if (gimmick.IsCompleted)
                    {
                        Debug.Log("[Gimmick] Completed!");
                        // TODO: 문 열림/다음 노드 활성화 같은 “보상”은 2.2 끝나고 연결
                    }
                }
                else
                {
                    Debug.Log("[Gimmick] Fail");
                    // MVP 패널티: 일단 없음(2.4에서 공격성/어그로에 연결)
                }

                psm.ToIdle();
            });
        }
    }
}
