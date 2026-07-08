using System.Collections;
using UnityEngine;
using Game.NodeSystem;
using Game.Player;

namespace Game.Monster
{
    public class MonsterNodeAI_V2 : MonoBehaviour
    {
        private enum State  { Patrol, Detect, Chase, Attack, Disabled }

        [Header("Refs")]
        [SerializeField] private PlayerNodeMover player;
        [SerializeField] private Node currentNode;
        [SerializeField] private PatrolPath patrolPath;
        [SerializeField] private Animator animator;
        [SerializeField] private EnemyFootstep footStepSound;

        [Header("Move")]
        [SerializeField] private float thinkInterval = 0.35f;
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float arriveDistance = 0.05f;


        [Header("Patrol")]
        [SerializeField] private int patrolLookAhead = 3;
        [SerializeField] private bool followPatrolByDefault = true;

        [Header("Approach Rule")]
        [SerializeField] private float randomPickChance = 0.25f; // 예측 불가성
        [SerializeField] private int maxDistanceStepPerThink = 1; // 한 번에 너무 확 좁히지 않게(점진)

        [Header("Detection")]
        [SerializeField] private float chaseDistance = 6f;
        [SerializeField] private float detectDelay = 2f;

        [Header("Attack")]
        [SerializeField] private float attackDistance = 0.9f;

        [Header("Disable/Respawn")]
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private float defaultDisableDuration = 6.0f;
        [SerializeField] private Node spawnNode;

        private State state = State.Patrol;
        private Coroutine brain;
        private bool isMoving;
        private int patrolIndex = 0;
        private int patrolDir = 1; // 1 정방향, -1 역방향(필요하면 사용)

        #region Unity
        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlayerNodeMover>();

            if (patrolPath == null) patrolPath = FindAnyObjectByType<PatrolPath>();
            if (animator == null)  animator = GetComponentInChildren<Animator>();
            if (footStepSound== null) footStepSound = FindAnyObjectByType<EnemyFootstep>();
        }

        private void OnEnable()
        {
            EventBus.OnMonsterDisableRequested += OnDisableRequested;
        }

        private void OnDisable()
        {
            EventBus.OnMonsterDisableRequested -= OnDisableRequested;
        }
        #endregion

        private void Initialize()
        {
            if (spawnNode != null)
            {
                currentNode = spawnNode;
                transform.position = spawnNode.Position;
            }
            else if (currentNode == null)
            {
                Debug.LogWarning($"{name} : CurrentNode가 지정되지 않았습니다.");
                return;
                //AutoBindNearestNode(transform.position);
            }

            AlignPatrolIndexToCurrentNode();

            ChangeState(State.Patrol);

            if (brain != null)
                StopCoroutine(brain);

            brain = StartCoroutine(BrainLoop());
        }

        private void Start()
        {
            Initialize();
        }

        private float DistanceToPlayer()
        {
            if (player == null)
                return float.MaxValue;

            return Vector3.Distance(transform.position, player.transform.position);
        }

        #region FSM
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
                UpdateState();

                switch (state)
                {
                    case State.Patrol:
                    case State.Chase:
                        yield return HandleMove();
                        break;

                    case State.Attack:
                        yield return HandleAttack();
                        break;

                    case State.Detect:
                        yield return HandleDetect();
                        break;
                }

