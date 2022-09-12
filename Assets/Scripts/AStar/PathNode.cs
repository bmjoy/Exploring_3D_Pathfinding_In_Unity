using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEditor.Experimental.GraphView;

public struct PathNode {
    public int x, y, z;
    public float3 worldPoint;
    public float gCost, hCost, fCost;
    public bool isWalkable;
    public int index, cameFromNodeIndex;
    int heapIndex;
    public void CalculateFCost() {
        fCost = gCost + hCost;
    }
    public void SetIsWalkable(bool isWalkable) {
        this.isWalkable = isWalkable;
    }

    public int HeapIndex {
        get {
            return heapIndex;
        }
        set {
            heapIndex = value;
        }
    }

    public int CompareTo(PathNode nodeToCompare) {
        int compare = fCost.CompareTo(nodeToCompare.fCost);
        if (compare == 0) {
            compare = hCost.CompareTo(nodeToCompare.hCost);
        }
        return -compare;
    }
}