using Unity.AI.Navigation; // make sure the AI Navigation package is referenced
using UnityEngine;
public class BakeOnStart : MonoBehaviour
{
    public NavMeshSurface surface;
    void Start() { if (surface != null) surface.BuildNavMesh(); }
}
