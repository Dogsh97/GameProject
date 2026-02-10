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

        [Header("Move")]
        [SerializeField] private float thinkInterval = 0.35f;
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float arriveDistance = 0.05f;

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

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlayerNodeMover>();
            if (aggroSystem == null) aggroSystem = GetComponent<MonsterAggro>();
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

                // 이동: “직추적”이 아니라 “점진 접근”
                Node playerNode = player.CurrentNode;
                if (playerNode != null)
                {
                    Node next = ChooseNextNodeApproach(currentNode, playerNode);
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
    }
}
