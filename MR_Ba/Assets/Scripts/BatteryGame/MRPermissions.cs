using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class MRPermissions : MonoBehaviour
{
    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        const string scene = "com.oculus.permission.USE_SCENE";
        if (!Permission.HasUserAuthorizedPermission(scene))
            Permission.RequestUserPermission(scene);
#endif
    }
}