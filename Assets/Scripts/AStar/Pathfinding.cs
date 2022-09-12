using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.UIElements;
using System.Linq;
using System.Security.Cryptography;
public class Pathfinding : MonoBehaviour {
    [HideInInspector] public List<Unit> units = new List<Unit>();
    public Vector3 gridWorldSize = Vector3.one * 10;
    public Color walkableColor = Color.white, unwalkableColor = Color.red;
    public int maxNodeCount = 100000;
    public bool drawGridNodes;
    float startTime;
    int unitCount => units.Count;
    public NativeArray<PathNode> pathNodeArray;
    NativeArray<JobHandle> findPathJobHandleArray;
    int lastScheduledJob;
    public Transform neighborHelper;
    public NativeArray<int3> neighbourOffsetArray;
    public float nodeRadius = 0.5f;
    public float nodeDiameter => nodeRadius * 2f;
    public LayerMask layerMask;
    NativeArray<bool> overlapBoxResults, pathResults;
    FindPathJob[] pathJobs;
    NativeArray<OverlapBoxCommand> commands;
    NativeArray<ColliderHit> results;
    NativeArray<float3> worldPointArray;
    public int activeUnits;
    int gridLength;
    int3 gridSize;
    public static float3 up => new float3(0, 1, 0);
    public static float3 down => new float3(0, -1, 0);
    public static float3 forward => new float3(0, 0, 1);
    public static float3 back => new float3(0, 0, -1);
    public static float3 left => new float3(-1, 0, 0);
    public static float3 right => new float3(1, 0, 0);
    public float3 one => new float3(1, 1, 1);
    JobHandle walkableNodeCommandsHandle;
    ProcessWalkableResultsJob processWalkableResultsJob;
    JobHandle processWalkablesHandle, setupOverlapJobsHandle, generateWorldPointHandle;

    void Start() {
        SetGridSizeAndLength();
        commands = new NativeArray<OverlapBoxCommand>(gridLength, Allocator.Persistent);
        results = new NativeArray<ColliderHit>(gridLength, Allocator.Persistent);
        overlapBoxResults = new NativeArray<bool>(gridLength, Allocator.Persistent);
        pathResults = new NativeArray<bool>(gridLength, Allocator.Persistent);
        neighbourOffsetArray = new NativeArray<int3>(neighborHelper.childCount, Allocator.Persistent);
        for (int i = 0; i < neighborHelper.childCount; i++) {
            int3 pos = int3.zero;
            pos.x = Mathf.RoundToInt(neighborHelper.GetChild(i).localPosition.x);
            pos.y = Mathf.RoundToInt(neighborHelper.GetChild(i).localPosition.y);
            pos.z = Mathf.RoundToInt(neighborHelper.GetChild(i).localPosition.z);
            neighbourOffsetArray[i] = pos;
        }
        GenerateWorldPoints();
        SetupOverlapCommands();
        GenerateWalkableNodes();
        ProcessWalkableResults();
        for (int i = 0; i < unitCount; i++) {
            units[i].refreshTime = units[i].refreshTimer;
            FindPath(units[i], i);
        }
    }

    void Update() {
        for (int i = 0; i < unitCount; i++) {
            units[i].refreshTime += Time.deltaTime;
            if (units[i].refreshTime >= units[i].refreshTimer) {
                FindPath(units[i], i);
            }
        }
    }

    void OnDestroy() {
        if (findPathJobHandleArray.IsCreated) {
            JobHandle.CompleteAll(findPathJobHandleArray);
            findPathJobHandleArray.Dispose();
        }
        pathNodeArray.Dispose();
        neighbourOffsetArray.Dispose();
        overlapBoxResults.Dispose();
        pathResults.Dispose();
        commands.Dispose();
        results.Dispose();
    }

    void SetGridSizeAndLength() {
        gridSize = new int3(
        Mathf.RoundToInt(gridWorldSize.x / nodeDiameter),
        Mathf.RoundToInt(gridWorldSize.y / nodeDiameter),
        Mathf.RoundToInt(gridWorldSize.z / nodeDiameter)
        );
        gridLength = gridSize.x * gridSize.y * gridSize.z;
    }

