using UnityEngine;
using Game.Gimmick;

namespace Game.Monster
{
    [DisallowMultipleComponent]
    public class MonsterAggro : MonoBehaviour
    {
        [Header("Aggro 0~100")]
        [Range(0, 100)][SerializeField] private float aggro = 0f;

        [Header("Time Ramp (per sec)")]
        [SerializeField] private float rampPerSecond = 1.5f;  // 시간 경과로 상승
        [SerializeField] private float rampMultiplier = 1.0f; // 후반 가중치(스테이지/시간으로 조절 가능)

        [Header("Action Adds")]
        [SerializeField] private float failAdd = 18f;
        [SerializeField] private float noiseMinAdd = 5f;
        [SerializeField] private float noiseMaxAdd = 20f;

        public float Aggro => aggro;

        public void ResetAggro() => aggro = 0f;

        public void AddAggro(float amount)
        {
            aggro = Mathf.Clamp(aggro + amount, 0f, 100f);
        }

        private void OnEnable()
        {
            EventBus.OnSkillCheckFailed += OnSkillCheckFailed;
            EventBus.OnNoise += OnNoise;
            EventBus.OnMonsterRespawned += ResetAggro;
        }

        private void OnDisable()
        {
            EventBus.OnSkillCheckFailed -= OnSkillCheckFailed;
            EventBus.OnNoise -= OnNoise;
            EventBus.OnMonsterRespawned -= ResetAggro;
        }

        private void Update()
        {
            // 시간 경과로 점진 상승
            AddAggro(rampPerSecond * rampMultiplier * Time.deltaTime);
        }

        private void OnSkillCheckFailed(GimmickNode g) => AddAggro(failAdd);

        private void OnNoise(Vector3 pos, float intensity)
        {
            AddAggro(Mathf.Lerp(noiseMinAdd, noiseMaxAdd, Mathf.Clamp01(intensity)));
        }
    }
}
