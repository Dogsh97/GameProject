using UnityEngine;

namespace Game.Player
{
    public class PlayerInput : MonoBehaviour
    {
        [SerializeField]
        private PlayerController playerController;

        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (GameManager.Instance.CurrentState != GameState.Playing)
                return;

            if (!Input.GetMouseButtonDown(0))
                return;

            if (playerController.CurrentState != PlayerController.State.Idle)
                return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit))
                return;

            // 먼저 HideSpot 확인
            HideSpot hideSpot = hit.collider.GetComponent<HideSpot>();

            if (hideSpot != null)
            {
                playerController.Hide(hideSpot);
                return;
            }

            // 이동 노드 확인
            PlayerMovePoint movePoint = hit.collider.GetComponent<PlayerMovePoint>();

            if (movePoint != null)
            {
                playerController.MoveToNode(movePoint.TargetNode);
            }
        }
    }
}