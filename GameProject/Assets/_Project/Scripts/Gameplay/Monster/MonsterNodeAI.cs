using System.Collections;
using UnityEngine;
using Game.NodeSystem;
using Game.Player;

namespace Game.Monster
{
    public class MonsterNodeAI : MonoBehaviour
    {
        private enum State { Approaching, Attacking, Disabled }

        [Header("Refs")]
        [SerializeField] private PlayerNodeMover player;
        [SerializeField] private Node currentNode;
        [SerializeField] private MonsterAggro aggroSystem;
        [SerializeField] private PatrolPath patrolPath;

        [Header("Move")]
        [SerializeField] private float thinkInterval = 0.35f;
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float arriveDistance = 0.05f;


        [Header("Patrol Influence")]
        [SerializeField] private float patrolBias = 0.75f; // 0=완전 플레이어추적, 1=완전 패트롤
        [SerializeField] private int patrolLookAhead = 3;  // 경로 앞으로 몇 칸까지 “가까워지는 노드” 후보로 볼지
        [SerializeField] private float detourChanceAtHighAggro = 0.35f; // aggro 높을 때 경로 이탈 확률
        [SerializeField] private bool followPatrolByDefault = true;

        [Header("Approach Rule")]
        [SerializeField] private float randomPickChance = 0.25f; // 예측 불가성
        [SerializeField] private int maxDistanceStepPerThink = 1; // 한 번에 너무 확 좁히지 않게(점진)

        [Header("Attack")]
        [SerializeField] private float attackDistance = 0.9f;

        [Header("Disable/Respawn")]
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private float defaultDisableDuration = 6.0f;

        private State state = State.Approaching;
        private Coroutine brain;

        private int patrolIndex = 0;
        private int patrolDir = 1; // 1 정방향, -1 역방향(필요하면 사용)

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlayerNodeMover>();
            if (aggroSystem == null) aggroSystem = GetComponent<MonsterAggro>();
            if (patrolPath == null) patrolPath = FindAnyObjectByType<PatrolPath>();
        }

        private void OnEnable()
        {
            EventBus.OnMonsterDisableRequested += OnDisableRequested;
        }

        private void OnDisable()
        {
            EventBus.OnMonsterDisableRequested -= OnDisableRequested;
        }

        private void Start()
        {
            if (currentNode == null)
                AutoBindNearestNode(transform.position);

            AlignPatrolIndexToCurrentNode();

            state = State.Approaching;
            brain = StartCoroutine(BrainLoop());
        }

        //“즉시 공격 가능” 판정은 여기 기준으로 제공
        public bool CanAttackNow(Transform target)
        {
            if (target == null) return false;
            return Vector3.Distance(transform.position, target.position) <= attackDistance
                   && aggroSystem != null && aggroSystem.Aggro >= 100f;
        }

        private IEnumerator BrainLoop()
        {
            while (true)
            {
                if (state == State.Disabled)
                {
                    yield return null;
                    continue;
                }

                if (player == null || currentNode == null)
                {
                    yield return null;
                    continue;
                }

                // 어그로에 따라 이동 템포/속도 증가(점진적 위협 강화)
                float a01 = aggroSystem != null ? Mathf.Clamp01(aggroSystem.Aggro / 100f) : 0f;
                float think = Mathf.Lerp(0.55f, 0.18f, a01);
                float speed = Mathf.Lerp(2.8f, 4.8f, a01);
                thinkInterval = think;
                moveSpeed = speed;

                // 공격 조건: 거리 && 공격성 100
                float distToPlayer = Vector3.Distance(transform.position, player.transform.position);
                bool canAttack = distToPlayer <= attackDistance && aggroSystem != null && aggroSystem.Aggro >= 100f;

                if (canAttack)
                {
                    state = State.Attacking;
                    DoAttackPrototype();
                    // 공격 후 공격성 리셋(“100일때 공격” 규칙 자연스러움)
                    aggroSystem.ResetAggro();
                    state = State.Approaching;

                    yield return new WaitForSeconds(thinkInterval);
                    continue;
                }

                // 이동 선택
                Node playerNode = player.CurrentNode;
                if (playerNode != null)
                {
                    Node next = ChooseNextNodeWithPatrol(currentNode, playerNode);
                    if (next != null && next != currentNode)
                    {
                        yield return MoveToNode(next);
                        currentNode = next;
                    }
                }


                yield return new WaitForSeconds(thinkInterval);
            }
        }

