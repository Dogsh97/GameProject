using System.Collections.Generic;
using UnityEngine;

namespace Game.NodeSystem
{
    [DisallowMultipleComponent]
    public class Node : MonoBehaviour
    {
        [Header("Graph")]
        [SerializeField] private List<Node> neighbors = new();

        [Header("Rules")]
        [SerializeField] private bool isActive = true;

        public IReadOnlyList<Node> Neighbors => neighbors;
        public bool IsActive => isActive;

        public Vector3 Position => transform.position;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = isActive ? Color.green : Color.red;
            Gizmos.DrawSphere(transform.position, 0.5f);

            Gizmos.color = Color.yellow;
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (neighbors[i] == null) continue;
                Gizmos.DrawLine(transform.position, neighbors[i].transform.position);
            }
        }
#endif

        public bool IsNeighbor(Node other)
        {
            if (other == null) return false;
            return neighbors.Contains(other);
        }

        public void SetActive(bool active)
        {
            isActive = active;
        }

    }
}
