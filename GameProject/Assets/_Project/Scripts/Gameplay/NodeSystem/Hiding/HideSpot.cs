using UnityEngine;
using Game.NodeSystem;

namespace Game.Hiding
{
    [DisallowMultipleComponent]
    public class HideSpot : MonoBehaviour
    {
        [SerializeField] private Node node;
        [SerializeField] private bool isEnabled = true;

        public Node Node => node;
        public bool IsEnabled => isEnabled;

        private void Reset()
        {
            node = GetComponent<Node>();
        }
    }
}
