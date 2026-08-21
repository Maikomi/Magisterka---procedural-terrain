using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class plant_placement : MonoBehaviour
{
    public List<SpeciesPrefab> speciesPrefabs;

    [Header("References")]
    public Terrain terrain;
    public plant_analysis plantAnalysis;
    public plant_competition plantCompetition;

    [Header("Placement")]
    public float heightOffset = 0f;
    public Transform vegetationParent;

    [Header("Model Scaling")]
    public bool scaleModelsByGrowth = true;
    [Range(0.01f, 1f)] public float minimumModelScaleRatio = 0.2f;

    [Header("Random Research Placement")]
    [Min(1)] public int randomPlantCount = 660;

    Dictionary<string, GameObject> prefabDictionary;
    static readonly System.Random PlacementRandom = new System.Random();

    void Awake()
    {
        BuildPrefabDictionary();

        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        if (plantAnalysis == null)
        {
            plantAnalysis = FindAnyObjectByType<plant_analysis>();
        }

        if (plantCompetition == null)
        {
            plantCompetition = FindAnyObjectByType<plant_competition>();
        }
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
        if (prefabDictionary == null || prefabDictionary.Count == 0)
        {
            BuildPrefabDictionary();
        }

        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        if (plantCompetition == null)
        {
            plantCompetition = FindAnyObjectByType<plant_competition>();
        }

        if (plantAnalysis == null)
        {
            plantAnalysis = FindAnyObjectByType<plant_analysis>();
        }

        if (terrain == null)
        {
            Debug.LogError("Terrain is not assigned.");
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

        if (plantAnalysis == null)
        {
            Debug.LogError("Plant Analysis is not assigned.");
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

        GameObject vegetationObject =
            new GameObject("Generated Vegetation");

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

            if (PlacePlant(
                plant.seed.species.plantName,
                plant.seed.pixel,
                GetGrowthScaleRatio(
                    plant.radius,
                    plant.seed.species.maxRadius
                )
            ))
            {
                placedCount++;
            }
        }

        Debug.Log(
            $"Placed {placedCount} living plants from competition results. " +
            $"Skipped {deadCount} dead plants."
        );
    }

    public bool PlacePlantsFromFinalStatusJson()
    {
        if (!HasFinalStatusJson())
        {
            return false;
        }

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
            return false;
        }

        if (prefabDictionary == null || prefabDictionary.Count == 0)
        {
            Debug.LogError("No species prefabs are assigned.");
            return false;
        }

        EnsureVegetationParent();

        string finalStatusPath = GetFinalStatusPath();
        string json = File.ReadAllText(finalStatusPath);

        PlantFinalStatusSaveFile saveFile =
            JsonUtility.FromJson<PlantFinalStatusSaveFile>(json);

        if (saveFile == null ||
            saveFile.plants == null ||
            saveFile.plants.Count == 0)
        {
            Debug.LogWarning(
                $"Final plant status file is empty or invalid: {finalStatusPath}"
            );

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

            if (PlacePlant(
                plant.species,
                new Vector2Int(plant.pixelX, plant.pixelY),
                GetGrowthScaleRatio(plant)
            ))
            {
                placedCount++;
            }
        }

        Debug.Log(
            $"Placed {placedCount} living plants from final status JSON " +
            $"after {saveFile.simulationIterations} iterations. " +
            $"Skipped {deadCount} dead plants."
        );

        return true;
    }

    public bool HasFinalStatusJson()
    {
        return File.Exists(GetFinalStatusPath());
    }

    public void PlaceRandomPlants(List<Species> species)
    {
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

        if (prefabDictionary == null || prefabDictionary.Count == 0)
        {
            Debug.LogError("No species prefabs are assigned.");
            return;
        }

        List<string> placeableSpecies = GetPlaceableSpeciesNames(species);

        if (placeableSpecies.Count == 0)
        {
            Debug.LogError("No random placement species have assigned prefabs.");
            return;
        }

        EnsureVegetationParent();

        int placedCount = 0;

        for (int i = 0; i < randomPlantCount; i++)
        {
            string speciesName = placeableSpecies[
                PlacementRandom.Next(placeableSpecies.Count)
            ];

            if (PlacePlant(speciesName, GetRandomTerrainPixel()))
            {
                placedCount++;
            }
        }

        Debug.Log(
            $"Placed {placedCount} completely random plants " +
            $"without dominant species or competition iterations."
        );
    }

    List<string> GetPlaceableSpeciesNames(List<Species> species)
    {
        List<string> placeableSpecies = new List<string>();

        if (species == null)
        {
            return placeableSpecies;
        }

        foreach (Species plant in species)
        {
            if (plant == null ||
                string.IsNullOrWhiteSpace(plant.plantName) ||
                !prefabDictionary.ContainsKey(plant.plantName))
            {
                continue;
            }

            placeableSpecies.Add(plant.plantName);
        }

        return placeableSpecies;
    }

    Vector2Int GetRandomTerrainPixel()
    {
        int resolution = terrain.terrainData.heightmapResolution;

        return new Vector2Int(
            PlacementRandom.Next(resolution),
            PlacementRandom.Next(resolution)
        );
    }

    void PlaceSeeds()
    {
        List<Seed> seeds = plantAnalysis.lastGeneratedSeeds;

        if (seeds == null || seeds.Count == 0)
        {
            Debug.LogWarning("No seeds available. Generate Seed Map first.");
            return;
        }

        int placedCount = 0;

        foreach (Seed seed in seeds)
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
        if (seed == null || seed.species == null)
        {
            return false;
        }

        return PlacePlant(
            seed.species.plantName,
            seed.pixel,
            GetGrowthScaleRatio(seed.species.seedRadius, seed.species.maxRadius)
        );
    }

    bool PlacePlant(string speciesName, Vector2Int pixel)
    {
        return PlacePlant(speciesName, pixel, 1f);
    }

    bool PlacePlant(string speciesName, Vector2Int pixel, float modelScaleRatio)
    {
        if (string.IsNullOrWhiteSpace(speciesName))
        {
            return false;
        }

        if (!prefabDictionary.TryGetValue(speciesName, out GameObject plantPrefab) || plantPrefab == null)
        {
            Debug.LogWarning($"No prefab assigned for species '{speciesName}'.");
            return false;
        }

        Vector3 worldPosition = PixelToWorldPosition(pixel);

        worldPosition.y += heightOffset;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject plantObject =
                UnityEditor.PrefabUtility.InstantiatePrefab(
                    plantPrefab,
                    vegetationParent
                ) as GameObject;

            if (plantObject != null)
            {
                plantObject.transform.SetPositionAndRotation(
                    worldPosition,
                    Quaternion.identity
                );

                ApplyModelScale(plantObject, modelScaleRatio);

                UnityEditor.Undo.RegisterCreatedObjectUndo(
                    plantObject,
                    "Place Plant"
                );

                return true;
            }
        }
#endif

        GameObject runtimePlantObject =
            Instantiate(plantPrefab, worldPosition, Quaternion.identity, vegetationParent);

        ApplyModelScale(runtimePlantObject, modelScaleRatio);
        return true;
    }

    void ApplyModelScale(GameObject plantObject, float modelScaleRatio)
    {
        if (plantObject == null || !scaleModelsByGrowth)
        {
            return;
        }

        float safeScaleRatio = Mathf.Max(0.01f, modelScaleRatio);

        plantObject.transform.localScale *= safeScaleRatio;
    }

    float GetGrowthScaleRatio(PlantFinalStatusSaveData plant)
    {
        if (plant == null)
        {
            return 1f;
        }

        if (plant.growthRatio > 0f)
        {
            return ClampModelScaleRatio(plant.growthRatio);
        }

        float maxRadius = plant.maxRadius > 0f
            ? plant.maxRadius
            : GetSpeciesMaxRadius(plant.species);

        return GetGrowthScaleRatio(plant.radius, maxRadius);
    }

    float GetGrowthScaleRatio(float radius, float maxRadius)
    {
        if (maxRadius <= 0f)
        {
            return 1f;
        }

        return ClampModelScaleRatio(radius / maxRadius);
    }

    float ClampModelScaleRatio(float ratio)
    {
        return Mathf.Clamp(
            ratio,
            minimumModelScaleRatio,
            1f
        );
    }

    float GetSpeciesMaxRadius(string speciesName)
    {
        if (string.IsNullOrWhiteSpace(speciesName))
        {
            return 0f;
        }

        map_manager manager = GetComponent<map_manager>();

        if (manager == null)
        {
            manager = FindAnyObjectByType<map_manager>();
        }

        if (manager == null || manager.species == null)
        {
            return 0f;
        }

        Species species = manager.species.Find(
            plant => plant != null && plant.plantName == speciesName
        );

        return species != null ? species.maxRadius : 0f;
    }

    string GetFinalStatusPath()
    {
        string fileName =
            plantCompetition != null &&
            !string.IsNullOrWhiteSpace(plantCompetition.plantFinalStatusFileName)
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
