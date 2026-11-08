using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class TableSpawner : MonoBehaviour
{
    struct TableInfo
    {
        public Vector3 center;      // centro de la superficie en el mundo (y = altura)
        public float yaw;           // orientación de la mesa (grados)
        public float sizeX, sizeZ;  // medidas en los ejes de la mesa (m)
        public string source;
    }

    public static string LastPlacement = "Buscando mesa...";
    static TableSpawner self;

    [Header("Referencias")]
    public ARPlaneManager planeManager;
    public GameObject station;                 // la Estacion
    public BoxCollider tableCollider;          // el MesaCollider
    public GameFlowManager game;               // inicia el tiempo al colocar
    public Transform head;                     // se ignora: se usa la cámara del XR Origin que tiene el AR Plane Manager

    [Header("Mesa automática")]
    public float scanSeconds = 2f;             // espera tras ver una mesa válida
    public float fallbackSeconds = 12f;        // sin mesa válida: la pone delante de ti
    public float minSide = 0.3f;               // lado mínimo de la superficie (m)
    public float maxPlaneDistance = 8f;        // ignora superficies más lejos que esto (m)
    public bool onlyMarkedTables = false;      // true = solo superficies clasificadas como Mesa
    public bool forceFloorMode = true;         // pide al XR Origin el modo Floor

    [Header("Orientación y collider")]
    public bool alignToTable = true;           // alinea la Estacion con los ejes de la mesa (elige el giro que cabe)
    public float colliderShrink = 0.02f;       // el collider es esto más chico que la mesa, por lado (m)
    public float minColliderSide = 0.4f;       // solo cuando no hay mesa detectada
    public bool showTableOutline = true;       // dibuja el contorno del collider (para verificarlo en el visor)
    public Material outlineMaterial;           // opcional: cualquier material que ya se vea (por ejemplo Linea_Roja)

    [Header("Respaldo manual")]
    public float eyeToTable = 0.85f;           // botón B: la mesa queda a esta distancia bajo tus ojos
    public float distanceInFront = 0.8f;       // botón B: distancia delante de ti
    public float controllerTipDrop = 0.05f;    // sin planos: el control queda ~5 cm sobre la superficie

    [Header("Ajuste")]
    public bool centerGroup = true;            // centra los objetos sobre el pivote de la Estacion
    public bool snapGroupToSurface = true;     // apoya lo más bajo sobre la superficie
    public float liftOnTable = 0.01f;
    public float nudgeStep = 0.01f;            // X / Y mueven 1 cm
    public bool keepScenePoseInEditor = true;

    Unity.XR.CoreUtils.XROrigin origin;
    bool placed, hasColPose;
    float timer, firstCandidateTime = -1f, trim, holdTimer;
    int pressesA, pressesB, rotQuarter;
    Vector3 colPos;
    Quaternion colRot;
    string fitInfo = "";
    LineRenderer outline;
    InputAction autoAction, frontAction, downAction, upAction, rotateAction, resetAction;

    void Awake()
    {
        self = this;
        trim = PlayerPrefs.GetFloat("tableTrim", 0f);
        rotQuarter = PlayerPrefs.GetInt("tableRot", 0);

        // A (o T): vuelve a buscar la mesa y coloca ahí (si no hay planos, donde está el control)
        autoAction = MakeAction("Auto", "<XRController>{RightHand}/primaryButton", "<Keyboard>/t");
        autoAction.performed += _ => { pressesA++; PlaceAuto(); };

        // B (o Y): delante de ti, a una altura relativa a tus ojos
        frontAction = MakeAction("Front", "<XRController>{RightHand}/secondaryButton", "<Keyboard>/y");
        frontAction.performed += _ => { pressesB++; PlaceInFront(); };

        // X (o J): baja | Y del control izquierdo (o U): sube
        downAction = MakeAction("Down", "<XRController>{LeftHand}/primaryButton", "<Keyboard>/j");
        downAction.performed += _ => Nudge(-nudgeStep);
        upAction = MakeAction("Up", "<XRController>{LeftHand}/secondaryButton", "<Keyboard>/u");
        upAction.performed += _ => Nudge(nudgeStep);

        // Clic del joystick derecho (o R): gira la Estacion 90°
        rotateAction = MakeAction("Rotate", "<XRController>{RightHand}/thumbstickClicked", "<Keyboard>/r");
        rotateAction.performed += _ => RotateStation();

        // Clic del joystick izquierdo (o K): borra el ajuste de altura y el giro guardados
        resetAction = MakeAction("Reset", "<XRController>{LeftHand}/thumbstickClicked", "<Keyboard>/k");
        resetAction.performed += _ => ResetAdjustments();
    }

    static InputAction MakeAction(string name, string pad, string key)
    {
        var a = new InputAction(name, InputActionType.Button);
        a.AddBinding(pad);
        a.AddBinding(key);
        return a;
    }

    void OnEnable()
    {
        autoAction.Enable(); frontAction.Enable();
        downAction.Enable(); upAction.Enable();
        rotateAction.Enable(); resetAction.Enable();
    }

    void OnDisable()
    {
        autoAction.Disable(); frontAction.Disable();
        downAction.Disable(); upAction.Disable();
        rotateAction.Disable(); resetAction.Disable();
    }

    void Start()
    {
        // La cámara de referencia es la del XR Origin que contiene el AR Plane Manager
        origin = planeManager != null ? planeManager.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() : null;
        if (origin == null) origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();

        Transform auto = (origin != null && origin.Camera != null) ? origin.Camera.transform
                       : (Camera.main != null ? Camera.main.transform : null);

        if (head != null && auto != null && head != auto)
            Debug.LogWarning("TableSpawner: el campo Head (" + head.name + ") no es la cámara del XR Origin con los planos (" + auto.name + "). Uso esta última.");
        if (auto != null) head = auto;

        if (forceFloorMode && origin != null)
            origin.RequestedTrackingOriginMode = Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor;

        Debug.Log("TableSpawner: cámara = " + (head != null ? Path(head) : "ninguna"));

        CreateOutline();
        station.SetActive(false);
        if (planeManager != null) planeManager.enabled = true;
    }

    float FloorY() => origin != null ? origin.transform.position.y : 0f;

    static string Path(Transform t)
    {
        string s = t.name;
        for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    // ---------- Bucle principal ----------

    void Update()
    {
        HoldNudge();
        if (placed || head == null) return;
        timer += Time.deltaTime;

        if (Application.isEditor && keepScenePoseInEditor)
        {
            if (timer > 0.5f)
            {
                LastPlacement = "EDITOR (posición de la escena)";
                placed = true;
                station.SetActive(true);
                if (game != null) game.BeginGame();
            }
            return;
        }

        ARPlane plane = FindBestPlane();
        if (plane != null)
        {
            if (firstCandidateTime < 0f) firstCandidateTime = Time.time;
            if (Time.time - firstCandidateTime >= scanSeconds) PlaceOnTable(plane, "AUTOMÁTICO");
        }
        else
        {
            firstCandidateTime = -1f;
            if (timer >= fallbackSeconds)
            {
                Debug.LogWarning("TableSpawner: no hay mesa válida, la pongo delante de ti");
                PlaceInFront();
            }
        }
    }

    // ---------- Elegir mesa ----------

    static bool IsTable(ARPlane p) => (p.classifications & PlaneClassifications.Table) != 0;

    // Centro del plano tal como se VE en el visor (el cuadro azul)
    Vector3 VisibleCenter(ARPlane p, out string how)
    {
        foreach (var r in p.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r.bounds.size.sqrMagnitude < 0.0001f) continue;
            how = "dibujo";
            return r.bounds.center;
        }

        var b = p.boundary;
        if (b.IsCreated && b.Length >= 3)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < b.Length; i++) sum += b[i];
            sum /= b.Length;
            how = "borde";
            return p.transform.TransformPoint(new Vector3(sum.x, 0f, sum.y));
        }

        how = "transform";
        return p.transform.position;
    }

    // Por qué un plano NO sirve (null = sirve)
    string Reason(ARPlane p)
    {
        bool table = IsTable(p);

        if (!table)
        {
            if (onlyMarkedTables) return "no es Mesa";
            if (p.alignment != PlaneAlignment.HorizontalUp) return "no horizontal";
            if ((p.classifications & (PlaneClassifications.Floor | PlaneClassifications.Ceiling)) != 0) return "piso/techo";
        }

        if (Mathf.Min(p.size.x, p.size.y) < minSide) return "pequeña";

        Vector3 c = VisibleCenter(p, out _);
        float dist = Vector3.Distance(head.position, c);
        if (dist > maxPlaneDistance) return "lejos " + dist.ToString("0.0") + " m";

        if (!table)
        {
            float below = head.position.y - c.y;
            if (below < 0.3f || below > 1.3f) return "altura rara (" + below.ToString("0.00") + " m bajo tus ojos)";
        }
        return null;
    }

    // Prefiere las superficies marcadas como Mesa; entre iguales, la más cercana a ti
    ARPlane FindBestPlane()
    {
        if (planeManager == null || head == null) return null;

        ARPlane best = null;
        bool bestTable = false;
        float bestDist = float.MaxValue;

        foreach (var p in planeManager.trackables)
        {
            if (Reason(p) != null) continue;

            bool table = IsTable(p);
            float d = Vector3.Distance(head.position, VisibleCenter(p, out _));

            if ((table && !bestTable) || (table == bestTable && d < bestDist))
            {
                best = p; bestTable = table; bestDist = d;
            }
        }
        return best;
    }

    // ---------- Medir la mesa ----------

    static float PlaneYaw(ARPlane p)
    {
        Vector3 f = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up);
        if (f.sqrMagnitude < 0.01f) return 0f;
        f.Normalize();
        return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    // Rectángulo de área mínima que envuelve los puntos (giro, centro y medidas reales de la mesa)
    static void FitRect(List<Vector3> pts, out float yaw, out Vector3 center, out float sx, out float sz)
    {
        float bestArea = float.MaxValue;
        yaw = 0f; sx = 0f; sz = 0f; center = Vector3.zero;

        float avgY = 0f;
        foreach (var p in pts) avgY += p.y;
        avgY /= pts.Count;

        for (int a = 0; a < 90; a++)
        {
            Quaternion inv = Quaternion.Euler(0f, -a, 0f);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var w in pts)
            {
                Vector3 l = inv * w;
                if (l.x < minX) minX = l.x;
                if (l.x > maxX) maxX = l.x;
                if (l.z < minZ) minZ = l.z;
                if (l.z > maxZ) maxZ = l.z;
            }

            float area = (maxX - minX) * (maxZ - minZ);
            if (area < bestArea - 0.00001f)
            {
                bestArea = area;
                yaw = a;
                sx = maxX - minX;
                sz = maxZ - minZ;
                center = Quaternion.Euler(0f, a, 0f) * new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            }
        }
        center.y = avgY;
    }

    TableInfo Measure(ARPlane p)
    {
        var t = new TableInfo();
        Vector3 vc = VisibleCenter(p, out string how);
        bool drawing = how == "dibujo";

        var pts = new List<Vector3>();
        var b = p.boundary;
        if (b.IsCreated && b.Length >= 3)
            for (int i = 0; i < b.Length; i++)
                pts.Add(p.transform.TransformPoint(new Vector3(b[i].x, 0f, b[i].y)));

        if (pts.Count >= 3)
        {
            FitRect(pts, out t.yaw, out t.center, out t.sizeX, out t.sizeZ);
            t.source = "borde";

            if (drawing)
            {
                // Si el borde y el dibujo no coinciden en posición, manda el dibujo (es lo que ves)
                Vector3 d = t.center - vc;
                d.y = 0f;
                if (d.magnitude > 0.3f) t.center = new Vector3(vc.x, t.center.y, vc.z);
                t.center.y = vc.y;
                t.source = "borde+dibujo";
            }
        }
        else
        {
            t.center = vc;
            t.yaw = PlaneYaw(p);
            t.sizeX = p.size.x;
            t.sizeZ = p.size.y;
            t.source = how;
        }
        return t;
    }

    // ---------- Colocar ----------

    // Botón A: busca la mesa de nuevo; si no hay planos, usa donde está el control derecho
    void PlaceAuto()
    {
        if (head == null) return;
        if (TerminalLogic.connectedCorrect > 0)
        {
            Debug.Log("TableSpawner: ya hay pinzas conectadas, no se mueve");
            return;
        }

        ARPlane plane = FindBestPlane();
        if (plane != null)
        {
            PlaceOnTable(plane, "AUTOMÁTICO (botón A)");
        }
        else if (TryGetControllerPoint(out Vector3 w))
        {
            w.y -= controllerTipDrop;
            PlaceFree(w, "CONTROL (tocaste la mesa)");
        }
        else
        {
            LastPlacement = "Sin mesa válida ni control detectado";
            Debug.LogWarning("TableSpawner: " + LastPlacement);
        }
    }

    // Botón B: delante de ti, a una altura relativa a tus ojos (no depende del piso)
    void PlaceInFront()
    {
        if (head == null) return;
        if (TerminalLogic.connectedCorrect > 0)
        {
            Debug.Log("TableSpawner: ya hay pinzas conectadas, no se mueve");
            return;
        }

        Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 p = head.position + fwd * distanceInFront;
        p.y = head.position.y - eyeToTable;
        PlaceFree(p, "DELANTE DE TI (ojos - " + eyeToTable.ToString("0.00") + " m)");
    }

    // En el CENTRO de la mesa detectada
    void PlaceOnTable(ARPlane p, string prefix)
    {
        TableInfo t = Measure(p);
        Vector3 pos = t.center;
        pos.y += trim;

        LastPlacement = prefix + " " + p.classifications + " " + t.sizeX.ToString("0.00") + "x" + t.sizeZ.ToString("0.00") +
                        " m (" + t.source + ") | " + (head.position.y - pos.y).ToString("0.00") + " m bajo tus ojos";

        PlaceStation(pos, true, t, Quaternion.identity);
    }

    void PlaceFree(Vector3 point, string source)
    {
        Vector3 away = Vector3.ProjectOnPlane(point - head.position, Vector3.up);
        if (away.sqrMagnitude < 0.01f) away = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
        away.Normalize();

        point.y += trim;
        LastPlacement = source + " | " + (head.position.y - point.y).ToString("0.00") + " m bajo tus ojos";

        PlaceStation(point, false, default(TableInfo), Quaternion.LookRotation(away));
    }

    void PlaceStation(Vector3 pos, bool hasTable, TableInfo t, Quaternion freeRot)
    {
        station.SetActive(true);
        hasColPose = false;

        station.transform.localScale = Vector3.one;
        station.transform.rotation = Quaternion.identity;
        station.transform.position = pos;

        // 1) Medir el grupo y centrarlo sobre el pivote
        Vector3 ext = Vector3.zero;
        if (GroupLocalBounds(out Vector3 mn, out Vector3 mx))
        {
            ext = mx - mn;
            if (centerGroup)
            {
                Vector3 c = (mn + mx) * 0.5f;
                foreach (Transform child in station.transform)
                {
                    if (tableCollider != null && child == tableCollider.transform) continue;
                    child.localPosition -= new Vector3(c.x, 0f, c.z);
                }
            }
        }

        // 2) Orientación: alineada con la mesa y con el giro que cabe
        Quaternion rot = freeRot;
        if (hasTable && alignToTable) rot = ChooseRotation(t, ext);
        else fitInfo = "grupo " + ext.x.ToString("0.00") + "x" + ext.z.ToString("0.00") + " m";

        station.transform.SetPositionAndRotation(pos, rot);

        // 3) Apoyar sobre la superficie y ajustar el collider a la mesa
        if (snapGroupToSurface) SnapGroupToSurface();
        PlaceCollider(hasTable, t, pos, ext);
        StopBodies();

        if (!placed) { placed = true; if (game != null) game.BeginGame(); }

        if (GroupBounds(out Bounds gb))
            Debug.Log("[Estacion] centro=" + gb.center + " tamaño=" + gb.size + " cabeza=" + head.position);
        Debug.Log("Objetos colocados en " + station.transform.position + " | " + LastPlacement + " | " + fitInfo);
    }

    // ---------- Orientación ----------

    // Elige entre 0°, 90°, 180° y 270° (respecto a los ejes de la mesa) el giro que cabe y mira hacia ti
    Quaternion ChooseRotation(TableInfo t, Vector3 ext)
    {
        Vector3 away = Vector3.ProjectOnPlane(t.center - head.position, Vector3.up);
        if (away.sqrMagnitude < 0.01f) away = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        away.Normalize();

        float availX = Mathf.Max(0.05f, t.sizeX - 2f * colliderShrink);
        float availZ = Mathf.Max(0.05f, t.sizeZ - 2f * colliderShrink);

        float OverFor(int k)
        {
            bool even = (k % 2 == 0);
            float dimX = even ? ext.x : ext.z;
            float dimZ = even ? ext.z : ext.x;
            return Mathf.Max(0f, dimX - availX) + Mathf.Max(0f, dimZ - availZ);
        }

        int bestK = 0;
        float bestScore = float.MaxValue;
        for (int k = 0; k < 4; k++)
        {
            Vector3 fwd = Quaternion.Euler(0f, t.yaw + 90f * k, 0f) * Vector3.forward;
            float score = OverFor(k) * 100f - Vector3.Dot(fwd, away);   // primero que quepa, luego que mire hacia ti
            if (score < bestScore) { bestScore = score; bestK = k; }
        }

        int finalK = (bestK + rotQuarter) % 4;
        float finalYaw = t.yaw + 90f * finalK;
        float over = OverFor(finalK);

        fitInfo = "grupo " + ext.x.ToString("0.00") + "x" + ext.z.ToString("0.00") + " m | mesa " +
                  t.sizeX.ToString("0.00") + "x" + t.sizeZ.ToString("0.00") + " m | giro " +
                  Mathf.Repeat(finalYaw, 360f).ToString("0") + "° | desborde " + over.ToString("0.00") + " m" +
                  (over > 0.02f ? " (NO CABE: junta los objetos)" : "");

        return Quaternion.Euler(0f, finalYaw, 0f);
    }

    // Clic del joystick derecho: gira la Estacion 90° y lo recuerda
    void RotateStation()
    {
        if (!placed || !station.activeSelf) return;
        if (TerminalLogic.connectedCorrect > 0)
        {
            Debug.Log("TableSpawner: ya hay pinzas conectadas, no se gira");
            return;
        }

        rotQuarter = (rotQuarter + 1) % 4;
        PlayerPrefs.SetInt("tableRot", rotQuarter);
        station.transform.Rotate(0f, 90f, 0f, Space.World);
        ApplyColliderPose();
        Debug.Log("TableSpawner: giro extra " + (rotQuarter * 90) + "°");
    }

    // ---------- Collider de la mesa ----------

    // El collider copia la mesa: mismo centro, misma orientación y su tamaño (menos un margen)
    void PlaceCollider(bool hasTable, TableInfo t, Vector3 pos, Vector3 ext)
    {
        if (tableCollider == null) return;

        tableCollider.isTrigger = false;
        tableCollider.transform.localScale = Vector3.one;

        if (hasTable)
        {
            colPos = new Vector3(t.center.x, pos.y, t.center.z);
            colRot = Quaternion.Euler(0f, t.yaw, 0f);
            hasColPose = true;
            ApplyColliderPose();

            tableCollider.size = new Vector3(
                Mathf.Max(0.1f, t.sizeX - 2f * colliderShrink), 0.1f,
                Mathf.Max(0.1f, t.sizeZ - 2f * colliderShrink));
            tableCollider.center = new Vector3(0f, -0.05f, 0f);   // su cara de arriba queda en la superficie
        }
        else
        {
            tableCollider.transform.localPosition = Vector3.zero;
            tableCollider.transform.localRotation = Quaternion.identity;
            tableCollider.size = new Vector3(Mathf.Max(minColliderSide, ext.x + 0.1f), 0.1f, Mathf.Max(minColliderSide, ext.z + 0.1f));
            tableCollider.center = new Vector3(0f, -0.05f, 0f);
        }
    }

    void ApplyColliderPose()
    {
        if (hasColPose && tableCollider != null)
            tableCollider.transform.SetPositionAndRotation(colPos, colRot);
    }

    // Contorno del collider, para verificar en el visor que coincide con la mesa
    void CreateOutline()
    {
        if (!showTableOutline) return;

        Material mat = outlineMaterial;
        if (mat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null)
            {
                Debug.LogWarning("TableSpawner: no hay material para el contorno. Asigna Outline Material.");
                return;
            }
            mat = new Material(sh);
            mat.color = Color.cyan;
        }

        var go = new GameObject("ContornoMesa");
        go.transform.SetParent(transform, false);
        outline = go.AddComponent<LineRenderer>();
        outline.useWorldSpace = true;
        outline.loop = true;
        outline.positionCount = 4;
        outline.startWidth = outline.endWidth = 0.012f;
        outline.sharedMaterial = mat;
        outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outline.receiveShadows = false;
        outline.enabled = false;
    }

    void LateUpdate()
    {
        if (outline == null) return;

        bool show = showTableOutline && placed && station.activeSelf && tableCollider != null;
        outline.enabled = show;
        if (!show) return;

        Vector3 c = tableCollider.center;
        Vector3 h = tableCollider.size * 0.5f;
        float y = c.y + h.y + 0.003f;
        Transform tr = tableCollider.transform;

        outline.SetPosition(0, tr.TransformPoint(new Vector3(c.x - h.x, y, c.z - h.z)));
        outline.SetPosition(1, tr.TransformPoint(new Vector3(c.x + h.x, y, c.z - h.z)));
        outline.SetPosition(2, tr.TransformPoint(new Vector3(c.x + h.x, y, c.z + h.z)));
        outline.SetPosition(3, tr.TransformPoint(new Vector3(c.x - h.x, y, c.z + h.z)));
    }

    // ---------- Control derecho ----------

    bool TryGetControllerPoint(out Vector3 world)
    {
        world = Vector3.zero;

        var dev = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (dev.isValid && origin != null && origin.CameraFloorOffsetObject != null &&
            dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 local))
        {
            world = origin.CameraFloorOffsetObject.transform.TransformPoint(local);
            return true;
        }

        if (origin != null)
            foreach (var t in origin.GetComponentsInChildren<Transform>(false))
                if (t.name == "Right Controller") { world = t.position; return true; }

        return false;
    }

    // ---------- Medidas del grupo ----------

    bool IsGroupRenderer(Renderer r)
    {
        if (r is ParticleSystemRenderer || r is LineRenderer) return false;
        if (tableCollider != null && r.transform.IsChildOf(tableCollider.transform)) return false;
        return true;
    }

    // Medidas en los ejes locales de la Estacion (sirve con la Estacion sin girar)
    bool GroupLocalBounds(out Vector3 min, out Vector3 max)
    {
        min = Vector3.positiveInfinity;
        max = Vector3.negativeInfinity;
        bool any = false;

        foreach (var r in station.GetComponentsInChildren<Renderer>())
        {
            if (!IsGroupRenderer(r)) continue;

            Bounds b = r.bounds;
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 l = station.transform.InverseTransformPoint(corner);
                min = Vector3.Min(min, l);
                max = Vector3.Max(max, l);
            }
            any = true;
        }
        return any;
    }

    bool GroupBounds(out Bounds b)
    {
        b = new Bounds();
        bool any = false;
        foreach (var r in station.GetComponentsInChildren<Renderer>())
        {
            if (!IsGroupRenderer(r)) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    // Apoya lo más bajo de lo que hay en la Estacion sobre la superficie
    void SnapGroupToSurface()
    {
        if (!GroupBounds(out Bounds b))
        {
            Debug.LogWarning("TableSpawner: no encontré objetos con Renderer dentro de Estacion");
            return;
        }

        float delta = Mathf.Clamp(station.transform.position.y + liftOnTable - b.min.y, -1.5f, 1.5f);
        foreach (Transform child in station.transform)
        {
            if (tableCollider != null && child == tableCollider.transform) continue;
            child.position += Vector3.up * delta;
        }
    }

    // ---------- Ajuste fino de altura ----------

    void ShiftY(float dy)
    {
        station.transform.position += Vector3.up * dy;
        if (hasColPose) { colPos.y += dy; ApplyColliderPose(); }
        StopBodies();
    }

    void Nudge(float dy)
    {
        trim += dy;
        PlayerPrefs.SetFloat("tableTrim", trim);

        if (!placed || !station.activeSelf) return;
        ShiftY(dy);
    }

    // Manteniendo X o Y más de medio segundo, se mueve de forma continua (10 cm/s)
    void HoldNudge()
    {
        float dir = downAction.IsPressed() ? -1f : upAction.IsPressed() ? 1f : 0f;

        if (dir == 0f)
        {
            if (holdTimer > 0.5f) PlayerPrefs.SetFloat("tableTrim", trim);
            holdTimer = 0f;
            return;
        }

        holdTimer += Time.unscaledDeltaTime;
        if (holdTimer <= 0.5f) return;

        float dy = dir * 0.1f * Time.unscaledDeltaTime;
        trim += dy;
        if (placed && station.activeSelf) ShiftY(dy);
    }

    void ResetAdjustments()
    {
        float oldTrim = trim;
        trim = 0f;
        rotQuarter = 0;
        PlayerPrefs.SetFloat("tableTrim", 0f);
        PlayerPrefs.SetInt("tableRot", 0);

        if (placed && station.activeSelf) ShiftY(-oldTrim);
        Debug.Log("TableSpawner: ajustes reiniciados. Pulsa A para volver a colocar la Estacion");
    }

    void StopBodies()
    {
        foreach (var rb in station.GetComponentsInChildren<Rigidbody>())
        {
            if (rb.isKinematic) continue;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // ---------- Texto para el panel de depuración ----------

    static string Relative(Vector3 world)
    {
        Transform hd = self.head;
        Vector3 d = world - hd.position;
        Vector3 flat = new Vector3(d.x, 0f, d.z);
        Vector3 fwd = Vector3.ProjectOnPlane(hd.forward, Vector3.up);
        float ang = (flat.sqrMagnitude < 0.0001f || fwd.sqrMagnitude < 0.0001f)
            ? 0f : Vector3.SignedAngle(fwd, flat, Vector3.up);

        string side = Mathf.Abs(ang) < 30f ? "al frente"
                    : Mathf.Abs(ang) > 150f ? "detrás de ti"
                    : ang > 0f ? "a tu derecha" : "a tu izquierda";

        return flat.magnitude.ToString("0.0") + " m " + side + ", " + (-d.y).ToString("0.00") + " m bajo tus ojos";
    }

    public static string Where()
    {
        if (self == null || self.head == null) return "";

        var sb = new StringBuilder();
        sb.AppendLine("Pulsaciones  A=" + self.pressesA + "  B=" + self.pressesB + " | giro extra " + (self.rotQuarter * 90) + "°");
        sb.AppendLine("Cámara usada: " + Path(self.head) + " en " + self.head.position.ToString("0.00"));

        var origins = Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None);
        sb.AppendLine("XR Origins: " + origins.Length + " | cámaras activas: " + Camera.allCamerasCount);

        if (self.origin != null)
            sb.AppendLine("Cabeza sobre el piso: " + (self.head.position.y - self.FloorY()).ToString("0.00") + " m");

        var cables = Object.FindObjectsByType<CableVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cables.Length == 0) sb.AppendLine("CABLES: no hay ningún CableVisual en la escena");
        foreach (var cv in cables) sb.AppendLine("cable " + cv.name + ": " + cv.Describe());

        if (!self.station.activeSelf) { sb.AppendLine("Estacion: sin colocar"); return sb.ToString(); }

        if (!string.IsNullOrEmpty(self.fitInfo)) sb.AppendLine(self.fitInfo);

        Vector3 where = self.GroupBounds(out Bounds b) ? b.center : self.station.transform.position;
        sb.AppendLine("Objetos: " + Relative(where));

        float minH = float.MaxValue;
        foreach (var c in self.station.GetComponentsInChildren<ClampPolarity>())
            minH = Mathf.Min(minH, c.transform.position.y - self.station.transform.position.y);
        if (minH != float.MaxValue) sb.AppendLine("pinza más baja a " + minH.ToString("0.00") + " m de la mesa");

        if (self.tableCollider != null)
            sb.AppendLine("collider mesa: " + self.tableCollider.size.x.ToString("0.00") + "x" + self.tableCollider.size.z.ToString("0.00") + " m");

        sb.AppendLine("ajuste de altura: " + (self.trim * 100f).ToString("0") + " cm");
        return sb.ToString();
    }

    public static string DescribePlanes()
    {
        if (self == null || self.planeManager == null || self.head == null) return "";

        var sb = new StringBuilder();
        int i = 0;
        foreach (var p in self.planeManager.trackables)
        {
            if (++i > 5) break;

            Vector3 vc = self.VisibleCenter(p, out string how);
            TableInfo m = self.Measure(p);
            string r = self.Reason(p);

            sb.AppendLine(p.classifications + " " + p.size.x.ToString("0.0") + "x" + p.size.y.ToString("0.0") + " -> " + (r ?? "OK"));
            sb.AppendLine("  " + Relative(vc) + " (" + how + ")");
            sb.AppendLine("  medido: " + m.sizeX.ToString("0.00") + "x" + m.sizeZ.ToString("0.00") + " m, giro " + m.yaw.ToString("0") + "° (" + m.source + ")");
        }
        return sb.ToString();
    }
}