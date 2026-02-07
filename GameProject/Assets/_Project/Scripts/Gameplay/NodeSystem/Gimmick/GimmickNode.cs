using Game.NodeSystem;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gimmick
{
    [DisallowMultipleComponent]
    public class GimmickNode : MonoBehaviour
    {
        [Header("Bind")]
        [SerializeField] private Node node; // 이 기믹이 붙은 노드

        [Header("Progress")]
        [SerializeField] private int requiredSuccess = 3;
        [SerializeField] private int currentSuccess = 0;

        [Header("Rewards")]
        [SerializeField] private List<Node> unlockNodes = new();
        public IReadOnlyList<Node> UnlockNodes => unlockNodes;

        public Node Node => node;
        public int RequiredSuccess => requiredSuccess;
        public int CurrentSuccess => currentSuccess;
        public bool IsCompleted => currentSuccess >= requiredSuccess;

        private void Reset()
        {
            node = GetComponent<Node>();
        }

        public void AddSuccess(int amount = 1)
        {
            if (IsCompleted) return;

            currentSuccess = Mathf.Clamp(currentSuccess + amount, 0, requiredSuccess);

            if (IsCompleted)
                ApplyUnlock();
        }

        private void ApplyUnlock()
        {
            for (int i = 0; i < unlockNodes.Count; i++)
            {
                if (unlockNodes[i] == null) continue;
                unlockNodes[i].SetActive(true);
            }
            EventBus.RaiseGimmickCompleted(this);
        }
    }
}
