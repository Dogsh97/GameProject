using System.Collections;
using UnityEngine;
using Game.NodeSystem;
using Game.Gimmick;
using Game.UI;

namespace Game.Player
{
    [DisallowMultipleComponent]
    public class PlayerGimmickController : MonoBehaviour
    {
        [Header("입력 설정")]
        [Tooltip("기믹 상호작용 및 중단에 사용되는 키 (기본: E)")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [Tooltip("스킬 체크 미니게임을 관리하는 UI 컨트롤러")]
        [SerializeField] private SkillCheckUIController skillCheckUI;

        [Header("스킬 체크 타이밍")]
        [Tooltip("스킬 체크가 발생하는 기본 간격(초)")]
        [SerializeField] private float checkBaseInterval = 2.2f;
        [Tooltip("타이밍의 무작위성 오차 범위 (+- 초)")]
        [SerializeField] private float checkRandomJitter = 0.6f;// +- 오차
        [Tooltip("기믹 진행도가 높아질 때 수렴하는 최소 간격(초)")]
        [SerializeField] private float checkMinInterval = 0.7f;// 진행할수록 하한으로 수렴

        [Header("난이도 조절")]
        [Tooltip("시작 시 성공 구간의 너비 (픽셀 단위)")]
        [SerializeField] private float zoneWidthStart = 220f; // px
        [Tooltip("완료 직전 성공 구간의 너비 (픽셀 단위, 진행될수록 축소됨)")]
        [SerializeField] private float zoneWidthEnd = 120f; // px (진행할수록 축소)

        [Header("몬스터 제한 조건")]
        [Tooltip("몬스터가 이 거리 안에 있으면 기믹을 시작할 수 없음 (공격 사거리 근사치)")]
        [SerializeField] private float forbidIfMonsterWithin = 1.2f; // “즉시 공격 가능 상태” 근사치

        private PlayerNodeMover nodeMover;
        private PlayerStateMachine psm;

        private bool inSession;
        private bool onCooldown;
        private float cooldownEndTime;

        private GimmickNode gimmick;
        private Coroutine routine;

        private Transform monsterTf; // 최소 조건 체크용(간단히)

        private void Awake()
        {
            nodeMover = GetComponent<PlayerNodeMover>();
            psm = GetComponent<PlayerStateMachine>();
            if (skillCheckUI == null)
                skillCheckUI = FindAnyObjectByType<SkillCheckUIController>(FindObjectsInactive.Include);

            var monsterAI = FindAnyObjectByType<Game.Monster.MonsterNodeAI>();
            if (monsterAI != null) monsterTf = monsterAI.transform;
        }

        private void Update()
        {
            if (nodeMover == null || psm == null) return;
            if (nodeMover.IsBusy) return;

            if (onCooldown && Time.time >= cooldownEndTime)
                onCooldown = false;

            if (Input.GetKeyDown(interactKey))
            {
                if (!inSession) TryStart();
                else StopByPlayer();
            }
            Debug.Log($"timeScale={Time.timeScale}, now={Time.time}, cooldownEnd={cooldownEndTime}");
        }

        private void TryStart()
        {
            if (onCooldown) return;

            Node cur = nodeMover.CurrentNode;
            if (cur == null) return;

            gimmick = cur.GetComponent<GimmickNode>();
            if (gimmick == null) return;
            if (gimmick.IsCompleted) return;
            if (!cur.IsActive) return;

            var monsterAI = FindAnyObjectByType<Game.Monster.MonsterNodeAI>();
            if (monsterAI != null && monsterAI.CanAttackNow(transform))
                return;

            inSession = true;
            psm.ToGimmick();

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(SessionLoop());
        }

        private void StopByPlayer()
        {
            if (!inSession) return;

            // 중단 패널티 + 압박 증가
            gimmick?.ApplyCancelPenalty();
            EventBus.RaiseNoise(transform.position, 0.4f);

            StartCooldown(gimmick != null ? gimmick.RetryCooldownOnCancel : 1.0f);
            EndSession(goIdle: true);
        }

        private IEnumerator SessionLoop()
        {
            float currentRate = gimmick.BaseRate; // 초당 진행도
            float nextCheckTime = Time.time + GetNextInterval01();

            while (inSession && gimmick != null && !gimmick.IsCompleted)
            {
                // 진행도는 “스킬체크를 잘해야 빨라지고, 실수하면 느려짐”
                gimmick.AddProgress(currentRate * Time.deltaTime);

                // 스킬 체크 주기 도래
                if (Time.time >= nextCheckTime)
                {
                    bool done = false;
                    bool success = false;

                    // 진행할수록 빈도 증가 + 성공구간 축소
                    float p01 = Mathf.Clamp01(gimmick.Progress / gimmick.MaxProgress);
                    float interval = Mathf.Lerp(checkBaseInterval, checkMinInterval, p01);
                    float zoneWidth = Mathf.Lerp(zoneWidthStart, zoneWidthEnd, p01);

                    // 스킬체크 실행 (UI가 끝날 때까지 대기)
                    skillCheckUI.Begin(result =>
                    {
                        success = result;
                        done = true;
                    }, overrideSuccessZoneWidth: zoneWidth, randomizeZonePosition: true);

                    while (!done && inSession)
                        yield return null;

                    if (!inSession) break;

                    if (success)
                    {
                        // 성공: 진행 속도 업(누적 성공 설계)
                        currentRate = gimmick.BoostedRate;
                    }
                    else
                    {
                        // 실패: 진행도 감소 + 속도 기본으로 + 재시도 딜레이
                        gimmick.ApplyFailPenalty();
                        currentRate = gimmick.BaseRate;

                        EventBus.RaiseSkillCheckFailed(gimmick);
                        EventBus.RaiseNoise(transform.position, 0.7f);

                        StartCooldown(gimmick.RetryCooldownOnFail);
                        break; // 실패하면 세션 강제 종료(요구: 실패 시 취소 이후 재진행 딜레이)
                    }

                    nextCheckTime = Time.time + interval + UnityEngine.Random.Range(-checkRandomJitter, checkRandomJitter);
                }

                yield return null;
            }

            // 완료 처리
            if (gimmick != null && gimmick.IsCompleted)
            {
                Debug.Log("[Gimmick] Completed (Progress=100)");
                // 너가 A에서 언락은 GimmickNode 완료 처리로 붙였으면 여기서도 이어짐.
                EventBus.RaiseGimmickCompleted(gimmick);
                //EventBus.RaiseNoise(transform.position, 0.2f);
            }

            EndSession(goIdle: true);
        }

        private float GetNextInterval01()
            => checkBaseInterval + UnityEngine.Random.Range(-checkRandomJitter, checkRandomJitter);

        private void StartCooldown(float sec)
        {
            onCooldown = true;
            cooldownEndTime = Time.time + Mathf.Max(0f, sec);
        }

        private void EndSession(bool goIdle)
        {
            inSession = false;

            if (skillCheckUI != null && (skillCheckUI.gameObject.activeSelf || skillCheckUI.IsRunning))
                skillCheckUI.Cancel();

            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            gimmick = null;

            if (goIdle)
                psm.ToIdle();
        }
    }
}
