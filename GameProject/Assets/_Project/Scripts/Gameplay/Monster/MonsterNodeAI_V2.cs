using System.Collections;
using UnityEngine;
using Game.NodeSystem;
using Game.Player;
using System.Collections.Generic;

namespace Game.Monster
{
    public class MonsterNodeAI_V2 : MonoBehaviour
    {
        private enum State  { Patrol, Detect, Chase, Attack, Disabled }

        [Header("Refs")]
        [SerializeField] private PlayerNodeMover playerNodeMover;
        [SerializeField] private PlayerHideController playerHideController;
        [SerializeField] private Node currentNode;
        [SerializeField] private PatrolPath patrolPath;
        [SerializeField] private Animator animator;
        [SerializeField] private EnemyFootstep footStepSound;
        private Renderer[] monsterRenderers;
        private Collider[] monsterColliders;

        [Header("Move")]
        [SerializeField] private float thinkInterval = 0.35f;
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float arriveDistance = 0.05f;


        [Header("Patrol")]
        [SerializeField] private int patrolLookAhead = 3;
        [SerializeField] private bool followPatrolByDefault = true;
        [SerializeField] private int recentNodeMemory = 3;

        [Header("Approach Rule")]
        [SerializeField] private float randomPickChance = 0; // 예측 불가성
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

        [Header("Spawn")]
        [SerializeField] private bool spawnOnStart = false;
        [SerializeField] private float minSpawnDelay = 5f;
        [SerializeField] private float maxSpawnDelay = 20f;

        private bool isSpawned = false;
        private bool spawnInitialized = false;

        private State state = State.Patrol;
        private Coroutine brain;
        private Coroutine spawnRoutine;
        private bool isMoving;
        private int patrolIndex = 0;
        private int patrolDir = 1; // 1 정방향, -1 역방향(필요하면 사용)
        private Node previousNode;
        private readonly List<Node> recentNodes = new List<Node>();

        #region Unity
        private void Awake()
        {
            
            if (playerNodeMover == null) playerNodeMover = FindAnyObjectByType<PlayerNodeMover>();

            if (playerHideController == null) playerHideController = FindAnyObjectByType<PlayerHideController>();

            if (playerNodeMover == null) Debug.LogError("[MonsterNodeAI_V2] PlayerNodeMover를 찾을 수 없습니다.");

            if (playerHideController == null) Debug.LogWarning("[MonsterNodeAI_V2] PlayerHideController를 찾을 수 없습니다.");
        
            if (patrolPath == null) patrolPath = FindAnyObjectByType<PatrolPath>();
            if (animator == null)  animator = GetComponentInChildren<Animator>();
            if (footStepSound== null) footStepSound = FindAnyObjectByType<EnemyFootstep>();

            monsterRenderers = GetComponentsInChildren<Renderer>(true);
            monsterColliders = GetComponentsInChildren<Collider>(true);
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
                Debug.LogWarning(
                    $"{name} : CurrentNode가 지정되지 않았습니다."
                );

                return;
            }

            isSpawned = false;

            SetMonsterVisible(false);

            if (brain != null)
                StopCoroutine(brain);

            if (spawnRoutine != null)
                StopCoroutine(spawnRoutine);

            spawnRoutine = StartCoroutine(SpawnRoutine());
        }

        private void Start()
        {
            Initialize();
        }

        private float DistanceToPlayer()
        {
            if (playerNodeMover == null)
                return float.MaxValue;

            return Vector3.Distance(transform.position, playerNodeMover.transform.position);
        }

