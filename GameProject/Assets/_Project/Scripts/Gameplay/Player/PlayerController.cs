using System.Collections;
using UnityEngine;

namespace Game.Player
{
    public class PlayerController : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Moving
        }

        [Header("Movement")]
        [SerializeField]
        private float moveSpeed = 3f;

        [SerializeField]
        private float rotationSpeed = 8f;

        [SerializeField]
        private float arriveDistance = 0.05f;

        [Header("Start Node")]
        [SerializeField]
        private PlayerNode startNode;

        private PlayerNode currentNode;
        private State state;

        public PlayerNode CurrentNode => currentNode;

        public State CurrentState => state;

        private void Start()
        {
            if (startNode == null)
            {
                Debug.LogError("Start Node가 설정되지 않았습니다.");
                enabled = false;
                return;
            }

            currentNode = startNode;
            transform.position = currentNode.Position;

            state = State.Idle;
        }

        public bool MoveToNode(PlayerNode targetNode)
        {
            if (state != State.Idle)
                return false;

            if (targetNode == null)
                return false;

            if (!currentNode.IsConnected(targetNode))
                return false;

            StartCoroutine(MoveRoutine(targetNode));

            return true;
        }

        private IEnumerator MoveRoutine(PlayerNode targetNode)
        {
            state = State.Moving;

            Vector3 destination = targetNode.Position;

            while (Vector3.Distance(transform.position, destination) > arriveDistance)
            {
                RotateTowards(destination);

                transform.position = Vector3.MoveTowards(
                    transform.position,
                    destination,
                    moveSpeed * Time.deltaTime);

                yield return null;
            }

            Arrive(targetNode);
        }

        private void RotateTowards(Vector3 destination)
        {
            Vector3 direction = destination - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }

        private void Arrive(PlayerNode node)
        {
            transform.position = node.Position;

            currentNode = node;

            state = State.Idle;
        }
    }
}