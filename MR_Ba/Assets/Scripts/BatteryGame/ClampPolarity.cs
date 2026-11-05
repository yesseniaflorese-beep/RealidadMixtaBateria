using UnityEngine;

public enum Polarity { Positive, Negative }

public class ClampPolarity : MonoBehaviour
{
    public Polarity polarity;
    public Transform tip;                       // opcional: la punta de la pinza
    [HideInInspector] public Vector3 homePos;
    [HideInInspector] public Quaternion homeRot;

    public Vector3 TipPosition => tip != null ? tip.position : transform.position;

    void Awake()
    {
        homePos = transform.position;
        homeRot = transform.rotation;
    }
}