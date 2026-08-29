using UnityEngine;
using System.Collections.Generic;

public class plant_placement : MonoBehaviour
{
    public List<SpeciesPrefab> speciesPrefabs;

    public plant_competition plantCompetition;


    [Header("References")]
    public Terrain terrain;
    public plant_analysis plantAnalysis;

    [Header("Placement")]
    public float heightOffset = 0f;
    public Transform vegetationParent;

    Dictionary<string, GameObject> prefabDictionary;

    void Awake()
    {
        BuildPrefabDictionary();
    }

    void OnValidate()
    {
        BuildPrefabDictionary();
    }

    void BuildPrefabDictionary()
    {
        prefabDictionary = new Dictionary<string, GameObject>();

        if (speciesPrefabs == null)
        {
            return;
        }

        foreach (var item in speciesPrefabs)
        {
            if (item != null && !string.IsNullOrWhiteSpace(item.speciesName) && item.prefab != null)
            {
                prefabDictionary[item.speciesName] = item.prefab;
            }
        }
    }

    public void PlacePlants()
    {
        Debug.Log("plant_placement.PlacePlants() started.");

        if (terrain == null)
        {
            Debug.LogError("Terrain is not assigned.");
            return;
        }

        if (plantAnalysis == null)
        {
            Debug.LogError("Plant Analysis is not assigned.");
            return;
        }

        if (prefabDictionary == null || prefabDictionary.Count == 0)
        {
            Debug.LogError("No species prefabs are assigned.");
            return;
        }

        List<Seed> seeds = plantAnalysis.lastGeneratedSeeds;

        if (seeds == null || seeds.Count == 0)
        {
            Debug.LogWarning("No seeds available. Generate Seed Map first.");
            return;
        }

        int placedCount = 0;

        foreach (Seed seed in seeds)
        {
            if (seed == null || seed.species == null)
            {
                continue;
            }

            if (!prefabDictionary.TryGetValue(seed.species.plantName, out GameObject plantPrefab) || plantPrefab == null)
            {
                Debug.LogWarning($"No prefab assigned for species '{seed.species.plantName}'.");
                continue;
            }

            Vector3 worldPosition = PixelToWorldPosition(seed.pixel);

            worldPosition.y += heightOffset;

            Instantiate(plantPrefab, worldPosition, Quaternion.identity, vegetationParent);
            placedCount++;
        }

        Debug.Log($"Placed {placedCount} plants.");
    }

    Vector3 PixelToWorldPosition(Vector2Int pixel)
    {
        TerrainData terrainData = terrain.terrainData;

        int resolution = terrainData.heightmapResolution;

        float normalizedX = (float)pixel.x / (resolution - 1);
        float normalizedZ = (float)pixel.y / (resolution - 1);

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;

        float worldX = terrainPosition.x + normalizedX * terrainSize.x;
        float worldZ = terrainPosition.z + normalizedZ * terrainSize.z;

        float worldY = terrain.SampleHeight(new Vector3(worldX, terrainPosition.y, worldZ)) + terrainPosition.y;

        return new Vector3(worldX, worldY, worldZ);
    }
}