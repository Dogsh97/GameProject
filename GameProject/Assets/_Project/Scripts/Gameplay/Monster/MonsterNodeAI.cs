using System.Collections;
using UnityEngine;
using Game.NodeSystem;
using Game.Player;

namespace Game.Monster
{
    public class MonsterNodeAI : MonoBehaviour
    {
        private enum State { Idle, Chasing, Attacking }

        [Header("Refs")]
        [SerializeField] private PlayerNodeMover player;
        [SerializeField] private Node currentNode;

        [Header("Chase")]
        [SerializeField] private float thinkInterval = 0.35f;
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float arriveDistance = 0.05f;

        [Header("Attack")]
        [SerializeField] private float attackDistance = 0.9f; // 월드 거리 기반 임시 판정
        [SerializeField] private float attackCooldown = 1.0f;

        [SerializeField] private PlayerHideController hide;

        private State state = State.Idle;
        private Coroutine brainRoutine;
        private float nextAttackTime;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlayerNodeMover>();
            if (hide == null) hide = FindAnyObjectByType<PlayerHideController>();
        }

        private void Start()
        {
            if (currentNode == null)
                AutoBindNearestNode();

            state = State.Chasing;
            brainRoutine = StartCoroutine(BrainLoop());
        }

        private IEnumerator BrainLoop()
        {
            while (true)
            {
                if (player == null)
                {
                    yield return null;
                    continue;
                }

                // 숨기 중이면 추적 중단(최소 구현)
                if (hide != null && hide.IsHiding)
                {
                    // 공격만 막을지/추적만 막을지 선택 가능
                    // MVP: 추적 중단 + 공격도 못하게 하려면 dist 체크도 스킵
                    yield return new WaitForSeconds(thinkInterval);
                    continue;
                }

                // 1) 공격 판정(임시)
                float distToPlayer = Vector3.Distance(transform.position, player.transform.position);
                if (distToPlayer <= attackDistance && Time.time >= nextAttackTime)
                {
                    state = State.Attacking;
                    nextAttackTime = Time.time + attackCooldown;

                    // 임시 GameOver
                    Debug.Log("[Monster] Attack! Game Over (Prototype)");
                    // 여기서 GameManager.GameOver() 같은 걸로 연결 예정
                    yield return null;

                    state = State.Chasing;
                }

                // 2) 추적(노드 기반)
                if (state == State.Chasing)
                {
                    Node targetPlayerNode = player.CurrentNode;
                    if (targetPlayerNode != null && currentNode != null)
                    {
                        // "진짜 최단경로"는 2단계(상태머신/고도화) 때.
                        // 지금은 이웃 중 "플레이어 노드에 더 가까워지는" 노드를 선택.
                        Node next = ChooseNextNodeGreedy(currentNode, targetPlayerNode);
                        if (next != null && next != currentNode)
                        {
                            yield return MoveToNode(next);
                            currentNode = next;
                        }
                    }
                }

                yield return new WaitForSeconds(thinkInterval);
            }
        }

        private Node ChooseNextNodeGreedy(Node from, Node playerNode)
        {
            Node best = from;
            float bestDist = Vector3.Distance(from.Position, playerNode.Position);

            var neighbors = from.Neighbors;
            for (int i = 0; i < neighbors.Count; i++)
            {
                Node n = neighbors[i];
                if (n == null || !n.IsActive) continue;

                float d = Vector3.Distance(n.Position, playerNode.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n;
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

        private void AutoBindNearestNode()
        {
            var nodes = FindObjectsByType<Node>(FindObjectsSortMode.None);
            if (nodes == null || nodes.Length == 0) return;

            Node best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < nodes.Length; i++)
            {
                float d = Vector3.Distance(transform.position, nodes[i].Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = nodes[i];
                }
            }

            if (best != null)
            {
                currentNode = best;
                transform.position = best.Position;
            }
        }
    }
}
