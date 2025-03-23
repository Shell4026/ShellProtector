using UnityEngine;
using UnityEditor;

public class HashFloat4Editor : EditorWindow
{
    Vector4 v0 = new Vector4(112f, 97f, 115f, 115f);
    Vector4 v1 = Vector4.zero;
    Vector4 v2 = Vector4.zero;
    Vector4 v3 = Vector4.zero;

    Vector4 result;

    [MenuItem("SnowyWalk/HashFloat4 Editor")]
    public static void ShowWindow()
    {
        GetWindow<HashFloat4Editor>("HashFloat4 Editor");
    }

    void OnGUI()
    {
        // 벡터 입력
        v0 = EditorGUILayout.Vector4Field("v0", v0);
        v1 = EditorGUILayout.Vector4Field("v1", v1);
        v2 = EditorGUILayout.Vector4Field("v2", v2);
        v3 = EditorGUILayout.Vector4Field("v3", v3);

        if (GUILayout.Button("Calculate"))
        {
            result = HashFloat4(v0, v1, v2, v3);
        }

        EditorGUILayout.LabelField("Result", result.ToString("F5"));
    }

    Vector4 HashFloat4(Vector4 a, Vector4 b, Vector4 c, Vector4 d)
    {
        // (1) seed 계산
        //    float4 seed = v0*0.1031 + v1*0.11369 + v2*0.13787 + v3*0.09997;
        Vector4 seed = a * 0.1031f + b * 0.11369f + c * 0.13787f + d * 0.09997f;

        // (2) sin(seed) * 43758.5453 => frac(...)
        //    sin()과 floor()는 C#에서 Mathf.Sin, Mathf.Floor 사용
        Vector4 sinVal = new Vector4(
            Mathf.Sin(seed.x),
            Mathf.Sin(seed.y),
            Mathf.Sin(seed.z),
            Mathf.Sin(seed.w)
        );

        sinVal *= 43758.5453f;

        // frac(x) = x - floor(x)
        Vector4 fracVal = new Vector4(
            sinVal.x - Mathf.Floor(sinVal.x),
            sinVal.y - Mathf.Floor(sinVal.y),
            sinVal.z - Mathf.Floor(sinVal.z),
            sinVal.w - Mathf.Floor(sinVal.w)
        );

        return fracVal;
    }
}
