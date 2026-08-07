using UnityEngine;

namespace Game.Player
{
    public class PlayerMovePoint : MonoBehaviour
    {
        [SerializeField]
        private PlayerNode targetNode;

        [SerializeField]
        private GameObject visual;

        public PlayerNode TargetNode => targetNode;

        public void SetVisible(bool visible)
        {
            if (visual != null)
                visual.SetActive(visible);
        }
    }
}