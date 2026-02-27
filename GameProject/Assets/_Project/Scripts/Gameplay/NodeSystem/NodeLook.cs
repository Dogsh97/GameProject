using UnityEngine;

namespace Game.NodeSystem
{
    [DisallowMultipleComponent]
    public class NodeLook : MonoBehaviour
    {
        [Tooltip("도착 시 카메라가 이 방향을 바라보도록 정렬됨(월드 기준) Z축 방향을 바라봄")]
        [SerializeField] private Transform lookForward;

        public Vector3 Forward
        {
            get
            {
                if (lookForward != null) return lookForward.forward;
                return transform.forward; // 지정 안 하면 노드의 forward 사용
            }
        }
    }
}
