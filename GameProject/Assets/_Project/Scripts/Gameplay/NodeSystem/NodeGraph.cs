using UnityEngine;

namespace Game.NodeSystem
{
    [DisallowMultipleComponent]
    public class NodeGraph : MonoBehaviour
    {
        public static NodeGraph Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
    }
}
