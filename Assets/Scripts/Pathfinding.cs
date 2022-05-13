using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.VisualScripting;
using UnityEngine;

public class Pathfinding : MonoBehaviour {
    PathRequestManager requestManager;
    Grid grid;
    void Awake() {
        requestManager = GetComponent<PathRequestManager>();
        grid = GetComponent<Grid>();
    }

    public void StartFindPath(Vector3 startPos, Vector3 targetPos) {
        StartCoroutine(FindPath(startPos, targetPos));
    }
    IEnumerator FindPath(Vector3 startPos, Vector3 targetPos) {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        Vector3[] waypoints = new Vector3[0];
        bool pathSuccess = false;
        Node startNode = grid.NodeFromWorldPoint(startPos);
        Node targetNode = grid.NodeFromWorldPoint(targetPos
        );
        if (startNode.walkable && targetNode.walkable) {
            Heap<Node> openSet = new Heap<Node>(grid.MaxSize);
            HashSet<Node> closedSet = new HashSet<Node>();
            openSet.Add(startNode);
            while (openSet.Count > 0) {
                Node currentNode = openSet.RemoveFirst();
                closedSet.Add(currentNode);
                if (currentNode == targetNode) {
                    sw.Stop();
                    print("path found: " + sw.ElapsedMilliseconds + " ms");
                    pathSuccess = true;
                    break;
                }
                foreach (Node neighbour in grid.GetNeighbours(currentNode)) {
                    if (!neighbour.walkable || closedSet.Contains(neighbour)) {
                        continue;
                    }
                    float newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour) + neighbour.movementPenalty;
                    if (newMovementCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour)) {
                        neighbour.gCost = newMovementCostToNeighbour;
                        neighbour.hCost = GetDistance(neighbour, targetNode);
                        neighbour.parent = currentNode;
                        if (!openSet.Contains(neighbour)) {
                            openSet.Add(neighbour);
                        }else{
                            openSet.UpdateItem(neighbour);
                        }
                    }
                }
            }
        }

        yield return null;
        if (pathSuccess) {
            waypoints = RetracePath(startNode, targetNode);
        }
        requestManager.FinishedProcessingPath(waypoints, pathSuccess);
    }
    Vector3[] RetracePath(Node startNode, Node endNode) {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;
        while (currentNode != startNode) {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        Vector3[] waypoints = SimplifyPath(path);
        Array.Reverse(waypoints);
        return waypoints;
    }

    Vector3[] SimplifyPath(List<Node> path) {
        List<Vector3> waypoints = new List<Vector3>();
        Vector3 directionOld = Vector3.zero;
        for (int i = 1; i < path.Count; i++) {
            float x = path[i - 1].gridX - path[i].gridX;
            float y = path[i - 1].gridY - path[i].gridY;
            float z = path[i - 1].gridZ - path[i].gridZ;
            Vector3 directionNew = new Vector3(x, y, z);
            if (directionNew != directionOld) {
                waypoints.Add(path[i].worldPosition);
            }
            directionOld = directionNew;
        }
        return waypoints.ToArray();
    }
    float GetDistance(Node nodeA, Node nodeB) {
        // int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        // int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);
        // int dstZ = Mathf.Abs(nodeA.gridZ - nodeB.gridZ);
        // if (dstX < dstY && dstX < dstZ) {
        //     if (dstY < dstZ) {
        //         return 17 * dstX + 14 * (dstY - dstX) + 10 * (dstZ - dstY - dstX);
        //     }
        //     return 17 * dstX + 14 * (dstX - dstY) + 10 * (dstY - dstZ - dstX);
        // }
        // if (dstY < dstX && dstY < dstZ) {
        //     if (dstX < dstZ) {
        //         return 17 * dstY + 14 * (dstX - dstY) + 10 * (dstZ - dstX - dstY);
        //     }
        // }
        // return 17 * dstY + 14 * (dstY - dstX) + 10 * (dstX - dstZ - dstY);
        return (Vector3.Distance(nodeA.worldPosition, nodeB.worldPosition));
    }
}
