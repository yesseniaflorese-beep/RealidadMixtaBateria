using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class PlaneDebugHUD : MonoBehaviour
{
    public ARPlaneManager planeManager;
    public TMP_Text label;
    float next;

    void Update()
    {
        if (Time.time < next || label == null) return;
        next = Time.time + 1f;

        var sb = new StringBuilder();
        sb.AppendLine("Planos: " + (planeManager != null ? planeManager.trackables.count : 0));
        sb.AppendLine("Colocación: " + TableSpawner.LastPlacement);
        sb.AppendLine(TableSpawner.Where());
        sb.Append(TableSpawner.DescribePlanes());
        label.text = sb.ToString();
    }
}