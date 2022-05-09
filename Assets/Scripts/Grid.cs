using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Grid : MonoBehaviour {
    int width, height;
    int[,] gridArray;
    float cellSize;
    public Grid(int width, int height, float cellSize) {
        this.width = width;
        this.height = height;
        this.cellSize = cellSize;
        gridArray = new int[width, height];
        for (int x = 0; x < gridArray.GetLength(0); x++) {
            for (int y = 0; y < gridArray.GetLength(1); y++) {
                Debug.DrawLine(GetWorldPosition(x, y), GetWorldPosition(x, y + 1), Color.white, 100f);
                Debug.DrawLine(GetWorldPosition(x, y), GetWorldPosition(x + 1, y), Color.white, 100f);
            }
        }
        Debug.DrawLine(GetWorldPosition(0, height), GetWorldPosition(width, height), Color.white, 100f);
        Debug.DrawLine(GetWorldPosition(width, 0), GetWorldPosition(width, height), Color.white, 100f);

    }
    public Vector3 GetWorldPosition(int width, int depth) {
        Vector3 pos = Vector3.zero;
        pos.Set(width, 0, depth);
        return pos * cellSize;
        //return new Vector3(width, height) * cellSize;
    }
}
