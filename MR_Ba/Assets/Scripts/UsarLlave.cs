using UnityEngine;

// Version de escritorio (mouse). En VR se usa LlaveVR.
public class UsarLlave : MonoBehaviour
{
    public AgarrarObjeto agarrar;   // el script AgarrarObjeto de la camara
    public float distancia = 2f;

    private Birlo birloMirado;

    void Update()
    {
        birloMirado = null;

        // Solo funciona si tienes la llave en la mano
        Rigidbody mano = agarrar.ObjetoAgarrado;
        if (mano == null || mano.GetComponent<LlaveBirlos>() == null) return;

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, distancia))
            birloMirado = hit.collider.GetComponentInParent<Birlo>();

        if (birloMirado == null) return;

        if (Input.GetMouseButton(0)) birloMirado.Girar(-1f); // clic izquierdo: aflojar
        if (Input.GetMouseButton(1)) birloMirado.Girar(1f);  // clic derecho: apretar
    }

    void OnGUI()
    {
        if (birloMirado == null) return;

        string texto = "Apriete: " + Mathf.RoundToInt(birloMirado.apriete * 100f) + "%";
        GUI.Label(new Rect(Screen.width / 2f - 80f, Screen.height / 2f + 30f, 200f, 30f), texto);
    }
}