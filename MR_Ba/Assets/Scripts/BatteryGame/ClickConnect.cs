using UnityEngine;
using UnityEngine.InputSystem;

public class ClickConnect : MonoBehaviour
{
    public Camera cam;
    public float reach = 8f;
    public Color highlight = Color.yellow;

    ClampPolarity selected;
    MaterialPropertyBlock block;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        block = new MaterialPropertyBlock();

        foreach (var c in FindObjectsByType<ClampPolarity>())
        {
            var rb = c.GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
        }
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (!GameFlowManager.Playing || TerminalLogic.Completed) return;   // juego terminado

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        var hits = Physics.RaycastAll(ray, reach, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            var clamp = hit.collider.GetComponentInParent<ClampPolarity>();
            if (clamp != null) { OnClampClicked(clamp); return; }

            var terminal = hit.collider.GetComponentInParent<Terminal3D>();
            if (terminal != null && selected != null) { OnTerminalClicked(terminal); return; }
        }
        Select(null);
    }

    void OnClampClicked(ClampPolarity c)
    {
        if (FindTerminalOf(c) != null) return;   // ya conectada: no se puede quitar
        Select(selected == c ? null : c);
    }

    void OnTerminalClicked(Terminal3D t)
    {
        var c = selected;
        Select(null);
        if (!t.Place(c))                     // incorrecta u ocupada: vuelve a su lugar
            c.transform.SetPositionAndRotation(c.homePos, c.homeRot);
    }

    Terminal3D FindTerminalOf(ClampPolarity c)
    {
        foreach (var t in Terminal3D.All)
            if (t.occupied == c) return t;
        return null;
    }

    void Select(ClampPolarity c)
    {
        if (selected != null) SetColor(selected, false);
        selected = c;
        if (selected != null) SetColor(selected, true);
    }

    void SetColor(ClampPolarity c, bool on)
    {
        foreach (var r in c.GetComponentsInChildren<Renderer>())
        {
            if (on) { block.SetColor("_BaseColor", highlight); r.SetPropertyBlock(block); }
            else r.SetPropertyBlock(null);
        }
    }
}