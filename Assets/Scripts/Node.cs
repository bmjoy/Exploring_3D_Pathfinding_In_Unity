using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node {
    public bool walkable;
    public Vector3 worldPos;
    public int gCost, hCost;
    public int gridX, gridY, gridZ;
    public Node(bool _walkable, Vector3 _worldPos, int _gridX, int _gridY, int _gridZ) {
        walkable = _walkable;
        worldPos = _worldPos;
        gridX = _gridX;
        gridY = _gridY;
        gridZ = _gridZ;
    }
    public int fCost {
        get { return gCost + hCost; }
    }
}
