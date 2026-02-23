using UnityEngine;
using UnityEditor;
using Game.NodeSystem;
using Game.Hiding;
using Game.Gimmick;

public class NodeCreator
{
    private const string Node_Layer_Name = "Node";
    private const float AutoLinkDistance = 8f;

    [MenuItem("NodeCreator/Create Node")]
    public static void CreateNode()
    {
        CreateNodeInternal("Node_", addHideSpot: false, addGimmick: false);
    }

    [MenuItem("NodeCreator/Create Hide Node")]
    public static void CreateHideNode()
    {
        CreateNodeInternal("Node_Hide_", addHideSpot: true, addGimmick: false);
    }

    [MenuItem("NodeCreator/Create Gimmick Node")]
    public static void CreateGimmickNode()
    {
        CreateNodeInternal("Node_Gimmick_", addHideSpot: false, addGimmick: true);
    }

    // 추가: 버튼(메뉴)으로 인접 노드 재계산
    [MenuItem("NodeCreator/Rebuild Neighbors (Distance 5)")]
    public static void RebuildNeighborsMenu()
    {
        NodeGraph graph = Object.FindObjectOfType<NodeGraph>();
        if (graph == null)
        {
            Debug.LogError("씬에 NodeGraph가 존재하지 않습니다.");
            return;
        }

        RebuildNeighbors(graph.transform, AutoLinkDistance);
        Debug.Log($"[NodeCreator] Rebuild Neighbors 완료! (거리 <= {AutoLinkDistance})");
    }

    private static void CreateNodeInternal(string prefix, bool addHideSpot, bool addGimmick)
    {
        NodeGraph graph = Object.FindObjectOfType<NodeGraph>();
        if (graph == null)
        {
            Debug.LogError("씬에 NodeGraph가 존재하지 않습니다.");
            return;
        }

        Transform parent = graph.transform;

        int nextIndex = GetNextNodeIndex(parent, prefix);
        string uniqueName = $"{prefix}{nextIndex:00}";

        GameObject newObj = new GameObject(uniqueName);
        newObj.transform.SetParent(parent);
        newObj.transform.localPosition = new Vector3(0f, 0f, (nextIndex - 1) * 5f);

        int nodeLayer = LayerMask.NameToLayer(Node_Layer_Name);
        if (nodeLayer != -1) newObj.layer = nodeLayer;

        newObj.AddComponent<Node>();
        newObj.AddComponent<NodeSelectable>();

        if (addHideSpot) newObj.AddComponent<HideSpot>();
        if (addGimmick) newObj.AddComponent<GimmickNode>();

        BoxCollider boxCollider = newObj.AddComponent<BoxCollider>();
        boxCollider.size = new Vector3(5f, 5f, 5f);
        boxCollider.center = new Vector3(0f, 2.5f, 0f);

        Undo.RegisterCreatedObjectUndo(newObj, "Create Node");
        Selection.activeGameObject = newObj;
    }

    private static int GetNextNodeIndex(Transform parent, string prefix)
    {
        int maxIndex = 0;

        foreach (Transform child in parent)
        {
            if (!child.name.StartsWith(prefix)) continue;

            string numberPart = child.name.Substring(prefix.Length);
            if (int.TryParse(numberPart, out int index))
                maxIndex = Mathf.Max(maxIndex, index);
        }

        return maxIndex + 1;
    }

    // -------------------------
    // 아래부터: Rebuild 로직
    // -------------------------

    private static void RebuildNeighbors(Transform root, float maxDistance)
    {
        Node[] nodes = root.GetComponentsInChildren<Node>(includeInactive: true);

        // 1) 전부 비우기
        foreach (var node in nodes)
            ClearNeighbors(node);

        // 2) 거리 기준으로 다시 연결 (양방향)
        for (int i = 0; i < nodes.Length; i++)
        {
            for (int j = i + 1; j < nodes.Length; j++)
            {
                float dist = Vector3.Distance(nodes[i].transform.position, nodes[j].transform.position);
                if (dist <= maxDistance)
                {
                    AddNeighbor(nodes[i], nodes[j]);
                    AddNeighbor(nodes[j], nodes[i]);
                }
            }
        }
    }

    private static void ClearNeighbors(Node node)
    {
        SerializedObject so = new SerializedObject(node);
        SerializedProperty neighborsProp = so.FindProperty("neighbors");
        neighborsProp.ClearArray();
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(node);
    }

    private static void AddNeighbor(Node owner, Node target)
    {
        SerializedObject so = new SerializedObject(owner);
        SerializedProperty neighborsProp = so.FindProperty("neighbors");

        // 중복 방지
        for (int i = 0; i < neighborsProp.arraySize; i++)
        {
            if (neighborsProp.GetArrayElementAtIndex(i).objectReferenceValue == target)
                return;
        }

        neighborsProp.arraySize++;
        neighborsProp.GetArrayElementAtIndex(neighborsProp.arraySize - 1).objectReferenceValue = target;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(owner);
    }
}