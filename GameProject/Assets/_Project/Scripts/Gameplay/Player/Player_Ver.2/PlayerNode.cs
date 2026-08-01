using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Player
{
    [RequireComponent(typeof(Collider))]
    public class PlayerNode : MonoBehaviour
    {
        [SerializeField]
        private List<PlayerNode> connectedNodes = new();


        public IReadOnlyList<PlayerNode> ConnectedNodes => connectedNodes;

        public Vector3 Position => transform.position;

        public event Action<PlayerNode> OnSelected;

        [SerializeField]
        private Transform cameraPoint;

        public Transform CameraPoint => cameraPoint;


        public bool IsConnected(PlayerNode node)
        {
            return connectedNodes.Contains(node);
        }

        private void OnMouseDown()
        {
            OnSelected?.Invoke(this);
        }
    }
}