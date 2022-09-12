using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
public class Unit : MonoBehaviour {
    public Pathfinding grid;
    public Transform target;
    public float3 pos, endPos;
    public float speed = 1f, refreshTimer = 1f;
    [HideInInspector] public float refreshTime;
    public bool generatingPath, drawPathNodes;
    public Color pathColor = Color.black;
    public NativeList<PathNode> path;
    public int pathLength;
    public Vector3 velocity;
    public int pathIndex;
    public float nodeDistance, finalNodeDistance;
    public float dst;
    public Rigidbody rb;
    public NativeArray<PathNode> pathNodeArray;
    NativeArray<float3> result;
    NativeArray<float> resultDst;
    bool checkingDst;
    JobHandle setVelandDstJobHandle;
    SetDstAndVelocityJob setVelocityJob;
    public NativeList<PathNode> pathToCopy;


    void Awake() {
        rb = GetComponent<Rigidbody>();
        path = new NativeList<PathNode>(Allocator.Persistent);
        result = new NativeArray<float3>(1, Allocator.Persistent);
        resultDst = new NativeArray<float>(1, Allocator.Persistent);
        pathToCopy = new NativeList<PathNode>(Allocator.Persistent);
    }

    void OnEnable() {
        if (!grid.units.Contains(this)) {
            grid.units.Add(this);
            grid.activeUnits++;
        }
    }



    void Start() {
        SetPositions();
    }

    void Update() {
        SetPositions();
        CheckDstSetVelocity();
        if (rb) {
            rb.position += velocity;
        } else {
            transform.position += velocity;
        }
    }

    void OnDisable() {
        if (grid.units.Contains(this)) {
            grid.activeUnits--;
        }
    }

    void OnDestroy() {
        if (path.IsCreated) {
            path.Dispose();
        }
        if (setVelandDstJobHandle.IsCompleted) {
            setVelandDstJobHandle.Complete();
        }
        if (result.IsCreated) {
            result.Dispose();
        }
        if (resultDst.IsCreated) {
            resultDst.Dispose();
        }
        pathToCopy.Dispose();

    }

    void SetPositions() {
        pos = transform.position;
        endPos = target.position;
    }

    void CheckDstSetVelocity() {
        if (pathLength > 0) {
            if (!checkingDst) {
                checkingDst = true;
                setVelocityJob = new SetDstAndVelocityJob {
                    _deltaTime = Time.deltaTime,
                    _pathNode = path[pathIndex],
                    _pos = transform.position,
                    _speed = speed,
                    _result = result,
                    _resultDst = resultDst,
                };
                setVelandDstJobHandle = setVelocityJob.Schedule();
            }
            if (setVelandDstJobHandle.IsCompleted) {
                checkingDst = false;
                setVelandDstJobHandle.Complete();
                dst = resultDst[0];
                if (dst <= nodeDistance && pathIndex > 0) {
                    pathIndex--;
                }
                if (dst <= finalNodeDistance) {
                    velocity = Vector3.zero;
                }
                velocity = result[0];
            }
        } else {
            velocity = Vector3.zero;
        }
    }

    [BurstCompile]
    struct SetDstAndVelocityJob : IJob {
        public PathNode _pathNode;
        public float3 _pos;
        public float _deltaTime, _speed;
        public NativeArray<float3> _result;
        public NativeArray<float> _resultDst;
        public void Execute() {
            float dst = math.distance(_pathNode.worldPoint, _pos);
            float3 dir = (_pathNode.worldPoint - _pos);
            math.normalize(dir);
            _result[0] = dir * _speed * _deltaTime;
            _resultDst[0] = dst;
        }
    }

    public void SetPathIndex(int i) {
        pathIndex = i;
    }

    public void SetPathLength() {
        pathLength = path.Length;
    }

    void OnDrawGizmos() {
        if (Application.isPlaying && drawPathNodes) {
            var center = grid.transform.position;
            var size = Vector3.one * grid.nodeDiameter;
            foreach (var n in path) {
                center = n.worldPoint;
                Gizmos.color = pathColor;
                Gizmos.DrawCube(center, size);
            }
        }
    }
}
