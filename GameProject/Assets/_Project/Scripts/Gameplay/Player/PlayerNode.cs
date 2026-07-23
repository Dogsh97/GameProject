using System.Collections.Generic;
using UnityEngine;

namespace Game.Player
{
    public class PlayerNode : MonoBehaviour
    {
        [Header("Connected Nodes")]
        [SerializeField]
        private List<PlayerNode> connectedNodes = new();

        public IReadOnlyList<PlayerNode> ConnectedNodes => connectedNodes;

        public Vector3 Position => transform.position;

        public bool IsConnected(PlayerNode node)
        {
            return connectedNodes.Contains(node);
        }
    }
}