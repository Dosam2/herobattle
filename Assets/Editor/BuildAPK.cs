using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

public static class BuildAPK
{
    [MenuItem("Build/URP 셰이더 Always Included 정리 (경량화)")]
    public static void CleanupAlwaysIncludedShaders()
    {
        var graphicsSettings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        SerializedObject so = new SerializedObject(graphicsSettings);
        SerializedProperty arrayProp = so.FindProperty("m_AlwaysIncludedShaders");

        // 제거할 무거운 셰이더 목록
        string[] heavyShaders = new string[]
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Simple Lit",
            "TextMeshPro/Mobile/Distance Field",
        };

        int removed = 0;
        for (int i = arrayProp.arraySize - 1; i >= 0; i--)
        {
            var shaderRef = arrayProp.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            if (shaderRef != null)
            {
                foreach (string name in heavyShaders)
                {
                    if (shaderRef.name == name)
                    {
                        arrayProp.DeleteArrayElementAtIndex(i);
                        // Unity는 objectReference를 null로 만든 뒤 한번 더 삭제해야 실제 제거됨
                        if (i < arrayProp.arraySize && arrayProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
                            arrayProp.DeleteArrayElementAtIndex(i);
                        removed++;
                        Debug.Log($"[Shader] Always Included에서 제거: {name}");
                        break;
                    }
                }
            }
        }

        so.ApplyModifiedProperties();
        Debug.Log($"[Shader] 완료! {removed}개 무거운 셰이더 제거됨. 빌드가 훨씬 빨라집니다.");
    }

    [MenuItem("Build/셰이더 참조 머터리얼 생성 (Resources)")]
    public static void CreateShaderReferenceMaterials()
    {
        string folder = "Assets/Resources/ShaderRef";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Resources", "ShaderRef");

        // URP Lit
        CreateRefMaterial(folder, "RefLit", "Universal Render Pipeline/Lit", Color.white);
        // URP Unlit (투명)
        var unlitMat = CreateRefMaterial(folder, "RefUnlit", "Universal Render Pipeline/Unlit", new Color(1, 1, 1, 0.5f));
        if (unlitMat != null)
        {
            unlitMat.SetFloat("_Surface", 1f);
            unlitMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(unlitMat);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Shader] Resources/ShaderRef에 참조 머터리얼 생성 완료. 빌드 시 셰이더가 자동 포함됩니다.");
    }

    private static Material CreateRefMaterial(string folder, string name, string shaderName, Color color)
    {
        string path = $"{folder}/{name}.mat";
        Shader shader = Shader.Find(shaderName);
        if (shader == null) { Debug.LogWarning($"셰이더 없음: {shaderName}"); return null; }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log($"[Shader] 생성: {path}");
        }
        return mat;
    }

    [MenuItem("Build/Android APK (바탕화면)")]
    public static void BuildAndroidToDesktop()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError("[Build] Android Build Support 모듈이 설치되어 있지 않습니다!");
            EditorUtility.DisplayDialog(
                "Android 모듈 미설치",
                "Android Build Support가 설치되어 있지 않습니다.\n\n" +
                "Unity Hub → Installs → 톱니바퀴(⚙) → Add Modules\n" +
                "→ 'Android Build Support' 체크 후 설치하세요.",
                "확인");
            return;
        }

        // 셰이더 참조 머터리얼 확인
        CreateShaderReferenceMaterials();

        // 바탕화면 경로
        string desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string apkPath = Path.Combine(desktop, "HeroBattle.apk");

        // 빌드할 씬 목록
        string[] scenes = new string[]
        {
            "Assets/Scenes/TestScene.unity"
        };

        // 빌드 옵션
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        Debug.Log($"[Build] Android APK 빌드 시작... 출력: {apkPath}");

        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[Build] APK 빌드 성공! 크기: {report.summary.totalSize / (1024 * 1024)}MB");
            Debug.Log($"[Build] 경로: {apkPath}");
            EditorUtility.RevealInFinder(apkPath);
        }
        else
        {
            Debug.LogError($"[Build] APK 빌드 실패: {report.summary.result}");
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error)
                        Debug.LogError(msg.content);
                }
            }
        }
    }

    [MenuItem("Build/설치된 빌드 타겟 확인")]
    public static void CheckBuildTargets()
    {
        Debug.Log("═══ 설치된 빌드 타겟 확인 ═══");
        Debug.Log($"현재 플랫폼: {EditorUserBuildSettings.activeBuildTarget}");
        Debug.Log($"Android: {(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android) ? "설치됨" : "미설치")}");
        Debug.Log($"iOS: {(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS) ? "설치됨" : "미설치")}");
        Debug.Log($"Windows: {(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64) ? "설치됨" : "미설치")}");
        Debug.Log($"WebGL: {(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL) ? "설치됨" : "미설치")}");
    }
}
