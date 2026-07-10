using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.NodeSystem;

namespace Game.Monster
{
    public class MonsterRouteAI : MonoBehaviour
    {

        public enum State
        {
            Waiting,
            Moving,
            Attack
        }

        [Header("AI Difficulty")]
        [SerializeField]
        [Range(0, 20)]
        private int aiLevel = 5;

        [SerializeField] private MonsterRoute route;

        [SerializeField] private Animator animator;

        [SerializeField] private EnemyFootstep footstep;

        [SerializeField] private float moveSpeed = 3f;

        [SerializeField] private float minSpawnDelay = 10f;

        [SerializeField] private float maxSpawnDelay = 20f;

        [SerializeField] private float arriveDistance = 0.05f;

        [SerializeField] private float thinkTime = 0.5f;

        private State state;

        private int currentIndex;

        public System.Action<int> OnNodeChanged;

        public System.Action OnMonsterSpawn;

        private void Start()
        {
            if (route == null || route.Count == 0)
            {
                Debug.LogError("MonsterRoute가 설정되지 않았습니다.");
                enabled = false;
                return;
            }

            MonsterRouteNode startNode = route.GetNode(0);

            if (startNode == null || startNode.node == null)
            {
                Debug.LogError("시작 노드가 없습니다.");
                enabled = false;
                return;
            }

            currentIndex = 0;

            transform.position = startNode.node.Position;

            StartCoroutine(MainLoop());
        }

        private void PrintState(string message)
        {
            Debug.Log($"[Monster] {message}");
        }

        private IEnumerator SpawnPhase()
        {
            state = State.Waiting;

            animator.SetFloat("MoveSpeed", 0f);

            float delay = Random.Range(minSpawnDelay, maxSpawnDelay);

            yield return new WaitForSeconds(delay);

            currentIndex = 0;

            MonsterRouteNode startNode = route.GetNode(currentIndex);

            if (startNode == null || startNode.node == null)
                yield break;

            transform.position = startNode.node.Position;

            animator.SetBool("isDetected", true);

            OnMonsterSpawn?.Invoke();

            yield return new WaitForSeconds(2f);

            animator.SetBool("isDetected", false);

            PrintState("Spawn");
        }

        private IEnumerator RoutePhase()
        {
            while (currentIndex < route.Count - 1)
            {
                yield return WaitNode();

                if (!CanMove())
                {
                    PrintState("Stay");
                    continue;
                }

                yield return new WaitForSeconds(thinkTime);

                yield return MoveNextNode();
            }
        }

        private IEnumerator AttackPhase()
        {
            state = State.Attack;

            animator.SetTrigger("Attack");

            PrintState("Attack");

            yield return new WaitForSeconds(2f);

            Debug.Log("Game Over");

            gameObject.SetActive(false);
        }

        private IEnumerator MainLoop()
        {
            while (true)
            {
                yield return SpawnPhase();

                yield return RoutePhase();

                yield return AttackPhase();
            }
        }

        public MonsterRouteNode CurrentRouteNode
        {
            get
            {
                return route.GetNode(currentIndex);
            }
        }

        public int CurrentNodeIndex
        {
            get
            {
                return currentIndex;
            }
        }

        public State CurrentState
        {
            get
            {
                return state;
            }
        }

        private IEnumerator WaitNode()
        {
            state = State.Waiting;

            animator.SetFloat("MoveSpeed", 0);

            MonsterRouteNode node = route.GetNode(currentIndex);

            if (node == null)
                yield break;

            float wait = Random.Range(node.minWait, node.maxWait);

            yield return new WaitForSeconds(wait);
        }

        private bool CanMove()
        {
            MonsterRouteNode node = route.GetNode(currentIndex);

            if (node == null)
                return false;

            float chance = node.moveChance;

            chance += aiLevel * 0.02f;

            chance = Mathf.Clamp01(chance);

            return Random.value <= chance;
        }

        private IEnumerator MoveNextNode()
        {
            currentIndex++;

            MonsterRouteNode nextNode = route.GetNode(currentIndex);

            if (nextNode == null || nextNode.node == null)
                yield break;

            yield return MoveToNode(nextNode);
        }

        private void ArriveNode(MonsterRouteNode routeNode)
        {
            animator.SetFloat("MoveSpeed", 0f);

            footstep?.SetWalking(false);

            OnNodeChanged?.Invoke(currentIndex);

            PrintState($"Arrive Node : {currentIndex}");
        }

        private IEnumerator MoveToNode(MonsterRouteNode routeNode)
        {
            Node target = routeNode.node;

            state = State.Moving;

            animator.SetFloat("MoveSpeed", 1f);

            footstep?.SetWalking(true);

            Vector3 dest = target.Position;

            while (Vector3.Distance(transform.position, dest) > arriveDistance)
            {
                Vector3 dir = dest - transform.position;
                dir.y = 0f;

                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion rot =
                        Quaternion.LookRotation(dir);

                    transform.rotation =
                        Quaternion.Slerp(
                            transform.rotation,
                            rot,
                            8f * Time.deltaTime);
                }

                transform.position =
                    Vector3.MoveTowards(
                        transform.position,
                        dest,
                        moveSpeed * Time.deltaTime);

                yield return null;
            }

            ArriveNode(routeNode);
        }

       
    }

}
