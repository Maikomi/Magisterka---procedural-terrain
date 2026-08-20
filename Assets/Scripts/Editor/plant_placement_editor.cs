using UnityEngine;
using UnityEditor;

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
        if (placement.vegetationParent == null)
        {
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

        placement.PlacePlants();

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
}