using Game.NodeSystem;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gimmick
{
    [DisallowMultipleComponent]
    public class GimmickNode : MonoBehaviour
    {
        [Header("Bind")]
        [SerializeField] private Node node;

        [Header("Progress (0~100)")]
        [Range(0, 100)][SerializeField] private float progress = 0f;
        [SerializeField] private float maxProgress = 100f;
        [SerializeField] private float passiveDecayPerSecond = 3f;
        [SerializeField] private bool decayWhenNotRunning = true;
        public bool IsRunning { get; set; } // 세션에서 true로 세팅

        [Header("Rates")]
        [SerializeField] private float baseRate = 4f;       // 기본 초당 상승(스킬체크 전/실패 후)
        [SerializeField] private float boostedRate = 7f;    // 스킬체크 성공 후 초당 상승
        [SerializeField] private float decayRate = 6f;      // 중단 시 초당 감소

        [Header("Penalties")]
        [SerializeField] private float failProgressLoss = 12f; // 실패 시 즉시 감소량
        [SerializeField] private float cancelProgressLoss = 6f; // E로 중단 시 즉시 감소량
        [SerializeField] private float retryCooldownOnFail = 2.0f; // 실패 후 재시도 딜레이
        [SerializeField] private float retryCooldownOnCancel = 1.0f;
        
        [Header("Results")]
        [SerializeField] private List<Node> unlockNodes = new(); // 완료 시 열릴 노드들

        public IReadOnlyList<Node> UnlockNodes => unlockNodes;
       
        public Node Node => node;

        public float Progress => progress;
        public float MaxProgress => maxProgress;

        public float BaseRate => baseRate;
        public float BoostedRate => boostedRate;
        public float DecayRate => decayRate;

        public float FailProgressLoss => failProgressLoss;
        public float CancelProgressLoss => cancelProgressLoss;
        public float RetryCooldownOnFail => retryCooldownOnFail;
        public float RetryCooldownOnCancel => retryCooldownOnCancel;

        public bool IsCompleted => progress >= maxProgress;

        private void Reset() => node = GetComponent<Node>();

        private void Update()
        {
            if (!decayWhenNotRunning) return;
            if (IsCompleted) return;
            if (IsRunning) return;

            progress = Mathf.Max(0f, progress - passiveDecayPerSecond * Time.deltaTime);
        }

        public void AddProgress(float amount)
        {
            if (IsCompleted) return;
            progress = Mathf.Clamp(progress + amount, 0f, maxProgress);
            if (IsCompleted)
                ApplyUnlock();
        }

        public void ApplyFailPenalty()
        {
            progress = Mathf.Clamp(progress - failProgressLoss, 0f, maxProgress);
        }

        public void ApplyCancelPenalty()
        {
            progress = Mathf.Clamp(progress - cancelProgressLoss, 0f, maxProgress);
        }

        private void ApplyUnlock()
        {
            for (int i = 0; i < unlockNodes.Count; i++)
            {
                if (unlockNodes[i] == null) continue;
                unlockNodes[i].SetActive(true);
            }
        }
    }
}