        #region FSM
        private IEnumerator BrainLoop()
        {
            Debug.Log("[Monster V2] BrainLoop 시작");
            while (true)
            {
                if (state == State.Disabled)
                {
                    yield return null;
                    continue;
                }

                if (playerNodeMover == null || currentNode == null)
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

            Debug.Log(
                $"[Monster V2] State : {state} -> {next}"
            );

            state = next;
        }

        private void UpdatePatrol(float distance)
        {
            animator?.SetBool("isDetected", false);

            // 플레이어가 숨어있으면 감지하지 않음
            if (playerHideController != null && playerHideController.IsHiding)
                return;

            if (distance <= chaseDistance)
            {
                ChangeState(State.Detect);
            }
        }

        private void UpdateChase(float distance)
        {
            animator?.SetBool("isDetected", true);

            if (playerHideController != null &&
                playerHideController.IsHiding)
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

            yield return new WaitForSeconds(detectDelay);

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
            if (currentNode == null)
                return null;

            // Patrol
            if (state == State.Patrol)
            {
                return GetNextPatrolNodeByNeighbors();
            }

            // Chase
            if (state == State.Chase)
            {
                Node playerNode = GetPlayerNode();

                Debug.Log(
                    $"[Monster V2] Chase / Current = {currentNode?.name} / Player = {playerNode?.name}"
                );

                if (playerNode == null)
                    return null;

                Node next = ChooseNextNodeApproach(
                    currentNode,
                    playerNode.Position
                );

                Debug.Log(
                    $"[Monster V2] Chase Next = {next?.name}"
                );

                return next;
            }

            // Patrol / Chase가 아닌 상태에서는 이동하지 않음
            return null;
        }

        private Node GetNextPatrolNodeByNeighbors()
        {
            if (currentNode == null)
                return null;

            var neighbors = currentNode.Neighbors;

            if (neighbors == null || neighbors.Count == 0)
                return null;

            // 1차 후보:
            // 최근 방문 Node와 previousNode를 모두 제외
            List<Node> candidates = new List<Node>();

            for (int i = 0; i < neighbors.Count; i++)
            {
                Node node = neighbors[i];

                if (node == null || !node.IsActive)
                    continue;

                if (node == previousNode)
                    continue;

                if (recentNodes.Contains(node))
                    continue;

                candidates.Add(node);
            }

            // 2차 후보:
            // 최근 방문 기록은 무시하고 previousNode만 제외
            if (candidates.Count == 0)
            {
                for (int i = 0; i < neighbors.Count; i++)
                {
                    Node node = neighbors[i];

                    if (node == null || !node.IsActive)
                        continue;

                    if (node == previousNode)
                        continue;

                    candidates.Add(node);
                }
            }

            // 3차 후보:
            // 정말 다른 선택지가 없다면 모든 활성 Node 허용
            if (candidates.Count == 0)
            {
                for (int i = 0; i < neighbors.Count; i++)
                {
                    Node node = neighbors[i];

                    if (node != null && node.IsActive)
                        candidates.Add(node);
                }
            }

            if (candidates.Count == 0)
                return null;

            return candidates[Random.Range(0, candidates.Count)];
        }

        private IEnumerator MoveNextNode()
        {
            if (isMoving)
                yield break;

            Node next = GetNextNode();

            if (next == null || next == currentNode)
                yield break;

            yield return MoveToNode(next);

            previousNode = currentNode;
            currentNode = next;

            recentNodes.Add(currentNode);

            while (recentNodes.Count > recentNodeMemory)
            {
                recentNodes.RemoveAt(0);
            }
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

        private Node ChooseNextNodeWithPatrol(Node from, Vector3 playerPosition)
        {
            bool hasPatrol =
                patrolPath != null &&
                patrolPath.IsValid &&
                followPatrolByDefault;

            if (state == State.Chase)
                return ChooseNextNodeApproach(from, playerPosition);

            if (!hasPatrol)
                return ChooseNextNodeApproach(from, playerPosition);

            Node target = GetPatrolTarget(from, playerPosition);

            if (target == null)
                return from;

            return ChooseNextNodeApproach(from, target.Position);
        }

        private Node ChooseNextNodeApproach(Node from, Vector3 playerPosition)
        {
            if (from == null)
                return null;

            Node playerNode = GetPlayerNode();

            if (playerNode == null)
                return null;

            // 이미 플레이어 Node에 도착했다면 이동하지 않음
            if (from == playerNode)
                return from;

            Node nextNode = FindNextNodeByBFS(from, playerNode);

            if (nextNode != null)
                return nextNode;

            // BFS 경로를 찾지 못했다면 기존처럼
            // 현재 Node의 활성 Neighbor 중 하나를 선택
            var neighbors = from.Neighbors;

            if (neighbors == null || neighbors.Count == 0)
                return from;

            List<Node> candidates = new List<Node>();

            for (int i = 0; i < neighbors.Count; i++)
            {
                Node node = neighbors[i];

                if (node == null || !node.IsActive)
                    continue;

                candidates.Add(node);
            }

            if (candidates.Count == 0)
                return from;

            return candidates[Random.Range(0, candidates.Count)];
        }

        private Node FindNextNodeByBFS(Node startNode, Node targetNode)
        {
            if (startNode == null || targetNode == null)
                return null;

            Queue<Node> queue = new Queue<Node>();
            HashSet<Node> visited = new HashSet<Node>();
            Dictionary<Node, Node> previous = new Dictionary<Node, Node>();

            queue.Enqueue(startNode);
            visited.Add(startNode);

            while (queue.Count > 0)
            {
                Node current = queue.Dequeue();

                if (current == targetNode)
                    break;

                var neighbors = current.Neighbors;

                if (neighbors == null)
                    continue;

                for (int i = 0; i < neighbors.Count; i++)
                {
                    Node next = neighbors[i];

                    if (next == null)
                        continue;

                    if (!next.IsActive)
                        continue;

                    if (visited.Contains(next))
                        continue;

                    visited.Add(next);
                    previous[next] = current;

                    queue.Enqueue(next);
                }
            }

            if (!visited.Contains(targetNode))
            {
                string visitedLog = "";

                foreach (Node node in visited)
                {
                    if (node == null)
                        continue;

                    visitedLog += node.name + " -> ";
                }

                Debug.Log(
                    $"[Monster BFS] 경로 없음\n" +
                    $"Start = {startNode.name}\n" +
                    $"Target = {targetNode.name}\n" +
                    $"Visited = {visitedLog}"
                );

                return null;
            }

            // Target → Start 방향으로 경로 복원
            List<Node> path = new List<Node>();

            Node step = targetNode;

            path.Add(step);

            while (previous.ContainsKey(step))
            {
                step = previous[step];
                path.Add(step);
            }

            // Start → Target 방향으로 뒤집기
            path.Reverse();

            // 전체 경로 로그
            string pathLog = "";

            for (int i = 0; i < path.Count; i++)
            {
                pathLog += path[i].name;

                if (i < path.Count - 1)
                    pathLog += " -> ";
            }

            Debug.Log(
                $"[Monster BFS] Start = {startNode.name} / Target = {targetNode.name}\n" +
                $"[Monster BFS] Path = {pathLog}"
            );

            // Start 다음의 첫 번째 Node가 실제 이동할 Node
            if (path.Count >= 2)
            {
                Node nextNode = path[1];

                Debug.Log(
                    $"[Monster BFS] Next = {nextNode.name}"
                );

                return nextNode;
            }

            // Start == Target인 경우
            return startNode;
        }

        private Node GetPlayerNode()
        {
            if (playerNodeMover == null)
                return null;

            return playerNodeMover.CurrentNode;
        }

        private Node GetPatrolTarget(Node from, Vector3 playerPosition)
        {
            Node patrolNext = GetNextPatrolNode();
            Node patrolBestAhead = GetBestPatrolNodeAheadTowardPlayer(playerPosition);

            Node chosen = state == State.Patrol ? patrolNext : patrolBestAhead;

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

        private Node GetBestPatrolNodeAheadTowardPlayer(Vector3 playerPosition)
        {
            if (patrolPath == null || !patrolPath.IsValid)
                return null;

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

                float d = Vector3.Distance(n.Position, playerPosition);
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
            if (playerNodeMover == null)
                return false;

            if (playerHideController != null && playerHideController.IsHiding)
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

        private IEnumerator SpawnRoutine()
        {
            if (spawnOnStart)
            {
                SpawnMonster();
                yield break;
            }

            float delay = Random.Range(
                minSpawnDelay,
                maxSpawnDelay
            );

            yield return new WaitForSeconds(delay);

            SpawnMonster();
        }

        private void SpawnMonster()
        {
            if (isSpawned)
                return;

            isSpawned = true;

            if (spawnNode != null)
            {
                currentNode = spawnNode;
                transform.position = spawnNode.Position;
            }

            previousNode = null;
            recentNodes.Clear();

            AlignPatrolIndexToCurrentNode();

            SetMonsterVisible(true);

            ChangeState(State.Patrol);

            if (brain != null)
                StopCoroutine(brain);

            brain = StartCoroutine(BrainLoop());

            Debug.Log(
                $"[Monster V2] Spawned after random delay."
            );
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

        private void Respawn()
        {
            previousNode = null;
            recentNodes.Clear();

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

        private void SetMonsterVisible(bool visible)
        {
            if (monsterRenderers != null)
            {
                foreach (Renderer renderer in monsterRenderers)
                {
                    if (renderer != null)
                        renderer.enabled = visible;
                }
            }

            if (monsterColliders != null)
            {
                foreach (Collider collider in monsterColliders)
                {
                    if (collider != null)
                        collider.enabled = visible;
                }
            }
        }

        #endregion
    }
}
