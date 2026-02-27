using UnityEngine;
using Game.NodeSystem;
using Game.Player;

namespace Game.CameraSystem
{
    [DisallowMultipleComponent]
    public class FirstPersonNodeCamera : MonoBehaviour
    {
        [Header("컴포넌트 참조")]
        [Tooltip("카메라 회전의 중심이 되는 피벗 오브젝트")]
        [SerializeField] private Transform cameraPivot;
        [Tooltip("사용될 메인 카메라")]
        [SerializeField] private Camera cam;
        [Tooltip("플레이어 이동 로직 스크립트")]
        [SerializeField] private PlayerNodeMover nodeMover;
        [Tooltip("플레이어 상태 머신 스크립트")]
        [SerializeField] private PlayerStateMachine psm;

        [Header("회전 입력 설정")]
        [Tooltip("초당 회전 속도 (도 단위)")]
        [SerializeField] private float yawSpeedDegPerSec = 90f;

        [Header("회전 제한 (대기 상태)")]
        [Tooltip("대기 중 좌우 회전 가능 범위 (중심 기준)")]
        [SerializeField] private float yawRangeIdle = 160f;
        [Tooltip("대기 중 상하 회전 가능 범위")]
        [SerializeField] private float pitchRangeIdle = 10f;

        [Header("회전 제한 (이동 상태)")]
        [Tooltip("이동 중 좌우 회전 가능 범위")]
        [SerializeField] private float yawRangeMoving = 60f;
        [Tooltip("이동 중 상하 회전 가능 범위 (0은 고정)")]
        [SerializeField] private float pitchRangeMoving = 0f;

        [Header("시야각 (FOV)")]
        [Tooltip("기본 시야각")]
        [SerializeField] private float baseFov = 70f;
        [Tooltip("이동 중 시야각")]
        [SerializeField] private float movingFov = 64f;
        [Tooltip("기믹/스킬체크 중 시야각")]
        [SerializeField] private float gimmickFov = 58f;
        [Tooltip("시야각 변화 부드러움 정도")]
        [SerializeField] private float fovLerpSpeed = 10f;

        [Header("도착 시 자동 정렬")]
        [Tooltip("노드 도착 후 정면을 바라보는 데 걸리는 시간")]
        [SerializeField] private float alignDuration = 0.25f;

        [Header("이동 중 Burst 흔들림 (Move Shake) - 부드럽게")]
        [Tooltip("버스트 흔들림 강도(각도). 너무 크면 멀미남. 추천 0.1~0.25")]
        [SerializeField] private float shakeAmplitudeDeg = 0.2f;
        [Tooltip("한 번의 흔들림(Burst)이 지속되는 시간")]
        [SerializeField] private float shakeBurstDuration = 0.12f;
        [Tooltip("흔들림 사이 최소 간격")]
        [SerializeField] private float shakeBurstIntervalMin = 0.4f;
        [Tooltip("흔들림 사이 최대 간격")]
        [SerializeField] private float shakeBurstIntervalMax = 0.9f;
        [Tooltip("흔들림 페이드 속도 (클수록 더 빨리/부드럽게 따라감)")]
        [SerializeField] private float shakeFadeSpeed = 12f;

        [Header("이동 중 Bob (걷기 상하 반복)")]
        [Tooltip("상하 진폭(각도). 추천 0.3~0.6")]
        [SerializeField] private float bobAmplitudeDeg = 0.5f;
        [Tooltip("초당 반복 횟수. 추천 1.6~2.2")]
        [SerializeField] private float bobFrequency = 1.8f;
        [Tooltip("이동 시작/끝 bob이 켜지고 꺼지는 속도")]
        [SerializeField] private float bobWeightSpeed = 6f;

        private float bobTimer = 0f;

        private float yaw;   // 현재 yaw(월드 기준)
        private float pitch; // 로컬 pitch
        private float yawCenter; // 제한 중심
        private float pitchCenter;

        private bool isMoving;
        private float alignTimer;
        private float alignStartYaw;
        private float alignTargetYaw;

        private float shakeTimer;
        private float nextShakeTime;

        // 최종 적용 오프셋
        private float shakeOffsetYaw;
        private float shakeOffsetPitch;

        // 부드러운 흔들림을 위한 현재/목표 값
        private float shakeCurrentYaw;
        private float shakeCurrentPitch;
        private float shakeTargetYaw;
        private float shakeTargetPitch;

        // bob on/off 가중치
        private float bobWeight;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (cameraPivot == null) cameraPivot = transform;
            if (nodeMover == null) nodeMover = GetComponentInParent<PlayerNodeMover>();
            if (psm == null) psm = GetComponentInParent<PlayerStateMachine>();

            // 초기 회전값
            Vector3 e = cameraPivot.rotation.eulerAngles;
            yaw = e.y;
            pitch = 0f;

            yawCenter = yaw;
            pitchCenter = pitch;

            ScheduleNextShake();
        }

        private void OnEnable()
        {
            if (nodeMover != null)
            {
                nodeMover.OnMoveStarted += HandleMoveStarted;
                nodeMover.OnMoveFinished += HandleMoveFinished;
            }
        }

        private void OnDisable()
        {
            if (nodeMover != null)
            {
                nodeMover.OnMoveStarted -= HandleMoveStarted;
                nodeMover.OnMoveFinished -= HandleMoveFinished;
            }
        }

        private void Update()
        {
            // 1) 상태에 따른 목표 FOV
            float targetFov = baseFov;
            if (isMoving) targetFov = movingFov;

            // 기믹(스킬체크) 근사: 입력이 꺼져있고 이동 중이 아니면
            bool inGimmick = (psm != null && nodeMover != null && !nodeMover.InputEnabled && !isMoving);
            if (inGimmick) targetFov = gimmickFov;

            if (cam != null)
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, fovLerpSpeed * Time.deltaTime);

