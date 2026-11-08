using System.Collections;
using UnityEngine;

public class VirtualTable : MonoBehaviour
{
    public GameObject station;               // la Estacion
    public GameFlowManager game;             // inicia el tiempo al colocar la mesa
    public Transform head;                   // vacío = cámara del XR Origin
    public Transform floorReference;         // el XR Origin: su Y es el piso
    public float distance = 0.9f;            // metros delante de ti
    public float tableHeight = 0.75f;        // metros sobre el piso
    public Vector2 tableSize = new Vector2(1.2f, 0.7f);
    public Color tableColor = new Color(0.55f, 0.38f, 0.22f);
    public float liftOnTable = 0.01f;

    IEnumerator Start()
    {
        station.SetActive(false);

        if (head == null)
        {
            var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null && origin.Camera != null) head = origin.Camera.transform;
            else if (Camera.main != null) head = Camera.main.transform;
        }
        if (head == null) { Debug.LogWarning("VirtualTable: no encuentro la cámara"); yield break; }

        // Espera a que el visor empiece a rastrear
        float t = 0f;
        while (head.position.y < 0.5f && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }

        Vector3 fwd = head.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();
        Quaternion rot = Quaternion.LookRotation(fwd);

        float floorY = floorReference != null ? floorReference.position.y : 0f;
        Vector3 surface = head.position + fwd * distance;
        surface.y = floorY + tableHeight;

        // Mesa visible con collider (su cara de arriba queda justo en "surface")
        var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "MesaVirtual";
        table.transform.localScale = new Vector3(tableSize.x, 0.06f, tableSize.y);
        table.transform.SetPositionAndRotation(surface - Vector3.up * 0.03f, rot);
        table.GetComponent<Renderer>().material.color = tableColor;

        // Estacion encima de la mesa
        station.transform.SetPositionAndRotation(surface, rot);
        station.SetActive(true);
        SnapToSurface();

        foreach (var rb in station.GetComponentsInChildren<Rigidbody>())
        {
            if (rb.isKinematic) continue;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (game != null) game.BeginGame();
        Debug.Log($"VirtualTable: mesa en {surface}, a {tableHeight} m del piso");
    }

    // Apoya lo más bajo del grupo sobre la mesa, sin importar a qué altura lo dejaste
    void SnapToSurface()
    {
        bool any = false;
        Bounds b = new Bounds();

        foreach (var r in station.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is LineRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return;

        float delta = station.transform.position.y + liftOnTable - b.min.y;
        foreach (Transform child in station.transform)
            child.position += Vector3.up * delta;
    }
}