        private Node ChooseNextNodeApproach(Node from, Node playerNode)
        {
            // 현재 거리(월드 거리 기준으로 단계 유사 처리)
            float curDist = Vector3.Distance(from.Position, playerNode.Position);

            // 후보: 이웃 노드들 중 “조금 더 가까워지는” 후보를 모음
            var neighbors = from.Neighbors;
            if (neighbors == null || neighbors.Count == 0) return from;

            Node best = from;
            float bestDist = curDist;

            // 랜덤 픽(예측 불가성)
            if (Random.value < randomPickChance)
            {
                // 활성 이웃 중 하나
                for (int t = 0; t < 6; t++)
                {
                    Node r = neighbors[Random.Range(0, neighbors.Count)];
                    if (r != null && r.IsActive) return r;
                }
            }

            // 점진 접근: “너무 확 좁히지 않도록” 제한
            // world 거리라 완벽한 단계는 아니지만 체감은 충분히 점진적으로 됨.
            for (int i = 0; i < neighbors.Count; i++)
            {
                Node n = neighbors[i];
                if (n == null || !n.IsActive) continue;

                float d = Vector3.Distance(n.Position, playerNode.Position);

                // 무조건 최단으로 가면 직추적 느낌 → “조금 가까워지는 후보”만 우선
                bool closer = d < bestDist;
                bool notTooHugeJump = (bestDist - d) <= (maxDistanceStepPerThink * 10f);
                // ↑ 이 숫자는 그래프 스케일에 맞춰 조절 (노드 간격이 2~5면 10f 넉넉)

                if (closer && notTooHugeJump)
                {
                    bestDist = d;
                    best = n;
                }
            }

            // 가까워지는 후보가 없다면 그냥 활성 이웃 중 하나(막히지 않게)
            if (best == from)
            {
                for (int i = 0; i < neighbors.Count; i++)
                {
                    Node n = neighbors[i];
                    if (n != null && n.IsActive) return n;
                }
            }

            return best;
        }

        private IEnumerator MoveToNode(Node target)
        {
            Vector3 dest = target.Position;
            while (Vector3.Distance(transform.position, dest) > arriveDistance)
            {
                transform.position = Vector3.MoveTowards(transform.position, dest, moveSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = dest;
        }

        private void DoAttackPrototype()
        {
            Debug.Log("[Monster] Attack! (Aggro=100) -> GameOver Prototype");
            // TODO: 5번(GameManager) 건들일 때 여기서 GameOver 호출로 연결
        }

        private void OnDisableRequested(float duration)
        {
            if (state == State.Disabled) return;

            float dur = duration > 0f ? duration : defaultDisableDuration;
            StartCoroutine(DisableRoutine(dur));
        }

        private IEnumerator DisableRoutine(float duration)
        {
            state = State.Disabled;

            // 필드에서 제거(가장 간단)
            gameObject.SetActive(false);

            // 비활성화 시간
            yield return new WaitForSecondsRealtime(duration);

            // 리젠: 위치 재배치 + 공격성 0
            if (respawnPoint != null)
            {
                transform.position = respawnPoint.position;
                AutoBindNearestNode(respawnPoint.position);
            }
            else
            {
                AutoBindNearestNode(transform.position);
            }

            if (aggroSystem != null) aggroSystem.ResetAggro();

            gameObject.SetActive(true);
            state = State.Approaching;
            EventBus.RaiseMonsterRespawned();
        }

        private void AutoBindNearestNode(Vector3 pos)
        {
            var nodes = FindObjectsByType<Node>(FindObjectsSortMode.None);
            if (nodes == null || nodes.Length == 0) return;

            Node best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < nodes.Length; i++)
            {
                float d = Vector3.Distance(pos, nodes[i].Position);
                if (d < bestDist) { bestDist = d; best = nodes[i]; }
            }

            if (best != null)
            {
                currentNode = best;
                transform.position = best.Position;
            }
        }
        private void AlignPatrolIndexToCurrentNode()
        {
            if (patrolPath == null || !patrolPath.IsValid || currentNode == null) return;

            var list = patrolPath.PathNodes;
            int best = 0;
            float bestDist = float.MaxValue;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                float d = Vector3.Distance(currentNode.Position, list[i].Position);
                if (d < bestDist) { bestDist = d; best = i; }
            }

            patrolIndex = best;
        }

