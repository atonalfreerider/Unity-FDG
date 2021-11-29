using System.Collections.Generic;
using UnityEngine;

namespace FDG.Demo
{
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
}