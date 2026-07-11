using System.Collections.Generic;
using UnityEngine;
using Game.NodeSystem;

namespace Game.Monster
{
    public class MonsterRoute : MonoBehaviour
    {
        [SerializeField]  private List<MonsterRouteNode> routeNodes = new();

        public int Count => routeNodes.Count;

        public MonsterRouteNode GetNode(int index)
        {
            if (index < 0 || index >= routeNodes.Count)
                return null;

            return routeNodes[index];
        }

    }   
}
