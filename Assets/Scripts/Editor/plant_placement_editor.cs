using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[CustomEditor(typeof(plant_placement))]
public class plant_placement_editor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        plant_placement placement =
            (plant_placement)target;

        EditorGUILayout.Space(15);

        EditorGUILayout.LabelField(
            "Vegetation Generation",
            EditorStyles.boldLabel
        );

        if (GUILayout.Button("Load Seeds", GUILayout.Height(30)))
        {
            LoadSeeds(placement);
        }

        if (GUILayout.Button("Place Plants", GUILayout.Height(30)))
        {
            PlacePlants(placement);
        }

        if (GUILayout.Button("Place Random Plants", GUILayout.Height(30)))
        {
            PlaceRandomPlants(placement);
        }

        if (GUILayout.Button("Clear Plants", GUILayout.Height(30)))
        {
            ClearPlants(placement);
        }
    }
    void LoadSeeds(plant_placement placement)
    {
        map_manager manager =
            placement.GetComponent<map_manager>();

        if (manager == null)
        {
            Debug.LogError(
                "map_manager was not found on this GameObject."
            );

            return;
        }

        if (placement.plantAnalysis == null)
        {
            Debug.LogError(
                "Plant Analysis is not assigned."
            );

            return;
        }

        bool loaded =
            placement.plantAnalysis.LoadSeedsFromJson(
                manager.species
            );

        if (loaded)
        {
            Debug.Log("Seeds loaded successfully.");
        }
    }

    void PlacePlants(plant_placement placement)
    {
        EnsureVegetationParentWithUndo(placement);

        placement.PlacePlants();

        EditorUtility.SetDirty(placement);
    }

    void PlaceRandomPlants(plant_placement placement)
    {
        map_manager manager =
            placement.GetComponent<map_manager>();

        if (manager == null)
        {
            Debug.LogError(
                "map_manager was not found on this GameObject."
            );

            return;
        }

        EnsureVegetationParentWithUndo(placement);

        placement.PlaceRandomPlants(manager.species);

        EditorUtility.SetDirty(placement);
    }

    void ClearPlants(plant_placement placement)
    {
        if (placement.vegetationParent == null)
        {
            Debug.LogWarning(
                "No Generated Vegetation object found."
            );

            return;
        }

        Transform parent =
            placement.vegetationParent;

        Undo.RegisterFullObjectHierarchyUndo(
            parent.gameObject,
            "Clear Generated Vegetation"
        );

        while (parent.childCount > 0)
        {
            DestroyImmediate(parent.GetChild(0).gameObject);
        }

        Debug.Log("Generated vegetation cleared.");

        EditorUtility.SetDirty(placement);
    }

    static void EnsureVegetationParentWithUndo(plant_placement placement)
    {
        if (placement.vegetationParent != null)
        {
            return;
        }

        GameObject vegetationObject =
            new GameObject("Generated Vegetation");

        Undo.RegisterCreatedObjectUndo(
            vegetationObject,
            "Create Generated Vegetation"
        );

        placement.vegetationParent =
            vegetationObject.transform;

        EditorUtility.SetDirty(placement);
    }
}

[InitializeOnLoad]
public static class plant_placement_play_mode_restore
{
    const string RestoreAfterPlayModeKey =
        "plant_placement_restore_after_play_mode";

    static plant_placement_play_mode_restore()
    {
        EditorApplication.playModeStateChanged -=
            OnPlayModeStateChanged;

        EditorApplication.playModeStateChanged +=
            OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            SessionState.SetBool(
                RestoreAfterPlayModeKey,
                HasGeneratedPlantsInPlayMode()
            );
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (!SessionState.GetBool(RestoreAfterPlayModeKey, false))
            {
                return;
            }

            SessionState.SetBool(RestoreAfterPlayModeKey, false);
            RestorePlantsFromFinalStatus();
        }
    }

    static bool HasGeneratedPlantsInPlayMode()
    {
        plant_placement[] placements =
            Object.FindObjectsByType<plant_placement>(
                FindObjectsInactive.Include
            );

        foreach (plant_placement placement in placements)
        {
            if (placement != null &&
                placement.vegetationParent != null &&
                placement.vegetationParent.childCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    static void RestorePlantsFromFinalStatus()
    {
        int restoredPlacementCount = 0;

        plant_placement[] placements =
            Object.FindObjectsByType<plant_placement>(
                FindObjectsInactive.Include
            );

        foreach (plant_placement placement in placements)
        {
            if (placement == null)
            {
                continue;
            }

            if (!placement.HasFinalStatusJson())
            {
                continue;
            }

            placement.EnsureVegetationParent();
            ClearGeneratedPlants(placement.vegetationParent);

            if (placement.PlacePlantsFromFinalStatusJson())
            {
                restoredPlacementCount++;
                EditorUtility.SetDirty(placement);

                if (placement.vegetationParent != null)
                {
                    EditorUtility.SetDirty(
                        placement.vegetationParent.gameObject
                    );
                }

                EditorSceneManager.MarkSceneDirty(
                    placement.gameObject.scene
                );
            }
        }

        if (restoredPlacementCount > 0)
        {
            Debug.Log(
                $"Restored generated plants after Play Mode for " +
                $"{restoredPlacementCount} placement component(s)."
            );
        }
    }

    static void ClearGeneratedPlants(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        while (parent.childCount > 0)
        {
            Object.DestroyImmediate(parent.GetChild(0).gameObject);
        }
    }
}