    public void FindPath(Unit unit, int id) {
        if (unit.gameObject.activeInHierarchy) {
            if ((pathJobs == null || pathJobs.Length == 0) && unitCount > 0) {
                pathJobs = new FindPathJob[unitCount];
                pathNodeArray = new NativeArray<PathNode>(gridLength, Allocator.Persistent);
                findPathJobHandleArray = new NativeArray<JobHandle>(unitCount, Allocator.Persistent);
            }
            if (!unit.generatingPath) {
                unit.generatingPath = true;
                startTime = Time.realtimeSinceStartup;
                GenerateWorldPoints();
                SchedulePathfinding(id, unit);
            }
            if (unit.generatingPath && findPathJobHandleArray[id].IsCompleted) {
                findPathJobHandleArray[id].Complete();
                CopyPathJob copyPathJob = new CopyPathJob {
                    _pathToCopy = unit.pathToCopy,
                    _unitPath = unit.path,
                };
                var CopyPathJobHandle = copyPathJob.Schedule();
                CopyPathJobHandle.Complete();

                unit.refreshTime = 0;
                unit.generatingPath = false;
                unit.SetPathLength();

                float dst = Mathf.Infinity;
                float newDst = Mathf.Infinity;
                int newPathIndex = unit.pathLength - 1;
                for (int i = 0; i < unit.pathLength; i++) {
                    newDst = Vector3.Distance(unit.transform.position, unit.path[i].worldPoint);
                    if (newDst <= dst) {
                        newPathIndex = i;
                        dst = newDst;
                    }
                }
                unit.SetPathIndex(newPathIndex);
            }
        }
    }

    void GenerateWorldPoints() {
        worldPointArray = new NativeArray<float3>(gridLength, Allocator.Persistent);
        GenerateWorldPointsJob generateWorldPointsJob = new GenerateWorldPointsJob {
            _gridSize = gridSize,
            _gridWorldSize = gridWorldSize,
            _nodeRadius = nodeRadius,
            _pos = transform.position,
            _worldPointArray = worldPointArray,
        };
        //jobHandleArray[lastScheduledJob]
        generateWorldPointHandle = generateWorldPointsJob.Schedule();
        generateWorldPointHandle.Complete();
    }

    void SetupOverlapCommands() {
        QueryParameters qParams = new QueryParameters {
            layerMask = layerMask,
        };
        SetupOverlapsJob setupOverlapsJob = new SetupOverlapsJob {
            _commands = commands,
            _gridSize = gridSize,
            _nodeRadius = nodeRadius,
            _qParams = qParams,
            _worldPointArray = worldPointArray,
        };
        setupOverlapJobsHandle = setupOverlapsJob.Schedule(generateWorldPointHandle);
        setupOverlapJobsHandle.Complete();
    }

    void GenerateWalkableNodes() {
        walkableNodeCommandsHandle = OverlapBoxCommand.ScheduleBatch(commands, results, gridLength / 8, 1, setupOverlapJobsHandle);
        walkableNodeCommandsHandle.Complete();
    }

    void ProcessWalkableResults() {
        processWalkableResultsJob = new ProcessWalkableResultsJob {
            _gridLength = gridLength,
            _overlapBoxResults = overlapBoxResults,
            _results = results,
        };
        processWalkablesHandle = processWalkableResultsJob.Schedule(walkableNodeCommandsHandle);
        processWalkablesHandle.Complete();
        worldPointArray.Dispose();
    }

    void SchedulePathfinding(int id, Unit unit) {
        pathJobs[id] = new FindPathJob {
            _startPosition = unit.pos,
            _endPosition = unit.endPos,
            _gridSize = gridSize,
            _path = unit.pathToCopy,
            _pathNodeArray = pathNodeArray,
            _neighbourOffsetArray = neighbourOffsetArray,
            _gridWorldSize = gridWorldSize,
            _pos = transform.position,
            _overlapBoxResults = overlapBoxResults,
            _pathResults = pathResults,
            _index = id,
            _worldPointArray = worldPointArray,
        };
        findPathJobHandleArray[id] = new JobHandle();
        var d = JobHandle.CombineDependencies(processWalkablesHandle, findPathJobHandleArray[lastScheduledJob]);
        findPathJobHandleArray[id] = pathJobs[id].Schedule(d);
        lastScheduledJob = id;


    }


