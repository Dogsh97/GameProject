using UnityEngine;

namespace Game.Player
{
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField]
        private PlayerController player;

        private void Start()
        {
            player.OnNodeChanged += MoveCamera;
        }

        private void OnDestroy()
        {
            if (player != null)
                player.OnNodeChanged -= MoveCamera;
        }

        private void MoveCamera(PlayerNode node)
        {
            if (node.CameraPoint == null)
                return;

            transform.SetPositionAndRotation(
                node.CameraPoint.position,
                node.CameraPoint.rotation);
        }
    }
}