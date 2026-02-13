using System;
using System.Collections;
using UnityEngine;
using Game.NodeSystem;

namespace Game.Player
{
    public class PlayerNodeMover : MonoBehaviour
    {
        private enum MoveState { Idle, Delay, Moving }

        [Header("Raycast")]
        [SerializeField] private Camera cam;
        [SerializeField] private LayerMask nodeLayerMask = ~0;

        [Header("Move Tuning")]
        [SerializeField] private float moveDelay = 0.6f;
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float arriveDistance = 0.05f;

        [Header("Runtime")]
        [SerializeField] private Node currentNode;

        private MoveState state = MoveState.Idle;
        private Coroutine moveRoutine;
        
        public bool IsBusy => state != MoveState.Idle;
        public bool InputEnabled { get; set; } = true;
        public Node CurrentNode => currentNode;

        public event Action OnMoveStarted;
        public event Action OnMoveFinished;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        private void Start()
        {
            if (currentNode == null)
                AutoBindNearestNode();
        }

        private void Update()
        {
            if (!InputEnabled) return;

            // Legacy Input: 좌클릭
            if (Input.GetMouseButtonDown(0))
                TrySelectNodeByClick();

            // 취소(우클릭 or ESC) : 딜레이 중에만 취소 가능 (기획서 기준)
            if (state == MoveState.Delay && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
                CancelMove();
        }

        private void AutoBindNearestNode()
        {
            var nodes = FindObjectsByType<Game.NodeSystem.Node>(FindObjectsSortMode.None);
            if (nodes == null || nodes.Length == 0) return;

            Game.NodeSystem.Node best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < nodes.Length; i++)
            {
                float d = Vector3.Distance(transform.position, nodes[i].Position);
                if (d < bestDist) { bestDist = d; best = nodes[i]; }
            }

            if (best != null)
                SetCurrentNode(best);
        }

        private void TrySelectNodeByClick()
        {

            if (state != MoveState.Idle) return;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Debug.DrawRay(ray.origin, ray.direction * 200f, Color.red, 1f);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f, nodeLayerMask, QueryTriggerInteraction.Ignore))
                return;
            Debug.Log("Click!");
            Debug.Log($"Hit: {hit.collider.name}");

            NodeSelectable selectable = hit.collider.GetComponentInParent<NodeSelectable>();
            if (selectable == null || selectable.Node == null) return;

            Node target = selectable.Node;

            // 규칙: 활성 노드만
            if (!target.IsActive) return;

            // 규칙: 현재 노드가 있으면 “연결된 노드만”
            if (currentNode != null && !currentNode.IsNeighbor(target))
                return;

            BeginMoveTo(target);
        }

        private void BeginMoveTo(Node target)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(MoveFlow(target));
        }

        private IEnumerator MoveFlow(Node target)
        {
            // 1) Delay
            state = MoveState.Delay;

            float t = 0f;
            while (t < moveDelay)
            {
                t += Time.deltaTime;
                yield return null;
            }
            OnMoveStarted?.Invoke();
            // 2) Move
            state = MoveState.Moving;

            Vector3 dest = target.Position;
            while (Vector3.Distance(transform.position, dest) > arriveDistance)
            {
                transform.position = Vector3.MoveTowards(transform.position, dest, moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = dest;
            currentNode = target;
            OnMoveFinished?.Invoke();
            // 3) Done
            state = MoveState.Idle;
            moveRoutine = null;
        }

        private void CancelMove()
        {
            if (state != MoveState.Delay) return;

            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
            }

            state = MoveState.Idle;
        }

        // 디버그/초기화용: 스폰 시 현재 노드를 강제로 지정하고 싶으면 호출
        public void SetCurrentNode(Node node)
        {
            currentNode = node;
            if (node != null)
                transform.position = node.Position;
        }
    }
}