            // 2) 회전 제약 파라미터
            float yawRange = isMoving ? yawRangeMoving : yawRangeIdle;
            float pitchRange = isMoving ? pitchRangeMoving : pitchRangeIdle;
            pitchRange = Mathf.Clamp(pitchRange, 0f, 89f);

            float pitchMinDyn = -pitchRange;
            float pitchMaxDyn = pitchRange;

            // 3) 입력(좌클릭=좌회전, 우클릭=우회전)
            float input = 0f;
            if (Input.GetMouseButton(0)) input -= 1f; // 좌
            if (Input.GetMouseButton(1)) input += 1f; // 우
            yaw += input * yawSpeedDegPerSec * Time.deltaTime;

            // 상하는 지금은 “제한”만 (확장 가능)
            pitch = Mathf.Clamp(pitch, pitchMinDyn, pitchMaxDyn);

            // 4) 제한: yaw는 center 기준 범위 내로 clamp
            float halfYaw = yawRange * 0.5f;
            yaw = ClampAngleAroundCenter(yaw, yawCenter, -halfYaw, halfYaw);

            // 5) 이동 흔들림(부드러운 burst + bob)
            UpdateShake();

            // 6) 노드 도착 자동 정렬
            UpdateAutoAlign();

            // 7) 적용
            ApplyRotation();
        }

        private void HandleMoveStarted()
        {
            isMoving = true;

            // 이동 시작 시 제한 중심을 현재로 재설정
            yawCenter = yaw;
            pitchCenter = pitch;

            // 이동 중 상하는 거의 고정
            pitch = 0f;

            // bob을 깔끔하게 시작
            bobTimer = 0f;

            ScheduleNextShake();
        }

        private void HandleMoveFinished()
        {
            isMoving = false;

            // 도착 시 기본 방향으로 자동 정렬
            TryStartAutoAlignToCurrentNode();
        }

        private void TryStartAutoAlignToCurrentNode()
        {
            if (nodeMover == null) return;
            Node cur = nodeMover.CurrentNode;
            if (cur == null) return;

            NodeLook look = cur.GetComponent<NodeLook>();
            Vector3 forward = (look != null) ? look.Forward : cur.transform.forward;

            float targetYaw = Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y;

            alignStartYaw = yaw;
            alignTargetYaw = targetYaw;
            alignTimer = alignDuration;
        }

        private void UpdateAutoAlign()
        {
            if (alignTimer <= 0f) return;

            alignTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(alignTimer / alignDuration);

            yaw = Mathf.LerpAngle(alignStartYaw, alignTargetYaw, t);

            if (alignTimer <= 0f)
            {
                yawCenter = yaw;
                pitchCenter = pitch;
            }
        }

        private void UpdateShake()
        {
            if (!isMoving)
            {
                // 이동 아닐 때: 목표를 0으로 두고 부드럽게 0으로 복귀
                shakeTargetYaw = 0f;
                shakeTargetPitch = 0f;

                bobWeight = Mathf.MoveTowards(bobWeight, 0f, Time.deltaTime * bobWeightSpeed);
            }
            else
            {
                bobWeight = Mathf.MoveTowards(bobWeight, 1f, Time.deltaTime * bobWeightSpeed);

                // 버스트 시작
                if (Time.time >= nextShakeTime && shakeTimer <= 0f)
                {
                    shakeTimer = shakeBurstDuration;

                    //버스트는 yaw(좌우) 위주
                    shakeTargetYaw = Random.Range(-shakeAmplitudeDeg, shakeAmplitudeDeg);

                    //pitch 랜덤은 매우 약하게(멀미 방지) 또는 0으로 해도 됨
                    shakeTargetPitch = Random.Range(-shakeAmplitudeDeg * 0.25f, shakeAmplitudeDeg * 0.25f);

                    ScheduleNextShake();
                }

                // 버스트 끝나면 목표를 0으로 (부드럽게 돌아감)
                if (shakeTimer > 0f)
                {
                    shakeTimer -= Time.deltaTime;
                    if (shakeTimer <= 0f)
                    {
                        shakeTargetYaw = 0f;
                        shakeTargetPitch = 0f;
                    }
                }
            }

            //목표값으로 부드럽게 따라가기 (툭툭 튐 방지)
            shakeCurrentYaw = Mathf.Lerp(shakeCurrentYaw, shakeTargetYaw, shakeFadeSpeed * Time.deltaTime);
            shakeCurrentPitch = Mathf.Lerp(shakeCurrentPitch, shakeTargetPitch, shakeFadeSpeed * Time.deltaTime);

            //bob은 pitch에만
            bobTimer += Time.deltaTime;
            float bob = Mathf.Sin(bobTimer * bobFrequency * Mathf.PI * 2f) * bobAmplitudeDeg * bobWeight;

            shakeOffsetYaw = shakeCurrentYaw;
            shakeOffsetPitch = shakeCurrentPitch + bob;
        }

        private void ScheduleNextShake()
        {
            nextShakeTime = Time.time + Random.Range(shakeBurstIntervalMin, shakeBurstIntervalMax);
        }

        private void ApplyRotation()
        {
            float finalYaw = yaw + shakeOffsetYaw;
            float finalPitch = pitch + shakeOffsetPitch;

            cameraPivot.rotation = Quaternion.Euler(finalPitch, finalYaw, 0f);
        }

        private static float ClampAngleAroundCenter(float angle, float center, float minOffset, float maxOffset)
        {
            float delta = Mathf.DeltaAngle(center, angle);
            delta = Mathf.Clamp(delta, minOffset, maxOffset);
            return center + delta;
        }
    }
}