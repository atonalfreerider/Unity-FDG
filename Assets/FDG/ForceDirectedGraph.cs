using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace FDG
{
    /// <summary>
    /// A force directed graph for Unity that uses Hooke's Law and Coulombs Law running in Unity C# Jobs.
    ///
    /// Usage:
    /// -Attach this component to a GameObject in a scene.
    /// -Add nodes to the FDG by calling <see cref="AddNodeToGraph"/> and passing a Unity component that will act
    ///  as the node.
    /// -Add edges to the FDG by calling <see cref="AddEdgeToGraph"/> and passing two components that have been
    ///  previously added to the graph and therefore have <see cref="Node"/> components attached to them. Note that
    ///  there is no visual edge that is drawn by this graph. Such an edge must be created by the user.
    /// -Run and Stop the graph using <see cref="StartGraph"/> and <see cref="StopGraph"/>. 
    /// </summary>
    public class ForceDirectedGraph : MonoBehaviour
    {
        // internal variables

        /// <summary>
        /// All of the nodes in the graph. While running, the forces acting on the nodes will move the transform that
        /// is associated with that node.
        /// </summary>
        readonly Dictionary<int, Node> nodes = new();
        readonly Dictionary<int, int> idToIndexMap = new();

        bool backgroundCalculation = false;

        public delegate void MovementCallback();

        /// <summary>
        /// A coroutine that continuously runs and updates the state of the world on every iteration.
        /// </summary>
        Coroutine graphAnimator;

        // variables that can be set externally or adjusted from the Unity Editor.
        [Header("Adjustable Values")] [Range(0.001f, 500)]
        // The constant that resembles Ke in Coulomb's Law to signify the strength of the repulsive force between nodes.
        public float UniversalRepulsiveForce = 100;

        [Range(0.001f, 100)]
        // The constant that resembles K in Hooke's Law to signify the strength of the attraction on an edge.
        public float UniversalSpringForce = 15;

        [Range(1, 10)]
        // The speed at which each iteration is run (lower is faster).
        public int TimeStep = 2;

        [Range(1, 20)]
        // An optimization for the C# Job. Gradually increase this value until performance begins to drop.
        public int ForceCalcBatch = 1;

        /// <summary>
        /// Adds a <see cref="Node"/> component to the component gameobject that is passed. When the graph is run,
        /// this behaviour will move the gameobject as it responds to forces in the graph.
        /// </summary>
        /// <param name="component">The component whose gameobject will have a node attached.</param>
        /// <param name="index">A UNIQUE index for this node.</param>
        /// <param name="nodeMass">The mass of the node. A larger mass will mean more inertia.</param>
        [PublicAPI]
        public void AddNodeToGraph(
            Component component,
            int index,
            float nodeMass = 1,
            MovementCallback movementCallback = null)
        {
            Node newNode = component.gameObject.AddComponent<Node>();
            newNode.Mass = nodeMass;
            newNode.movementCallback = movementCallback;
            nodes.Add(index, newNode);
            idToIndexMap.Add(component.GetInstanceID(), index);
        }

        [PublicAPI]
        public void AddEdgeToGraph(Component componentA, Component componentB)
        {
            int indexA = idToIndexMap[componentA.GetInstanceID()];
            int indexB = idToIndexMap[componentB.GetInstanceID()];
            nodes.TryGetValue(indexA, out Node nodeA);
            nodes.TryGetValue(indexB, out Node nodeB);

            if (nodeA != null && nodeB != null)
            {
                nodeA.MyEdges.Add(indexB);
                nodeB.MyEdges.Add(indexA);
            }
        }

        [PublicAPI]
        public void Clear()
        {
            foreach (Node node in nodes.Values)
            {
                Destroy(node);
            }

            nodes.Clear();
        }

        [PublicAPI]
        public void StartGraph()
        {
            foreach (Node node in nodes.Values)
            {
                node.VirtualPosition = node.transform.position;
            }

            graphAnimator = StartCoroutine(Iterate());
        }

        [PublicAPI]
        public void StopGraph()
        {
            if (graphAnimator == null) return;

            StopCoroutine(graphAnimator);
            graphAnimator = null;
        }

        [PublicAPI]
        public void RunForIterations(int numIterations)
        {
            backgroundCalculation = true;
            foreach (Node node in nodes.Values)
            {
                node.VirtualPosition = node.transform.position;
            }

            graphAnimator = StartCoroutine(Iterate(numIterations));
        }

        [PublicAPI]
        public void SetNodeMobility(Component nodeComponent, bool isImmobile)
        {
            Node node = nodeComponent.GetComponent<Node>();
            if (node == null) return;
            node.IsImmobile = isImmobile;
        }

        [PublicAPI]
        public void SetNodeMass(Component nodeComponent, float nodeMass)
        {
            Node node = nodeComponent.GetComponent<Node>();
            if (node == null) return;
            node.Mass = nodeMass;
        }

        void OnDestroy()
        {
            Clear();
        }

        IEnumerator Iterate(int remainingIterations = 0)
        {
            // Perform a job to get all resulting balance forces. Each set of forces of length 1 less than all nodes
            // are all of the forces acting on node N in the nodes collection.
            List<Vector3> balanceDisplacements = NodeBalanceDisplacements().ToList();

            int finalCount = 0;
            foreach (Node node in nodes.Values)
            {
                Vector3 finalForce = balanceDisplacements[finalCount];
                finalCount++;
                if (node.IsImmobile) continue;
                node.VirtualPosition += finalForce;
            }

            if (!backgroundCalculation || remainingIterations > 0)
            {
                if (!backgroundCalculation)
                {
                    foreach (Node node in nodes.Values)
                    {
                        node.transform.position = node.VirtualPosition;
                        node.movementCallback?.Invoke();
                    }

                    yield return null;
                    yield return Iterate();
                }
                else
                {
                    yield return Iterate(remainingIterations - 1);
                }
            }
            else
            {
                yield return MoveToFinal();
            }
        }

        IEnumerator MoveToFinal()
        {
            float prog = 0;
            float animSec = 1;
            while (prog < animSec)
            {
                foreach (Node node in nodes.Values)
                {
                    node.transform.position = Vector3.Lerp(
                        node.transform.position,
                        node.VirtualPosition,
                        Time.deltaTime / (animSec - prog)
                    );

                    node.movementCallback?.Invoke();
                }

                yield return null;

                prog += Time.deltaTime;
            }

            foreach (Node node in nodes.Values)
            {
                node.transform.position = node.VirtualPosition;
                node.movementCallback?.Invoke();
            }
        }

        /// <summary>
        /// Run a job to calculate the balance forces that each node is experiencing from every other node in the map. 
        /// </summary>
        /// <returns>The result forces on each node in the order that they are represented in nodes.</returns>
        IEnumerable<Vector3> NodeBalanceDisplacements()
        {
            // prepare native arrays for each calculation value
            NativeArray<Vector3> nodePositions =
                new NativeArray<Vector3>(nodes.Count, Allocator.TempJob);
            NativeArray<float> nodeMasses =
                new NativeArray<float>(nodes.Count, Allocator.TempJob);
            NativeArray<int> edgeBlocks =
                new NativeArray<int>(nodes.Count, Allocator.TempJob);

            NativeArray<Vector3> nodeResultDisplacement =
                new NativeArray<Vector3>(nodes.Count, Allocator.TempJob);

            List<int> allEdges = new List<int>();
            foreach (KeyValuePair<int, Node> idxAndNode in nodes)
            {
                nodePositions[idxAndNode.Key] = idxAndNode.Value.VirtualPosition;
                nodeMasses[idxAndNode.Key] = idxAndNode.Value.Mass;
                edgeBlocks[idxAndNode.Key] = idxAndNode.Value.MyEdges.Count;

                allEdges.AddRange(idxAndNode.Value.MyEdges);
            }

            NativeArray<int> edgeIndices =
                new NativeArray<int>(allEdges.Count, Allocator.TempJob);
            for (int i = 0; i < allEdges.Count; i++)
            {
                edgeIndices[i] = allEdges[i];
            }

            BalanceForceJob balanceForceJob = new()
            {
                NodePositions = nodePositions,
                NodeMasses = nodeMasses,
                EdgeBlocks = edgeBlocks,
                EdgeIndices = edgeIndices,
                Ke = UniversalRepulsiveForce,
                K = UniversalSpringForce,
                TimeValue = TimeStep,
                NodeResultDisplacement = nodeResultDisplacement
            };

            // Schedule the job with one Execute per index in the results array and only 1 item per processing batch
            JobHandle handle = balanceForceJob.Schedule(
                nodeResultDisplacement.Length,
                ForceCalcBatch);

            // Wait for the job to complete
            handle.Complete();

            List<Vector3> results = nodeResultDisplacement.ToList();

            // Free the memory allocated by the arrays
            nodePositions.Dispose();
            nodeMasses.Dispose();
            edgeBlocks.Dispose();
            edgeIndices.Dispose();
            nodeResultDisplacement.Dispose();

            return results;
        }

        struct BalanceForceJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> NodePositions;
            [ReadOnly] public NativeArray<float> NodeMasses;
            [ReadOnly] public NativeArray<int> EdgeBlocks;
            [ReadOnly] public NativeArray<int> EdgeIndices;

            [ReadOnly] public float Ke;
            [ReadOnly] public float K;
            [ReadOnly] public float TimeValue;

            public NativeArray<Vector3> NodeResultDisplacement;

            public void Execute(int i)
            {
                Vector3 nodeI = NodePositions[i];
                Vector3 resultForceAndDirection = Vector3.zero;

                int edgesStart = 0;
                for (int z = 0; z < i; z++)
                {
                    edgesStart += EdgeBlocks[z];
                }

                int edgesEnd = edgesStart + EdgeBlocks[i];

                for (int j = 0; j < NodePositions.Length; j++)
                {
                    if (i == j) continue;
                    Vector3 nodeJ = NodePositions[j];
                    float distance = Vector3.Distance(nodeI, nodeJ);
                    Vector3 direction = Vector3.Normalize(nodeI - nodeJ);

                    bool isActor = false;
                    for (int w = edgesStart; w < edgesEnd; w++)
                    {
                        if (EdgeIndices[w] == j)
                        {
                            isActor = true;
                            w = edgesEnd;
                        }
                    }

                    // Hooke's Law attractive force p2 <- p1
                    float hF = isActor ? K * distance : 0;

                    // Coulomb's Law repulsive force p2 -> p1
                    float cF = Ke / (distance * distance);

                    resultForceAndDirection += (cF - hF) * direction;
                }

                // Divide the result force by the amount of displacements that were summed and also by the node mass and
                // the time step in the calculation.
                NodeResultDisplacement[i] = resultForceAndDirection /
                                            (TimeValue * NodeMasses[i] * (NodePositions.Length - 1));
            }
        }

        class Node : MonoBehaviour
        {
            public float Mass;
            public bool IsImmobile = false;
            public Vector3 VirtualPosition = Vector3.zero;
            public readonly List<int> MyEdges = new();

            public MovementCallback movementCallback;
        }
    }
}