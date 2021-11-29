using UnityEngine;

namespace FDG.Demo
{
    public class DemoEdge : MonoBehaviour
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