using System.Collections.Generic;
using UnityEngine;

namespace Game.NodeSystem
{
    [DisallowMultipleComponent]
    public class Node : MonoBehaviour
    {
        [Header("인접한 노드 지정")]
        [SerializeField] private List<Node> neighbors = new();

        [Header("이동 가능 여부")]
        [SerializeField] private bool isActive = true;

        public IReadOnlyList<Node> Neighbors => neighbors;
        public bool IsActive => isActive;

        public Vector3 Position => transform.position;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 1순위: 기믹 노드라면 노란색
            if (GetComponent<Game.Gimmick.GimmickNode>() != null)
            {
                Gizmos.color = Color.yellow;
            }
            // 2순위: 은신처 노드라면 파란색
            else if (GetComponent<Game.Hiding.HideSpot>() != null)
            {
                Gizmos.color = Color.blue;
            }
            // 3순위: 일반 노드는 활성화 여부에 따라 초록/빨강
            else
            {
                Gizmos.color = isActive ? Color.green : Color.red;
            }

            Gizmos.DrawSphere(transform.position, 0.5f);

            Gizmos.color = Color.white;
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
