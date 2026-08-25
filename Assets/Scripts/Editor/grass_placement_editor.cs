using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(grass_placement))]
public class grass_placement_editor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        grass_placement placement =
            (grass_placement)target;

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField(
            "Grass Generation",
            EditorStyles.boldLabel
        );


        // =====================================================
        // TERRAIN TEXTURES
        // =====================================================

        EditorGUILayout.LabelField(
            "Terrain",
            EditorStyles.miniBoldLabel
        );

        if (GUILayout.Button(
            "Apply Grass + Rock Textures",
            GUILayout.Height(35)))
        {
            placement.ApplyTerrainTextures();

            EditorUtility.SetDirty(
                placement
            );
        }


        EditorGUILayout.Space(10);


        // =====================================================
        // GRASS MODELS
        // =====================================================

        EditorGUILayout.LabelField(
            "Models",
            EditorStyles.miniBoldLabel
        );

        if (GUILayout.Button(
            "Place Grass Models",
            GUILayout.Height(35)))
        {
            placement.GenerateGrass();

            EditorUtility.SetDirty(
                placement
            );
        }


        EditorGUILayout.Space(5);


        // =====================================================
        // BOTH
        // =====================================================

        if (GUILayout.Button(
            "Generate Grass + Texture",
            GUILayout.Height(40)))
        {
            placement.GenerateGrassAndTexture();

            EditorUtility.SetDirty(
                placement
            );
        }


        EditorGUILayout.Space(5);


        // =====================================================
        // CLEAR
        // =====================================================

        if (GUILayout.Button(
            "Clear Grass Models",
            GUILayout.Height(30)))
        {
            placement.ClearGeneratedGrass();

            EditorUtility.SetDirty(
                placement
            );
        }


        EditorGUILayout.Space(15);


        // =====================================================
        // INFO
        // =====================================================

        EditorGUILayout.HelpBox(
            "Grass Map determines where grass is suitable. " +
            "Suitable areas receive the Grass Terrain Layer, " +
            "while all other areas receive the Rock Terrain Layer. " +
            "Grass models are placed only inside suitable areas.",
            MessageType.Info
        );
    }
}