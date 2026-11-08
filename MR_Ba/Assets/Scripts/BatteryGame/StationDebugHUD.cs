using System.Text;
using TMPro;
using UnityEngine;

public class StationDebugHUD : MonoBehaviour
{
    public Transform station;
    public TMP_Text label;
    float next;

    void Update()
    {
        if (Time.time < next || station == null || label == null) return;
        next = Time.time + 1f;

        var sb = new StringBuilder();
        sb.AppendLine($"Estacion activa: {station.gameObject.activeSelf} | Y={station.position.y:0.00}");
        int i = 0;
        foreach (var c in station.GetComponentsInChildren<ClampPolarity>(true))
        {
            if (++i > 4) break;
            sb.AppendLine($"{c.name}: {c.transform.position.y - station.position.y:0.00} m sobre la mesa");
        }
        label.text = sb.ToString();
    }
}