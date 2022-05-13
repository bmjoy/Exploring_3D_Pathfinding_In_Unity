using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grid : MonoBehaviour {
    public Vector3 gridWorldSize;
    public float nodeRadius;
    public TerrainType[] walkableRegions;
    public LayerMask unwalkableMask;
    public bool displayGridGizmos, onlyDisplayWalkable, onlyDisplayUnwalkable;
    [Range(0.001f, 1)] public float nodeSizeGizmo = 0.1f;
    Node[,,] grid;
    public float nodeDiameter{get;set;}
    int gridSizeX, gridSizeY, gridSizeZ;
    LayerMask walkableMask;
    Dictionary<int, int> walkableRegionsDictionary = new Dictionary<int, int>();
    public bool splitPathfindingOverFrames;
    public int MaxSize {
        get {
            return gridSizeX * gridSizeY * gridSizeZ;
        }
    }
    void Awake() {
        nodeDiameter = nodeRadius * 2f;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        gridSizeZ = Mathf.RoundToInt(gridWorldSize.z / nodeDiameter);
        foreach (TerrainType region in walkableRegions) {
            walkableMask.value |= region.terrainMask.value;
            walkableRegionsDictionary.Add((int)Mathf.Log(region.terrainMask.value, 2), region.terrainPenalty);
        }
        CreateGrid();
    }
    void CreateGrid() {
        grid = new Node[gridSizeX, gridSizeY, gridSizeZ];
        Vector3 leftEdge = (Vector3.left * gridWorldSize.x / 2);
        Vector3 bottomEdge = (Vector3.down * gridWorldSize.y / 2);
        Vector3 backEdge = (Vector3.back * gridWorldSize.z / 2);
        Vector3 worldBottomLeft = transform.position + leftEdge + bottomEdge + backEdge;
        for (int x = 0; x < gridSizeX; x++) {
            for (int y = 0; y < gridSizeY; y++) {
                for (int z = 0; z < gridSizeZ; z++) {
                    Vector3 worldPoint = worldBottomLeft +
                    Vector3.right * (x * nodeDiameter + nodeRadius) +
                    Vector3.up * (y * nodeDiameter + nodeRadius) +
                    Vector3.forward * (z * nodeDiameter + nodeRadius);
                    bool walkable = !(Physics.CheckSphere(worldPoint, nodeRadius, unwalkableMask));
                    int movementPenalty = 0;
                    //raycast
                    if (walkable) {
                        Ray ray = new Ray(worldPoint + Vector3.up * 50, Vector3.down);
                        RaycastHit hit;
                        if (Physics.Raycast(ray, out hit, 100, walkableMask)) {
                            walkableRegionsDictionary.TryGetValue(hit.collider.gameObject.layer, out movementPenalty);
                        }
                    }
                    grid[x, y, z] = new Node(walkable, worldPoint, x, y, z, movementPenalty);
                }
            }
        }
    }

    public List<Node> GetNeighbours(Node node) {
        List<Node> neighbours = new List<Node>();
        for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
                for (int z = -1; z <= 1; z++) {
                    if (x == 0 && y == 0 && z == 0) {
                        continue;
                    }
                    int checkX = node.gridX + x;
                    int checkY = node.gridY + y;
                    int checkZ = node.gridZ + z;
                    if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY && checkZ >= 0 && checkZ < gridSizeZ) {
                        neighbours.Add(grid[checkX, checkY, checkZ]);
                    }
                }
            }
        }
        return neighbours;
    }


    public Node NodeFromWorldPoint(Vector3 worldPosition) {
        float percentX = (worldPosition.x + gridWorldSize.x / 2) / gridWorldSize.x;
        float percentY = (worldPosition.y + gridWorldSize.y / 2) / gridWorldSize.y;
        float percentZ = (worldPosition.z + gridWorldSize.z / 2) / gridWorldSize.z;
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);
        percentZ = Mathf.Clamp01(percentZ);
        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        int z = Mathf.RoundToInt((gridSizeZ - 1) * percentZ);
        return grid[x, y, z];
    }
    void OnDrawGizmos() {
        Gizmos.DrawWireCube(transform.position, gridWorldSize);
        if (grid != null && displayGridGizmos) {
            foreach (Node n in grid) {
                bool walkable = n.walkable;
                //only do this if both settings arent turned on. if they are, ignore it.
                if (!(onlyDisplayUnwalkable && onlyDisplayWalkable)) {
                    if ((walkable && onlyDisplayUnwalkable) || (!walkable && onlyDisplayWalkable)) {
                        continue;
                    }
                }

                Gizmos.color = walkable ? Color.white : Color.red;
                Gizmos.DrawWireCube(n.worldPosition, Vector3.one * (nodeDiameter - nodeSizeGizmo));
            }
        }
    }
    [System.Serializable]
    public class TerrainType {
        public LayerMask terrainMask;
        public int terrainPenalty;
    }
}
