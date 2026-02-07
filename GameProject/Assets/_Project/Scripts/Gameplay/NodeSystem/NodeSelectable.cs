using UnityEngine;

namespace Game.NodeSystem
{
    [RequireComponent(typeof(Collider))]
    public class NodeSelectable : MonoBehaviour
    {
        [SerializeField] private Node node;

        public Node Node => node;

        private void Reset()
        {
            node = GetComponent<Node>();
        }
    }
}
