using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FDG
{
    [RequireComponent(typeof(ForceDirectedGraph))]
    public class DemoRandomNodes : MonoBehaviour
    {
        [Header("Init Values")]
        public int NumRandNodes;
        public int NumRandConnections;

        GameObject nodeContainer;
        GameObject edgeContainer;

        void Awake()
        {
            ForceDirectedGraph forceDirectedGraph = GetComponent<ForceDirectedGraph>();

            nodeContainer = new GameObject("Nodes");
            edgeContainer = new GameObject("Edges");

            DemoNode node0 = DemoNode.New("node0");
            node0.transform.SetParent(nodeContainer.transform);
            forceDirectedGraph.AddNodeToGraph(node0, 0, 1, node0.UpdateMyEdges);

            node0.transform.position = Vector3.zero;
            forceDirectedGraph.SetNodeMobility(node0, true);

            Dictionary<int, DemoNode> randomNodes = new Dictionary<int, DemoNode> {{0, node0}};
            for (int i = 1; i < NumRandNodes; i++)
            {
                DemoNode newNode = DemoNode.New($"node{i}");
                newNode.transform.SetParent(nodeContainer.transform);
                forceDirectedGraph.AddNodeToGraph(newNode, i, 1, newNode.UpdateMyEdges);
                newNode.transform.position = new Vector3(
                    Random.Range(-10.0f, 10.0f),
                    Random.Range(-10.0f, 10.0f),
                    Random.Range(-10.0f, 10.0f));
                randomNodes.Add(i, newNode);
            }

            for (int i = 0; i < NumRandConnections; i++)
            {
                int randA = Random.Range(0, NumRandNodes);
                int randB = Random.Range(0, NumRandNodes);
                if (randA == randB) continue;

                DemoNode randNodeA = randomNodes[randA];
                DemoNode randNodeB = randomNodes[randB];

                string edgeName1 = $"edge: {randNodeA.name} - {randNodeA.name}";

                DemoEdge newDemoEdge = DemoEdge.New(edgeName1);
                newDemoEdge.transform.SetParent(edgeContainer.transform);
                newDemoEdge.NodeA = randNodeA.transform;
                newDemoEdge.NodeB = randNodeB.transform;

                forceDirectedGraph.AddEdgeToGraph(randNodeA, randNodeB);

                randNodeA.MyEdges.Add(newDemoEdge);
                randNodeB.MyEdges.Add(newDemoEdge);
            }

            forceDirectedGraph.StartGraph();
        }

        class DemoNode : MonoBehaviour
        {
            public readonly List<DemoEdge> MyEdges = new List<DemoEdge>();

            public static DemoNode New(string nodeName)
            {
                DemoNode newSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere).AddComponent<DemoNode>();
                newSphere.name = nodeName;
                return newSphere;
            }

            public void UpdateMyEdges()
            {
                foreach (DemoEdge myEdge in MyEdges)
                {
                    myEdge.UpdateEdge();
                }
            }
        }

        class DemoEdge : MonoBehaviour
        {
            public Transform NodeA;
            public Transform NodeB;

            public static DemoEdge New(string edgeName)
            {
                DemoEdge newCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder).AddComponent<DemoEdge>();
                newCylinder.name = edgeName;
                newCylinder.transform.localScale = new Vector3(.1f, 1f, .1f);
                return newCylinder;
            }

            public void UpdateEdge()
            {
                transform.position = Vector3.Lerp(NodeA.position, NodeB.position, .5f);
                transform.LookAt(NodeA);
                transform.Rotate(Vector3.right * 90);
                transform.localScale = new Vector3(
                    .05f,
                    Vector3.Distance(NodeA.position, NodeB.position) * .5f,
                    .05f);
            }
        }
    }
}