                yield return new WaitForSeconds(thinkInterval);
            }
        }

        private void ChangeState(State next)
        {
            if (state == next)
                return;

            state = next;
        }

        private void UpdatePatrol(float distance)
        {
            animator?.SetBool("isDetected", false);

            if (distance <= chaseDistance)
            {
                ChangeState(State.Detect);
            }
        }

        private void UpdateChase(float distance)
        {
            animator?.SetBool("isDetected", true);

            if (distance > chaseDistance)
            {
                ChangeState(State.Patrol);
                return;
            }

            if (distance <= attackDistance)
                ChangeState(State.Attack);
        }

        private void UpdateAttack(float distance)
        {
            if (distance > attackDistance)
                ChangeState(State.Chase);
        }

        private void UpdateState()
        {
            float distance = DistanceToPlayer();

            switch (state)
            {
                case State.Patrol:
                    UpdatePatrol(distance);
                    break;

                case State.Chase:
                    UpdateChase(distance);
                    break;

                case State.Attack:
                    UpdateAttack(distance);
                    break;

                case State.Detect:
                    break;
            }
        }

        private IEnumerator HandleDetect()
        {
            animator.SetBool("isDetected", true);

            StopWalking();

            yield return new WaitForSeconds(1.8f);

            animator.SetBool("isDetected", false);

            ChangeState(State.Chase);
        }

        private IEnumerator HandleMove()
        {
            yield return MoveNextNode();
        }

        private IEnumerator HandleAttack()
        {
            if (!CanAttack())
            {
                ChangeState(State.Chase);
                yield break;
            }

            animator?.ResetTrigger("Attack");
            animator?.SetTrigger("Attack");

            DoAttackPrototype();

            yield return new WaitForSeconds(thinkInterval);

            ChangeState(State.Chase);
        }
        #endregion

        #region Movement
        private Node GetNextNode()
        {
            if (player == null)
                return null;

            Node playerNode = player.CurrentNode;

            if (playerNode == null)
                return null;

            return ChooseNextNodeWithPatrol(currentNode, playerNode);
        }

        private IEnumerator MoveNextNode()
        {
            if (isMoving)
                yield break;

            Node next = GetNextNode();

            if (next == null || next == currentNode)
                yield break;

            yield return MoveToNode(next);

            currentNode = next;
        }

        private void Move(Vector3 target)
        {
            Vector3 direction = target - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }

            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        }

        private void StartWalking()
        {
            footStepSound?.SetWalking(true);
            animator?.SetFloat("MoveSpeed", 1f);
        }

        private void StopWalking()
        {
            footStepSound?.SetWalking(false);
            animator?.SetFloat("MoveSpeed", 0f);
        }

        private IEnumerator MoveToNode(Node target)
        {
            isMoving = true;

            Vector3 dest = target.Position;

            while (Vector3.Distance(transform.position, dest) > arriveDistance)
            {
                Move(dest);

                StartWalking();

                yield return null;
            }

            transform.position = dest;

            StopWalking();

            isMoving = false;
        }

        private Node ChooseNextNodeWithPatrol(Node from, Node playerNode)
        {
            bool hasPatrol =
                patrolPath != null &&
                patrolPath.IsValid &&
                followPatrolByDefault;

            if (state == State.Chase)
                return ChooseNextNodeApproach(from, playerNode);

            if (!hasPatrol)
                return ChooseNextNodeApproach(from, playerNode);

            Node target = GetPatrolTarget(from, playerNode);

            return ChooseNextNodeApproach(from, target);
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

        private Node GetPatrolTarget(Node from, Node playerNode)
        {
            Node patrolNext = GetNextPatrolNode();
            Node patrolBestAhead = GetBestPatrolNodeAheadTowardPlayer(playerNode);

            Node chosen = state == State.Patrol
                ? patrolNext
                : patrolBestAhead;

            if (chosen == null)
                chosen = patrolNext;

            if (chosen == null)
                chosen = from;

            return chosen;
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
        #endregion

        #region Combat
        private bool CanAttack()
        {
            if (player == null)
                return false;

            return DistanceToPlayer() <= attackDistance;
        }

        //“즉시 공격 가능” 판정은 여기 기준으로 제공
        public bool CanAttackNow(Transform target)
        {
            if (target == null)
                return false;

            return Vector3.Distance(transform.position,target.position) <= attackDistance;
        }

        private void DoAttackPrototype()
        {
            Debug.Log("[Monster] Attack!");
        }

        #endregion

        #region Utility
        

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

        private void Respawn()
        {
            if (spawnNode != null)
            {
                currentNode = spawnNode;
                transform.position = spawnNode.Position;
            }
            else if (respawnPoint != null)
            {
                transform.position = respawnPoint.position;
                AutoBindNearestNode(respawnPoint.position);
            }
            else
            {
                AutoBindNearestNode(transform.position);
            }
        }

        private IEnumerator DisableRoutine(float duration)
        {
            ChangeState(State.Disabled);

            // 필드에서 제거(가장 간단)
            gameObject.SetActive(false);

            // 비활성화 시간
            yield return new WaitForSecondsRealtime(duration);

            Respawn();

            gameObject.SetActive(true);
            ChangeState(State.Patrol);
            EventBus.RaiseMonsterRespawned();
        }

        private void OnDisableRequested(float duration)
        {
            if (state == State.Disabled) return;

            float dur = duration > 0f ? duration : defaultDisableDuration;
            StartCoroutine(DisableRoutine(dur));
        }

        #endregion
    }
}
