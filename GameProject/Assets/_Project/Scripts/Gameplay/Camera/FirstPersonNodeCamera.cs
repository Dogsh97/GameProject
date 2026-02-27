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
        [Tooltip("카메라 상하 최소 각도")]
        [SerializeField] private float pitchMin = -10f;
        [Tooltip("카메라 상하 최대 각도")]
        [SerializeField] private float pitchMax = 10f;

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

        [Header("이동 중 흔들림 (Move Shake)")]
        [Tooltip("흔들림의 강도 (각도)")]
        [SerializeField] private float shakeAmplitudeDeg = 0.7f;
        [Tooltip("한 번의 흔들림(Burst)이 지속되는 시간")]
        [SerializeField] private float shakeBurstDuration = 0.18f;
        [Tooltip("흔들림 사이의 최소 간격")]
        [SerializeField] private float shakeBurstIntervalMin = 0.22f;
        [Tooltip("흔들림 사이의 최대 간격")]
        [SerializeField] private float shakeBurstIntervalMax = 0.6f;

        [Header("이동 중 Bob (상하 반복)")]
        [SerializeField] private float bobAmplitudeDeg = 1.5f;   // 상하 진폭 (각도)
        [SerializeField] private float bobFrequency = 2.0f;      // 초당 반복 횟수
        private float bobTimer = 0f;

        [Header("이동 흔들림 스무딩")]
        [SerializeField] private float shakeFadeSpeed = 12f; // 숫자 클수록 빨리 부드럽게 따라감

        private float shakeCurrentYaw;
        private float shakeCurrentPitch;
        private float shakeTargetYaw;
        private float shakeTargetPitch;

        private float bobWeight; // 0~1 (이동중 1, 멈추면 0)

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
        private float shakeOffsetYaw;
        private float shakeOffsetPitch;

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

            // 기믹(스킬체크) 중이면 추가 축소
            // (PlayerStateMachine의 Gimmick 상태에서 NodeMover.InputEnabled=false라 했으니,
            //  여기서는 "InputEnabled=false && !isMoving"로 기믹 상태를 근사하거나,
            //  더 정확히는 psm.CurrentState를 노출해서 체크하면 됨.
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

            // 상하 회전은 “제한됨”이라 기본은 거의 안 쓰지만, 나중에 확장 가능
            // 여기서는 pitch를 센터로 고정/클램프만 유지
            pitch = Mathf.Clamp(pitch, pitchMinDyn, pitchMaxDyn);

            // 4) 제한: yaw는 center 기준 범위 내로 clamp
            float halfYaw = yawRange * 0.5f;
            yaw = ClampAngleAroundCenter(yaw, yawCenter, -halfYaw, halfYaw);

            // 5) 이동 중 흔들림(짧고 불규칙한 burst)
            UpdateShake();

            // 6) 노드 도착 자동 정렬(짧은 지연 회전)
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

            // 이동 중 상하 거의 고정
            pitch = 0f;
            bobTimer = 0f;

            ScheduleNextShake();
        }

        private void HandleMoveFinished()
        {
            isMoving = false;

            // 도착 시 기본 방향으로 자동 정렬
            TryStartAutoAlignToCurrentNode();

            // 도착 직후 중심도 기본 방향으로 맞춤(정렬 끝나면 업데이트됨)
        }

        private void TryStartAutoAlignToCurrentNode()
        {
            if (nodeMover == null) return;
            Node cur = nodeMover.CurrentNode;
            if (cur == null) return;

            NodeLook look = cur.GetComponent<NodeLook>();
            Vector3 forward = (look != null) ? look.Forward : cur.transform.forward;

            // 목표 yaw 계산
            float targetYaw = Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y;

            alignStartYaw = yaw;
            alignTargetYaw = targetYaw;
            alignTimer = alignDuration;

            // 정렬 중엔 제한 중심도 target 쪽으로 유도
        }

        private void UpdateAutoAlign()
        {
            if (alignTimer <= 0f) return;

            alignTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(alignTimer / alignDuration);

            yaw = Mathf.LerpAngle(alignStartYaw, alignTargetYaw, t);

            // 정렬이 끝나면 중심 갱신
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
                // 이동 아닐 때는 목표를 0으로 두고 부드럽게 0으로 돌아가게
                shakeTargetYaw = 0f;
                shakeTargetPitch = 0f;

                bobWeight = Mathf.MoveTowards(bobWeight, 0f, Time.deltaTime * 6f);
            }
            else
            {
                bobWeight = Mathf.MoveTowards(bobWeight, 1f, Time.deltaTime * 6f);

                // 버스트 발생
                if (Time.time >= nextShakeTime && shakeTimer <= 0f)
                {
                    shakeTimer = shakeBurstDuration;

                    // 추천: 버스트는 yaw(좌우) 위주, pitch는 아주 약하게(또는 0)
                    shakeTargetYaw = Random.Range(-shakeAmplitudeDeg, shakeAmplitudeDeg);
                    shakeTargetPitch = Random.Range(-shakeAmplitudeDeg * 0.25f, shakeAmplitudeDeg * 0.25f);

                    ScheduleNextShake();
                }

                // 버스트 시간이 끝나면 목표를 0으로 (근데 부드럽게 돌아감)
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

            // 목표값으로 부드럽게 따라가게 (툭툭 튐 방지)
            shakeCurrentYaw = Mathf.Lerp(shakeCurrentYaw, shakeTargetYaw, shakeFadeSpeed * Time.deltaTime);
            shakeCurrentPitch = Mathf.Lerp(shakeCurrentPitch, shakeTargetPitch, shakeFadeSpeed * Time.deltaTime);

            // Bob(상하 반복) - pitch에만 주고, 이동 아닐 땐 weight로 0에 수렴
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
