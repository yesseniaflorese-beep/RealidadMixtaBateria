using UnityEngine;

// Va en la RAIZ de cada estacion (la que tiene Birlos y SocketLlanta adentro).
// Al empezar el juego une sola la estacion con su llanta vieja: la LlantaCae mas cercana.
public class EstacionRueda : MonoBehaviour
{
    void Awake()
    {
        LlantaCae mejor = null;
        float mejorDistancia = float.MaxValue;

        foreach (LlantaCae l in FindObjectsByType<LlantaCae>())
        {
            float d = Vector3.Distance(transform.position, l.transform.position);
            if (d < mejorDistancia)
            {
                mejorDistancia = d;
                mejor = l;
            }
        }

        if (mejor == null)
        {
            Debug.LogWarning("[ESTACION] " + name + ": no encontre ninguna llanta con LlantaCae.");
            return;
        }

        mejor.misBirlos = GetComponentsInChildren<Birlo>();

        Debug.Log("[ESTACION] " + name + " -> " + LlantaCae.Ruta(mejor.transform)
                  + " (" + mejor.misBirlos.Length + " birlos, distancia " + mejorDistancia.ToString("F2") + " m)");

        // Aviso si hay otra estacion encima de esta
        foreach (EstacionRueda otra in FindObjectsByType<EstacionRueda>())
        {
            if (otra == this) continue;
            if (Vector3.Distance(transform.position, otra.transform.position) < 0.5f)
                Debug.LogWarning("[ESTACION] " + name + " y " + otra.name + " estan en el mismo lugar. Mueve una a su rueda.");
        }
    }
}