    [BurstCompile]
    struct CopyPathJob : IJob {
        public NativeList<PathNode> _pathToCopy, _unitPath;
        public void Execute() {
            _unitPath.Clear();
            for (int i = 0; i < _pathToCopy.Length; i++) {
                _unitPath.Add(_pathToCopy[i]);
            }
        }
    }

    [BurstCompile]
    struct SetupOverlapsJob : IJob {
        public int3 _gridSize;
        public NativeArray<float3> _worldPointArray;
        public NativeArray<OverlapBoxCommand> _commands;
        float3 one;
        public float _nodeRadius;
        public QueryParameters _qParams;
        public void Execute() {
            one = new float3(1, 1, 1);
            for (int x = 0; x < _gridSize.x; x++) {
                for (int y = 0; y < _gridSize.y; y++) {
                    for (int z = 0; z < _gridSize.z; z++) {
                        int index = CalculateIndex(x, y, z, _gridSize);
                        float3 _worldPoint = _worldPointArray[index];
                        _commands[index] = new OverlapBoxCommand(_worldPoint, one * _nodeRadius, quaternion.identity, _qParams);
                    }
                }
            }
        }
    }

    [BurstCompile]
    struct GenerateWorldPointsJob : IJob {
        public NativeArray<float3> _worldPointArray;
        public float _nodeRadius;
        public float3 _gridWorldSize, _pos;
        public int3 _gridSize;

        public void Execute() {
            for (int x = 0; x < _gridSize.x; x++) {
                for (int y = 0; y < _gridSize.y; y++) {
                    for (int z = 0; z < _gridSize.z; z++) {
                        float3 _worldPoint = NodePosition(x, y, z);
                        int index = CalculateIndex(x, y, z, _gridSize);
                        _worldPointArray[index] = _worldPoint;
                    }
                }
            }
        }
        public float3 WorldBottomLeft() {
            float3 leftEdge = (left * (_gridWorldSize.x / 2));
            float3 bottomEdge = (down * (_gridWorldSize.y / 2));
            float3 backEdge = (back * (_gridWorldSize.z / 2));
            return _pos + leftEdge + bottomEdge + backEdge;
        }
        public float3 NodePosition(int x, int y, int z) {
            float _nodeDiameter = _nodeRadius * 2;
            float3 xAxis = right * (x * _nodeDiameter + _nodeRadius);
            float3 yAxis = up * (y * _nodeDiameter + _nodeRadius);
            float3 zAxis = forward * (z * _nodeDiameter + _nodeRadius);
            return WorldBottomLeft() + xAxis + yAxis + zAxis;
        }
    }

    [BurstCompile]
    struct ProcessWalkableResultsJob : IJob {
        public int _gridLength;
        public NativeArray<bool> _overlapBoxResults;
        public NativeArray<ColliderHit> _results;
        public void Execute() {
            for (int ri = 0; ri < _gridLength; ri++) {
                _overlapBoxResults[ri] = _results[ri].instanceID != 0;
            }
        }
    }

