using UnityEngine;

namespace Game.Player
{
    public class PlayerMovePointManager : MonoBehaviour
    {
        [SerializeField]
        private PlayerController player;

        [SerializeField]
        private PlayerMovePoint[] movePoints;

        private void Start()
        {
            player.OnNodeChanged += UpdateMovePoints;

            UpdateMovePoints(player.CurrentNode);
        }

        private void OnDestroy()
        {
            if (player != null)
                player.OnNodeChanged -= UpdateMovePoints;
        }

        private void UpdateMovePoints(PlayerNode currentNode)
        {
            foreach (PlayerMovePoint point in movePoints)
            {
                if (player.CurrentState != PlayerController.State.Idle)
                {
                    point.SetVisible(false);
                    continue;
                }

                bool connected = currentNode.IsConnected(point.TargetNode);

                point.SetVisible(connected);
            }
        }
    }
}