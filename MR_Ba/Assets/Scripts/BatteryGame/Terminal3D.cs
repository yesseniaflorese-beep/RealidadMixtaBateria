using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TerminalLogic))]
public class Terminal3D : MonoBehaviour
{
    public static readonly List<Terminal3D> All = new List<Terminal3D>();

    public Transform anchor;                    // opcional: dónde queda la punta
    [HideInInspector] public ClampPolarity occupied;

    TerminalLogic logic;
    Transform A => anchor != null ? anchor : transform;

    void Awake() => logic = GetComponent<TerminalLogic>();
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public bool Place(ClampPolarity c)
    {
        if (occupied != null) return false;
        if (!logic.TryConnect(c)) return false;   // polaridad incorrecta: chispas y penalización

        var rb = c.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;
        c.transform.rotation = A.rotation;
        c.transform.position += A.position - c.TipPosition;
        occupied = c;
        return true;
    }

    public void Release()
    {
        if (occupied == null) return;
        occupied = null;
        logic.Disconnect();
    }
}