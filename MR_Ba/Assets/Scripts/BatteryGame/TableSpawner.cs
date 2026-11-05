using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class TableSpawner : MonoBehaviour
{
    public ARPlaneManager planeManager;
    public GameObject station;            // el objeto Estacion
    public BoxCollider tableCollider;     // el MesaCollider
    public GameFlowManager game;          // opcional: inicia el tiempo al colocar la mesa
    public float minSide = 0.4f;          // lado mínimo aceptable de la mesa (m)
    public float fallbackSeconds = 6f;    // si no hay mesa, usa una virtual
    public Transform head;                // vacío = Camera.main

    bool placed;
    float timer;

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        station.SetActive(false);
    }

    void OnEnable()
    {
        if (planeManager != null) planeManager.trackablesChanged.AddListener(OnPlanes);
    }

    void OnDisable()
    {
        if (planeManager != null) planeManager.trackablesChanged.RemoveListener(OnPlanes);
    }

    void OnPlanes(ARTrackablesChangedEventArgs<ARPlane> args)
    {
        if (placed) return;
        foreach (var p in args.added) { Debug.Log($"Plano: {p.classifications}, {p.size}"); TryPlace(p); }
        foreach (var p in args.updated) TryPlace(p);   // la clasificación puede llegar después
    }

    void TryPlace(ARPlane p)
    {
        if (placed) return;
        bool isTable = (p.classifications & PlaneClassifications.Table) != 0;
        bool bigEnough = Mathf.Min(p.size.x, p.size.y) >= minSide;
        if (!isTable || !bigEnough) return;

        Vector3 pos = p.transform.TransformPoint(p.center);
        if (tableCollider != null)
            tableCollider.size = new Vector3(p.size.x, 0.02f, p.size.y);
        Place(pos);
    }

    void Update()
    {
        if (placed) return;
        timer += Time.deltaTime;
        if (timer < fallbackSeconds || head == null) return;

        // Mesa virtual de respaldo (editor, o si no se detectó ninguna)
        Vector3 fwd = head.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 pos = head.position + fwd * 0.8f;
        pos.y = head.position.y - 0.7f;
        Debug.Log("TableSpawner: no se detectó mesa, uso mesa virtual de respaldo");
        Place(pos);
    }

    void Place(Vector3 pos)
    {
        placed = true;

        Vector3 away = pos - (head != null ? head.position : Vector3.zero);
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Vector3.forward;

        station.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(away.normalized));
        station.SetActive(true);
        if (game != null) game.BeginGame();
        Debug.Log($"Objetos colocados en {pos}");
    }
}