    [BurstCompile]
    struct FindPathJob : IJob {
        public float3 _startPosition, _endPosition, _pos, _gridWorldSize;
        public int3 _gridSize;
        public NativeList<PathNode> _path;
        public NativeArray<PathNode> _pathNodeArray;
        public NativeArray<int3> _neighbourOffsetArray;
        public NativeArray<bool> _overlapBoxResults, _pathResults;
        public int _index;
        [DeallocateOnJobCompletion] public NativeArray<float3> _worldPointArray;
        public void Execute() {

            for (int x = 0; x < _gridSize.x; x++) {
                for (int y = 0; y < _gridSize.y; y++) {
                    for (int z = 0; z < _gridSize.z; z++) {
                        int index = CalculateIndex(x, y, z, _gridSize);
                        float3 _worldPoint = _worldPointArray[index];
                        PathNode pathNode = _pathNodeArray[index];
                        pathNode.worldPoint = _worldPoint;
                        pathNode.x = x;
                        pathNode.y = y;
                        pathNode.z = z;
                        pathNode.index = index;
                        pathNode.gCost = int.MaxValue;
                        pathNode.hCost = CalculateDistanceCost(new int3(x, y, z), _endPosition);
                        pathNode.CalculateFCost();
                        pathNode.isWalkable = !_overlapBoxResults[index];
                        pathNode.cameFromNodeIndex = -1;
                        _pathNodeArray[pathNode.index] = pathNode;
                    }
                }
            }
            int endNodeIndex = NodeFromWorldPoint(-_pos + _endPosition).index;
            PathNode startNode = NodeFromWorldPoint(-_pos + _startPosition);
            startNode.gCost = 0;
            startNode.CalculateFCost();
            _pathNodeArray[startNode.index] = startNode;
            NativeList<int> openList = new NativeList<int>(Allocator.Temp);
            NativeParallelHashSet<int> closedList = new NativeParallelHashSet<int>(_gridSize.x * _gridSize.y * _gridSize.z, Allocator.Temp);
            openList.Add(startNode.index);
            while (openList.Length > 0) {
                int currentNodeIndex = GetLowestCostFNodeIndex(openList, _pathNodeArray);
                PathNode currentNode = _pathNodeArray[currentNodeIndex];
                if (currentNodeIndex == endNodeIndex) {
                    // Reached our destination!
                    break;
                }
                // Remove current node from Open List
                for (int i = 0; i < openList.Length; i++) {
                    if (openList[i] == currentNodeIndex) {
                        openList.RemoveAtSwapBack(i);
                        break;
                    }
                }
                closedList.Add(currentNodeIndex);
                for (int i = 0; i < _neighbourOffsetArray.Length; i++) {
                    int3 neighbourOffset = _neighbourOffsetArray[i];
                    int3 neighbourPosition = new int3(currentNode.x + neighbourOffset.x, currentNode.y + neighbourOffset.y, currentNode.z + neighbourOffset.z
                    )
                    ;
                    if (!IsPositionInsideGrid(neighbourPosition, _gridSize)) {
                        // Neighbour not valid position
                        continue;
                    }
                    int neighbourNodeIndex = CalculateIndex(neighbourPosition.x, neighbourPosition.y, neighbourPosition.z, _gridSize);
                    if (closedList.Contains(neighbourNodeIndex)) {
                        // Already searched this node
                        continue;
                    }
                    PathNode neighbourNode = _pathNodeArray[neighbourNodeIndex];
                    if (!neighbourNode.isWalkable) {
                        // Not walkable
                        continue;
                    }
                    int3 currentNodePosition = new int3(currentNode.x, currentNode.y, currentNode.z);
                    float tentativeGCost = currentNode.gCost + CalculateDistanceCost(currentNodePosition, neighbourPosition);
                    if (tentativeGCost < neighbourNode.gCost) {
                        neighbourNode.cameFromNodeIndex = currentNodeIndex;
                        neighbourNode.gCost = tentativeGCost;
                        neighbourNode.CalculateFCost();
                        _pathNodeArray[neighbourNodeIndex] = neighbourNode;
                        if (!openList.Contains(neighbourNode.index)) {
                            openList.Add(neighbourNode.index);
                        }
                    }
                }
            }
            PathNode endNode = _pathNodeArray[endNodeIndex];
            _path.Clear();
            if (endNode.cameFromNodeIndex == -1) {
                // Didn't find a path!
            } else {
                // Found a path
                CalculatePath(_pathNodeArray, endNode);
                /*
                foreach (int3 pathPosition in path) {
                    Debug.Log(pathPosition);
                }
                */
            }
            openList.Dispose();
            closedList.Dispose();
        }
        public PathNode NodeFromWorldPoint(float3 worldPosition) {
            float percentX = (worldPosition.x + _gridWorldSize.x / 2) / _gridWorldSize.x;
            float percentY = (worldPosition.y + _gridWorldSize.y / 2) / _gridWorldSize.y;
            float percentZ = (worldPosition.z + _gridWorldSize.z / 2) / _gridWorldSize.z;
            percentX = Mathf.Clamp01(percentX);
            percentY = Mathf.Clamp01(percentY);
            percentZ = Mathf.Clamp01(percentZ);
            int x = Mathf.RoundToInt(((_gridSize.x - 1) * percentX));
            int y = Mathf.RoundToInt(((_gridSize.y - 1) * percentY));
            int z = Mathf.RoundToInt(((_gridSize.z - 1) * percentZ));
            int i = CalculateIndex(x, y, z, _gridSize);
            return _pathNodeArray[i];
        }
        void CalculatePath(NativeArray<PathNode> pathNodeArray, PathNode endNode) {
            if (endNode.cameFromNodeIndex == -1) {
                // Couldn't find a path!
                return;
            } else {
                // Found a path
                _path.Add(endNode);
                PathNode currentNode = endNode;
                while (currentNode.cameFromNodeIndex != -1) {
                    PathNode cameFromNode = pathNodeArray[currentNode.cameFromNodeIndex];
                    _path.Add(cameFromNode);
                    currentNode = cameFromNode;
                }
                _pathResults[_index] = true;
            }
        }
        bool IsPositionInsideGrid(int3 gridPosition, int3 gridSize) {
            return
                gridPosition.x >= 0 &&
                gridPosition.y >= 0 &&
                gridPosition.z >= 0 &&
                gridPosition.x < gridSize.x &&
                gridPosition.y < gridSize.y &&
                gridPosition.z < gridSize.z;
        }
        float CalculateDistanceCost(float3 aPosition, float3 bPosition) {
            return math.distance(aPosition, bPosition);
        }
        int GetLowestCostFNodeIndex(NativeList<int> openList, NativeArray<PathNode> pathNodeArray) {
            PathNode lowestCostPathNode = pathNodeArray[openList[0]];
            for (int i = 1; i < openList.Length; i++) {
                PathNode testPathNode = pathNodeArray[openList[i]];
                if (testPathNode.fCost < lowestCostPathNode.fCost) {
                    lowestCostPathNode = testPathNode;
                }
            }
            return lowestCostPathNode.index;
        }
    }