        private Node ChooseNextNodeWithPatrol(Node from, Node playerNode)
        {
            float a01 = aggroSystem != null ? Mathf.Clamp01(aggroSystem.Aggro / 100f) : 0f;

            bool hasPatrol = patrolPath != null && patrolPath.IsValid && followPatrolByDefault;

            // 1) 소음 interestNode가 있고 유지 시간 내면: 고어그로일수록 detour 우선
            //bool hasInterest = interestNode != null && (Time.time - lastInterestTime <= interestHoldTime);
            //if (hasInterest && Random.value < Mathf.Lerp(0.15f, 0.75f, a01))
            //    return ChooseNextNodeApproach(from, interestNode);

            // 2) 헌트(aggro 높음)면 경로 이탈 확률 증가(플레이어 쪽 압박 강화)
            if (Random.value < Mathf.Lerp(0.05f, detourChanceAtHighAggro, a01))
                return ChooseNextNodeApproach(from, playerNode);

            // 3) 기본: Patrol 기반(경로 다음 vs 경로 스킵 중 플레이어에 가까운 것)
            if (hasPatrol)
            {
                Node patrolNext = GetNextPatrolNode();
                Node patrolBestAhead = GetBestPatrolNodeAheadTowardPlayer(playerNode);

                // a01 낮으면 patrolBias 높게(패트롤 위주), 높으면 patrolBias 낮게(플레이어 압박 위주)
                float dynamicBias = Mathf.Lerp(0.9f, 0.45f, a01);
                Node chosen = (Random.value < dynamicBias) ? patrolNext : patrolBestAhead;
                // patrolBias가 높을수록 “그냥 경로대로”, 낮을수록 “플레이어 가까운 경로 노드”로
                //Node chosen = (Random.value < patrolBias) ? patrolNext : patrolBestAhead;

                // chosen이 null이면 fallback
                if (chosen == null) chosen = patrolNext;
                if (chosen == null) chosen = from;

                // 경로 노드로 바로 점프하지 말고, 실제 이동은 인접 노드로 한 칸씩 (노드 그래프 유지)
                // chosen까지 가는 방향으로 “한 칸” 선택(그리디)
                return ChooseNextNodeApproach(from, chosen);
            }

            // 4) Patrol이 없으면 기존 접근 로직
            return ChooseNextNodeApproach(from, playerNode);
        }

        private Node GetNextPatrolNode()
        {
            if (patrolPath == null || !patrolPath.IsValid) return null;

            var list = patrolPath.PathNodes;
            int next = patrolIndex + patrolDir;

            if (patrolPath.Loop)
            {
                if (next >= list.Count) next = 0;
                if (next < 0) next = list.Count - 1;
            }
            else
            {
                // 루프가 아니면 끝에서 방향 반전
                if (next >= list.Count) { patrolDir = -1; next = list.Count - 2; }
                if (next < 0) { patrolDir = 1; next = 1; }
            }

            patrolIndex = next;
            return list[patrolIndex];
        }

        private Node GetBestPatrolNodeAheadTowardPlayer(Node playerNode)
        {
            if (patrolPath == null || !patrolPath.IsValid || playerNode == null) return null;

            var list = patrolPath.PathNodes;
            int count = list.Count;

            Node best = null;
            float bestDist = float.MaxValue;

            // “현재 인덱스 기준 앞으로 patrolLookAhead칸”에서 플레이어에 가장 가까운 경로 노드 선택
            for (int step = 1; step <= Mathf.Max(1, patrolLookAhead); step++)
            {
                int idx = patrolIndex + step * patrolDir;

                if (patrolPath.Loop)
                {
                    idx %= count;
                    if (idx < 0) idx += count;
                }
                else
                {
                    if (idx < 0 || idx >= count) break;
                }

                Node n = list[idx];
                if (n == null || !n.IsActive) continue;

                float d = Vector3.Distance(n.Position, playerNode.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n;
                }
            }

            // 후보가 없으면 그냥 다음 노드
            return best ?? list[patrolIndex];
        }

    }
}
