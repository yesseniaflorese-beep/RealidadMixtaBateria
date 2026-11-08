using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ResetIfFallen : MonoBehaviour
{
    public Transform station;          // la Estacion (vacío = el padre)
    public float fallDistance = 0.4f;  // metros por debajo de la mesa

    Rigidbody rb;
    Vector3 localPos;
    Quaternion localRot;

    void Awake() => rb = GetComponent<Rigidbody>();

    void Start()
    {
        if (station == null) station = transform.parent;
        localPos = station.InverseTransformPoint(transform.position);
        localRot = Quaternion.Inverse(station.rotation) * transform.rotation;
    }

    void Update()
    {
        if (transform.position.y < station.position.y - fallDistance) Respawn();
    }

    void Respawn()
    {
        Debug.Log($"ResetIfFallen: {name} cayó, la regreso a la mesa");
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(station.TransformPoint(localPos),
                                         station.rotation * localRot);
    }
}