    public static int CalculateIndex(int x, int y, int z, int3 gridSize) {
        return (z * gridSize.x * gridSize.y) + (y * gridSize.x) + x;
    }

    // public int[] to3D(int i) {
    //     gridSize = new int3(
    //     Mathf.RoundToInt(gridWorldSize.x / nodeDiameter),
    //     Mathf.RoundToInt(gridWorldSize.y / nodeDiameter),
    //     Mathf.RoundToInt(gridWorldSize.z / nodeDiameter)
    //     );
    //     int z = i / (gridSize.x * gridSize.y);
    //     i -= (z * gridSize.x * gridSize.y);
    //     int y = i / gridSize.x;
    //     int x = i % gridSize.x;
    //     return new int[] { x, y, z };
    // }

    void OnDrawGizmos() {
        var center = transform.position;
        var size = gridWorldSize;
        Gizmos.DrawWireCube(center, size);
        if (Application.isPlaying) {
            size = Vector3.one * nodeDiameter;
            bool generatingPath = false;
            for (int i = 0; i < unitCount; i++) {
                if (units[i].generatingPath) {
                    generatingPath = true;
                }
            }
            if (drawGridNodes && !generatingPath) {
                int nodeCount = 0;
                foreach (var n in pathNodeArray) {
                    if (nodeCount <= maxNodeCount) {
                        center = n.worldPoint;
                        Gizmos.color = n.isWalkable ? walkableColor : unwalkableColor;
                        if (n.isWalkable) {
                            Gizmos.DrawWireCube(center, size);
                        } else {
                            Gizmos.DrawCube(center, size);
                        }
                        nodeCount++;
                    } else {
                        break;
                    }
                }
            }
        }
    }
}