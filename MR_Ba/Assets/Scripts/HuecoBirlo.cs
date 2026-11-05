using UnityEngine;

// Va en cada Hueco. Los huecos deben ser HIJOS de su SocketLlanta,
// asi solo se activan cuando hay una llanta nueva montada en ese mismo eje.
public class HuecoBirlo : MonoBehaviour
{
    public float radio = 0.05f;   // que tan cerca debe quedar el birlo para entrar al hueco

    private MontajeLlanta montaje;
    private bool activo;
    private bool ocupado;

    void Start()
    {
        montaje = GetComponentInParent<MontajeLlanta>();

        if (montaje != null)
        {
            montaje.LlantaMontada += Activar;
            montaje.LlantaRetirada += Desactivar;
        }
        else
        {
            Debug.LogWarning(name + ": no esta dentro de un SocketLlanta con MontajeLlanta.");
        }
    }

    void OnDestroy()
    {
        if (montaje != null)
        {
            montaje.LlantaMontada -= Activar;
            montaje.LlantaRetirada -= Desactivar;
        }
    }

    void Activar()
    {
        activo = true;
    }

    void Desactivar()
    {
        activo = false;
    }

    void Update()
    {
        if (!activo || ocupado) return;

        Collider[] cercanos = Physics.OverlapSphere(transform.position, radio);

        foreach (Collider c in cercanos)
        {
            Birlo b = c.GetComponentInParent<Birlo>();
            if (b == null) continue;
            if (b.estado != Birlo.Estado.Suelto) continue;
            if (b.EstaAgarrado) continue;   // espera a que lo sueltes

            b.Colocar(transform);
            ocupado = true;
            Debug.Log("[HUECO] " + name + " (" + LlantaCae.Ruta(transform) + ") coloco un birlo");

            // Con un birlo puesto, la llanta ya no se puede quitar
            if (montaje != null) montaje.BloquearLlanta();
            return;
        }
    }

    // Para ver la esfera del hueco en la Scene
    void OnDrawGizmos()
    {
        Gizmos.color = ocupado ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radio);
    }
}