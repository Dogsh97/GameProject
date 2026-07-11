using UnityEngine;
using Game.NodeSystem;

namespace Game.Monster
{
    [System.Serializable]
    public class MonsterRouteNode
    {
        [Header("Node")]
        public Node node;

        [Header("Wait Time")]
        public float minWait = 3f;

        public float maxWait = 7f;

        [Header("Move Chance")]
        [Range(0f, 1f)]
        public float moveChance = 1f;
    }
}