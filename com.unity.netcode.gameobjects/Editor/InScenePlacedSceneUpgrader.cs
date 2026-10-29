using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Netcode.Editor
{
    /// <summary>
    /// NGO 2.13 decides whether a NetworkObject is in-scene placed from a serialized flag (InScenePlaced) that is
    /// written when the scene is saved. Scenes saved with older versions of the package don't have it, so their
    /// NetworkObjects are treated as unregistered runtime-created objects and are never spawned.
    /// This tool opens every scene under Assets/, sets the flag on each NetworkObject, and saves scenes that changed.
    /// </summary>
    internal static class InScenePlacedSceneUpgrader
    {
        [MenuItem("Tools/Netcode/Mark In-Scene NetworkObjects In All Scenes")]
        private static void MarkAllScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var scenePaths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                scenePaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            int scenesChanged = 0, objectsChanged = 0;
            try
            {
                for (int i = 0; i < scenePaths.Count; i++)
                {
                    var path = scenePaths[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Marking in-scene NetworkObjects", path, (float)i / scenePaths.Count))
                    {
                        break;
                    }

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var changedInScene = 0;
                    foreach (var networkObject in FindObjects.FromSceneByType<NetworkObject>(scene, true))
                    {
                        if (networkObject.EditorRefreshInScenePlaced())
                        {
                            changedInScene++;
                            EditorUtility.SetDirty(networkObject);
                            if (PrefabUtility.IsPartOfPrefabInstance(networkObject))
                            {
                                // Store the flag as a prefab override so it is saved with the scene.
                                PrefabUtility.RecordPrefabInstancePropertyModifications(networkObject);
                            }
                        }
                    }

                    if (changedInScene > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        scenesChanged++;
                        objectsChanged += changedInScene;
                        Debug.Log($"[Netcode] Marked {changedInScene} NetworkObject(s) as in-scene placed in {path}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (originalSetup != null && originalSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
            }

            Debug.Log($"[Netcode] In-scene placed upgrade done: {objectsChanged} NetworkObject(s) updated across {scenesChanged} of {scenePaths.Count} scene(s).");
        }
    }
}
