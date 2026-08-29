using UnityEngine;
using System.Collections.Generic;
using System.IO;

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

        if (prefabDictionary == null || prefabDictionary.Count == 0)
        {
            BuildPrefabDictionary();
        }

        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

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

        EnsureVegetationParent();

        if (plantCompetition != null && plantCompetition.finalPlants.Count > 0)
        {
            PlaceCompetitionPlants();
            return;
        }

        if (PlacePlantsFromFinalStatusJson())
        {
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

        PlaceSeeds();
    }

    public void EnsureVegetationParent()
    {
        if (vegetationParent != null)
        {
            return;
        }

        GameObject vegetationObject = new GameObject("Generated Vegetation");
        vegetationParent = vegetationObject.transform;
    }

    void PlaceCompetitionPlants()
    {
        int placedCount = 0;
        int deadCount = 0;

        foreach (Plant plant in plantCompetition.finalPlants)
        {
            if (plant == null || plant.seed == null || plant.seed.species == null)
            {
                continue;
            }

            if (!plant.isAlive)
            {
                deadCount++;
                continue;
            }

            if (PlacePlant(plant.seed.species.plantName, plant.seed.pixel))
            {
                placedCount++;
            }
        }

        Debug.Log($"Placed {placedCount} living plants from competition results. Skipped {deadCount} dead plants.");
    }

    public bool PlacePlantsFromFinalStatusJson()
    {
        if (!HasFinalStatusJson())
        {
            return false;
        }

        string finalStatusPath = GetFinalStatusPath();
        PlantFinalStatusSaveFile saveFile = JsonUtility.FromJson<PlantFinalStatusSaveFile>(File.ReadAllText(finalStatusPath));

        if (saveFile == null || saveFile.plants == null || saveFile.plants.Count == 0)
        {
            Debug.LogWarning($"Final plant status file is empty or invalid: {finalStatusPath}");
            return false;
        }

        int placedCount = 0;
        int deadCount = 0;

        foreach (PlantFinalStatusSaveData plant in saveFile.plants)
        {
            if (plant == null)
            {
                continue;
            }

            if (!plant.isAlive)
            {
                deadCount++;
                continue;
            }

            if (PlacePlant(plant.species, new Vector2Int(plant.pixelX, plant.pixelY)))
            {
                placedCount++;
            }
        }

        Debug.Log($"Placed {placedCount} living plants from final status JSON. Skipped {deadCount} dead plants.");
        return true;
    }

    public bool HasFinalStatusJson()
    {
        return File.Exists(GetFinalStatusPath());
    }

    void PlaceSeeds()
    {
        int placedCount = 0;

        foreach (Seed seed in plantAnalysis.lastGeneratedSeeds)
        {
            if (PlacePlant(seed))
            {
                placedCount++;
            }
        }

        Debug.Log($"Placed {placedCount} plants from seed map.");
    }

    bool PlacePlant(Seed seed)
    {
        return seed != null && seed.species != null && PlacePlant(seed.species.plantName, seed.pixel);
    }

    bool PlacePlant(string speciesName, Vector2Int pixel)
    {
        if (string.IsNullOrWhiteSpace(speciesName) || !prefabDictionary.TryGetValue(speciesName, out GameObject plantPrefab) || plantPrefab == null)
        {
            Debug.LogWarning($"No prefab assigned for species '{speciesName}'.");
            return false;
        }

        Vector3 worldPosition = PixelToWorldPosition(pixel);
        worldPosition.y += heightOffset;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject plantObject = UnityEditor.PrefabUtility.InstantiatePrefab(plantPrefab, vegetationParent) as GameObject;
            if (plantObject != null)
            {
                plantObject.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
                UnityEditor.Undo.RegisterCreatedObjectUndo(plantObject, "Place Plant");
                return true;
            }
        }
#endif

        Instantiate(plantPrefab, worldPosition, Quaternion.identity, vegetationParent);
        return true;
    }

    string GetFinalStatusPath()
    {
        string fileName = plantCompetition != null && !string.IsNullOrWhiteSpace(plantCompetition.plantFinalStatusFileName)
            ? plantCompetition.plantFinalStatusFileName
            : "PlantFinalStatus.json";
        return Path.Combine(map_helper.GetMapsPath(), fileName);
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