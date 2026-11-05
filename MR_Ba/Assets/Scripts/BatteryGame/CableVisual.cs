using UnityEngine;

//GENRA LA LINEA ENTRE LA PINZA Y EL CABLE 
[RequireComponent(typeof(LineRenderer))]
public class CableVisual : MonoBehaviour
{
    public Transform clampA, clampB;
    LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = lr.endWidth = 0.01f;
    }

    void LateUpdate()
    {
        lr.SetPosition(0, clampA.position);
        lr.SetPosition(1, clampB.position);
    }
}