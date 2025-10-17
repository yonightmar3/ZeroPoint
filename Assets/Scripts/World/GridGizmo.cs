using UnityEngine;

[ExecuteAlways]
public class GridGizmo : MonoBehaviour
{
    public float cellSize = 2f;
    public int halfCells = 100;
    public Color color = new Color(1, 1, 1, 0.1f);

    void OnDrawGizmos()
    {
        Gizmos.color = color;
        for (int x = -halfCells; x <= halfCells; x++)
        {
            Vector3 a = new Vector3(x * cellSize, 0, -halfCells * cellSize) + transform.position;
            Vector3 b = new Vector3(x * cellSize, 0, halfCells * cellSize) + transform.position;
            Gizmos.DrawLine(a, b);
        }
        for (int z = -halfCells; z <= halfCells; z++)
        {
            Vector3 a = new Vector3(-halfCells * cellSize, 0, z * cellSize) + transform.position;
            Vector3 b = new Vector3(halfCells * cellSize, 0, z * cellSize) + transform.position;
            Gizmos.DrawLine(a, b);
        }
    }
}
