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
            if (!Input.GetMouseButtonDown(0))
                return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit))
                return;

            PlayerMovePoint movePoint = hit.collider.GetComponent<PlayerMovePoint>();

            if (movePoint == null)
                return;

            playerController.MoveToNode(movePoint.TargetNode);
        }
    }
}