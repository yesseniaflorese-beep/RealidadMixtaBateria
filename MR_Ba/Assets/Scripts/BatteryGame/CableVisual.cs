using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class CableVisual : MonoBehaviour
{
    public Polarity polarity;                  // solo se usa si el nombre no dice "roj" ni "neg"
    public Transform clampA, clampB;           // si están vacías o rotas, se buscan solas
    public bool autoFind = true;
    public int segments = 12;
    public float sag = 0.08f;
    public float width = 0.02f;
    public Color fallbackColor = Color.red;    // solo si el Line Renderer no tiene material

    LineRenderer lr;
    string status = "iniciando";
    float nextSearch;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = segments + 1;
        lr.startWidth = lr.endWidth = width;
        lr.numCapVertices = 4;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        if (lr.sharedMaterial == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                var m = new Material(sh);
                m.color = fallbackColor;
                lr.material = m;
            }
        }
    }

    Polarity WantedPolarity()
    {
        string n = name.ToLower();
        if (n.Contains("neg") || n.Contains("black")) return Polarity.Negative;
        if (n.Contains("roj") || n.Contains("red")) return Polarity.Positive;
        return polarity;
    }

    void TryLink()
    {
        Polarity want = WantedPolarity();
        var found = new System.Collections.Generic.List<ClampPolarity>();

        foreach (var c in FindObjectsByType<ClampPolarity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.polarity == want) found.Add(c);

        found.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        if (found.Count < 2)
        {
            status = "no encuentro 2 pinzas " + want + " (hay " + found.Count + ")";
            return;
        }

        clampA = found[0].transform;
        clampB = found[1].transform;
        Debug.Log("CableVisual (" + name + "): enlazado con " + clampA.name + " y " + clampB.name);
    }

    void LateUpdate()
    {
        if (clampA == null || clampB == null)
        {
            if (autoFind && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                TryLink();
            }
            if (clampA == null || clampB == null)
            {
                lr.enabled = false;
                if (!status.StartsWith("no encuentro")) status = "faltan las pinzas (Clamp A / Clamp B)";
                return;
            }
        }

        bool visible = clampA.gameObject.activeInHierarchy && clampB.gameObject.activeInHierarchy;
        lr.enabled = visible;
        if (!visible) { status = "pinzas inactivas (la Estacion aún no se coloca)"; return; }

        status = "dibujando entre " + clampA.name + " y " + clampB.name;

        Vector3 a = clampA.position, b = clampB.position;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y -= sag * 4f * t * (1f - t);
            lr.SetPosition(i, p);
        }
    }

    // Texto para el panel de depuración
    public string Describe()
    {
        if (!isActiveAndEnabled) return "INACTIVO (el objeto o el script están desactivados)";
        return status + ((lr != null && lr.sharedMaterial == null) ? " | SIN MATERIAL" : "");
    }
}