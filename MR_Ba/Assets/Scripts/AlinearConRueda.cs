using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Herramienta temporal: pone esta estacion exactamente en el centro de una rueda.
// Se usa una vez (clic derecho en el titulo del componente > "Alinear con la rueda").
// Despues puedes quitar el script.
public class AlinearConRueda : MonoBehaviour
{
    public Transform rueda;                         // la rueda donde va esta estacion
    public Vector3 rotacionExtra = Vector3.zero;    // si los birlos miran hacia adentro, prueba (0, 180, 0)

    [ContextMenu("Alinear con la rueda")]
    void Alinear()
    {
        if (rueda == null)
        {
            Debug.LogWarning("AlinearConRueda: arrastra la rueda al campo 'Rueda' primero.");
            return;
        }

#if UNITY_EDITOR
        Undo.RecordObject(transform, "Alinear estacion con rueda");
#endif

        transform.SetPositionAndRotation(rueda.position, rueda.rotation * Quaternion.Euler(rotacionExtra));
        Debug.Log("Estacion alineada con " + rueda.name);
    }
}
