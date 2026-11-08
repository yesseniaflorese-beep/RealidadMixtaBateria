using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StationPivotFixer : MonoBehaviour
{
    public Transform tableCollider;   // el MesaCollider (no cuenta para el centro)

    // Solo cuenta objetos activos y visibles
    bool GetBounds(out Bounds b)
    {
        b = new Bounds();
        bool any = false;
        foreach (var r in GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled) continue;
            if (r is ParticleSystemRenderer || r is LineRenderer) continue;
            if (tableCollider != null && r.transform.IsChildOf(tableCollider)) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    // El pivote NO se mueve; los objetos se mueven hasta quedar centrados en él
    [ContextMenu("1) Traer los objetos al pivote")]
    void BringObjectsToPivot()
    {
        if (!GetBounds(out Bounds b))
        {
            Debug.LogWarning("StationPivotFixer: no encontré objetos activos con Renderer dentro de la Estacion");
            return;
        }

        Vector3 groupBase = new Vector3(b.center.x, b.min.y, b.center.z);
        Vector3 delta = transform.position - groupBase;

        foreach (Transform child in transform)
        {
            if (tableCollider != null && child == tableCollider) continue;
#if UNITY_EDITOR
            Undo.RecordObject(child, "Traer objetos al pivote");
#endif
            child.position += delta;
        }

        if (tableCollider != null)
        {
            tableCollider.localPosition = Vector3.zero;
            tableCollider.localRotation = Quaternion.identity;
        }

        Debug.Log("StationPivotFixer: objetos movidos " + delta.magnitude.ToString("0.00") + " m hacia el pivote. Tamaño del grupo: " +
                  b.size.x.ToString("0.00") + " x " + b.size.z.ToString("0.00") + " m");
    }

    // Los objetos NO se mueven; el pivote se mueve hasta el centro de ellos
    [ContextMenu("2) Mover el pivote a los objetos")]
    void MovePivotToObjects()
    {
        if (!GetBounds(out Bounds b))
        {
            Debug.LogWarning("StationPivotFixer: no encontré objetos activos con Renderer dentro de la Estacion");
            return;
        }

        Vector3 newPos = new Vector3(b.center.x, b.min.y, b.center.z);

        var kids = new System.Collections.Generic.List<Transform>();
        foreach (Transform c in transform) kids.Add(c);

#if UNITY_EDITOR
        Undo.RegisterFullObjectHierarchyUndo(gameObject, "Mover pivote");
#endif
        foreach (var k in kids) k.SetParent(null, true);
        transform.SetPositionAndRotation(newPos, Quaternion.identity);
        transform.localScale = Vector3.one;
        foreach (var k in kids) k.SetParent(transform, true);

        if (tableCollider != null)
        {
            tableCollider.localPosition = Vector3.zero;
            tableCollider.localRotation = Quaternion.identity;
        }

        Debug.Log("StationPivotFixer: pivote movido a " + newPos);
    }

    // Con la Estacion seleccionada: caja amarilla = los objetos, esfera roja = el pivote
    void OnDrawGizmosSelected()
    {
        if (!GetBounds(out Bounds b)) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.05f);
    }
}