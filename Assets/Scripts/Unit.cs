using UnityEngine;
using System.Collections;

public class Unit : MonoBehaviour {
    public Transform target;
    public float speed = 2;
    Vector3[] path;
    int targetIndex;
    Grid grid;
    public KeyCode findPathKey = KeyCode.P;
    public bool findPathOnAwake = true;

    void Start() {
        grid = FindObjectOfType<Grid>();
        if (findPathOnAwake){
            FindPath();
        }
    }
    void Update() {
        if (Input.GetKeyDown(findPathKey)) {
            FindPath();
        }
    }
    void FindPath() {
        PathRequestManager.RequestPath(transform.position, target.position, OnPathFound);
    }

    public void OnPathFound(Vector3[] newPath, bool pathSuccessful) {
        if (pathSuccessful) {
            path = newPath;
            targetIndex = 0;
            StopCoroutine("FollowPath");
            StartCoroutine("FollowPath");
        }
    }

    IEnumerator FollowPath() {
        Vector3 currentWaypoint = path[0];
        while (true) {
            if (transform.position == currentWaypoint) {
                targetIndex++;
                if (targetIndex >= path.Length) {
                    yield break;
                }
                currentWaypoint = path[targetIndex];
            }

            transform.position = Vector3.MoveTowards(transform.position, currentWaypoint, speed * Time.deltaTime);
            yield return null;

        }
    }

    public void OnDrawGizmos() {
        if (path != null) {
            for (int i = targetIndex; i < path.Length; i++) {
                Gizmos.color = Color.black;
                //Gizmos.DrawWireCube(path[i], Vector3.one);
                Gizmos.DrawWireCube(path[i], Vector3.one * (grid.nodeDiameter - grid.nodeSizeGizmo));

                if (i == targetIndex) {
                    Gizmos.DrawLine(transform.position, path[i]);
                } else {
                    Gizmos.DrawLine(path[i - 1], path[i]);
                }
            }
        }
    }
}
