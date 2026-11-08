using UnityEngine;

public class StationNormalizer : MonoBehaviour
{
    public Transform tableCollider;   // el MesaCollider (no se mueve)
    public float lift = 0.01f;        // separación sobre la mesa (m)

    [ContextMenu("Normalizar estacion")]
    void Normalize()
    {
        bool any = false;
        Bounds b = new Bounds();

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer) continue;
            if (tableCollider != null && r.transform.IsChildOf(tableCollider)) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) { Debug.LogWarning("StationNormalizer: no encontré objetos con Renderer"); return; }

        // Punto central y más bajo del grupo -> debe quedar en el origen de la Estacion
        Vector3 groupBase = new Vector3(b.center.x, b.min.y, b.center.z);
        Vector3 delta = (transform.position + Vector3.up * lift) - groupBase;

        foreach (Transform child in transform)
        {
            if (child == tableCollider) continue;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(child, "Normalizar estacion");
#endif
            child.position += delta;
        }
        Debug.Log($"StationNormalizer: grupo movido {delta}");
    }
}