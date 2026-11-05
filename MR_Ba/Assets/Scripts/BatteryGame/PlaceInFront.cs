using System.Collections;
using UnityEngine;

public class PlaceInFront : MonoBehaviour
{
    public Transform head;              // vacío = Camera.main
    public float distance = 1.5f;
    public float heightOffset = 0f;
    public bool follow = false;         // el panel te sigue suavemente
    public float followSpeed = 2f;

    Vector3 targetPos;
    Quaternion targetRot;
    bool ready;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);   // espera a que el visor empiece a rastrear
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (head == null)
        {
            Debug.LogWarning("PlaceInFront: no encuentro la cámara del jugador (¿tiene el tag MainCamera?)");
            yield break;
        }
        Compute();
        transform.SetPositionAndRotation(targetPos, targetRot);
        ready = true;
    }

    void Compute()
    {
        Vector3 fwd = head.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();
        targetPos = head.position + fwd * distance + Vector3.up * heightOffset;
        targetRot = Quaternion.LookRotation(fwd);
    }

    void LateUpdate()
    {
        if (!ready || !follow) return;
        Compute();
        float t = followSpeed * Time.unscaledDeltaTime;
        transform.position = Vector3.Lerp(transform.position, targetPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }
}