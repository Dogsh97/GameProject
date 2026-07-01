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

        [Header("이동 인디케이터")]
        [SerializeField] private GameObject moveIndicator; // 이동 가능 심볼 오브젝트

        [Header("Runtime")]
        [SerializeField] private Node currentNode;

        private MoveState state = MoveState.Idle;
        private Coroutine moveRoutine;
        private Node hoveredNode;
        
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
            if (InGameMenuController.isPaused) return; //메뉴 활성화 시 게임 멈춤
            // Legacy Input: ��Ŭ��
            if (Input.GetMouseButtonDown(0))
                TrySelectNodeByClick();

            // ���(��Ŭ�� or ESC) : ������ �߿��� ��� ���� (��ȹ�� ����)

            if (state == MoveState.Delay && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
                CancelMove();
        }

        private void UpdateHoverIndicator()
        {
            if (moveIndicator == null) return;

            // 이동 중이거나 딜레이 중에는 인디케이터 숨김
            if (state != MoveState.Idle)
            {
                HideMoveIndicator();
                return;
            }

            if (cam == null)
            {
                HideMoveIndicator();
                return;
            }

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f, nodeLayerMask, QueryTriggerInteraction.Ignore))
            {
                HideMoveIndicator();
                return;
            }

            NodeSelectable selectable = hit.collider.GetComponentInParent<NodeSelectable>();
            if (selectable == null || selectable.Node == null)
            {
                HideMoveIndicator();
                return;
            }

            Node target = selectable.Node;

            // 비활성 노드, 현재 노드, 이웃이 아닌 노드는 표시 안 함
            if (!target.IsActive || target == currentNode)
            {
                HideMoveIndicator();
                return;
            }
            if (currentNode != null && !currentNode.IsNeighbor(target))
            {
                HideMoveIndicator();
                return;
            }

            // 이동 가능한 노드: 인디케이터를 해당 노드 위치에 표시
            moveIndicator.transform.position = target.Position;
            if (!moveIndicator.activeSelf)
                moveIndicator.SetActive(true);
            hoveredNode = target;
        }

        private void HideMoveIndicator()
        {
            if (moveIndicator != null && moveIndicator.activeSelf)
                moveIndicator.SetActive(false);
            hoveredNode = null;
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

            // ��Ģ: Ȱ�� ��常
            if (!target.IsActive) return;

            // ��Ģ: ���� ��尡 ������ ������� ��常��
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

        // �����/�ʱ�ȭ��: ���� �� ���� ��带 ������ �����ϰ� ������ ȣ��
        public void SetCurrentNode(Node node)
        {
            currentNode = node;
            if (node != null)
                transform.position = node.Position;
        }
    }
}
