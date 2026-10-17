#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KorEternalFrontier;

[InitializeOnLoad]
public static class KorSceneSetup
{
    static KorSceneSetup() { EditorApplication.delayCall += EnsureScene; }

    static void EnsureScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string scenePath = "Assets/Scenes/KorFrontier.unity";
        if (File.Exists(scenePath)) return;
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var systems = new GameObject("KOR Game Systems");
        systems.AddComponent<KorGame>();
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        Debug.Log("KOR: Eternal Frontier scene created. Press Play to deploy the Kor frontier.");
    }
}
#endif
