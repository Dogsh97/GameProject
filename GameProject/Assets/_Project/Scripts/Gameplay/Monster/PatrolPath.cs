using System.Collections.Generic;
using UnityEngine;
using Game.NodeSystem;

namespace Game.Monster
{
    [DisallowMultipleComponent]
    public class PatrolPath : MonoBehaviour
    {
        [SerializeField] private List<Node> pathNodes = new();
        [SerializeField] private bool loop = true;

        public IReadOnlyList<Node> PathNodes => pathNodes;
        public bool Loop => loop;

        public bool IsValid => pathNodes != null && pathNodes.Count >= 2;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (pathNodes == null || pathNodes.Count < 2) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < pathNodes.Count - 1; i++)
            {
                if (pathNodes[i] == null || pathNodes[i + 1] == null) continue;
                Gizmos.DrawLine(pathNodes[i].Position, pathNodes[i + 1].Position);
            }

            if (loop && pathNodes[0] != null && pathNodes[^1] != null)
                Gizmos.DrawLine(pathNodes[^1].Position, pathNodes[0].Position);
        }
#endif
    }
}
