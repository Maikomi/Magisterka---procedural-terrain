using UnityEngine;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class grass_placement : MonoBehaviour
{
    [Header("References")]

    public Terrain terrain;

    [Tooltip("Name of the Grass Map file located in Assets/maps.")]
    public string grassMapFileName = "GrassMap.exr";


    // =========================================================
    // TERRAIN TEXTURES
    // =========================================================

    [Header("Terrain Textures")]

    [Tooltip("Texture used for the grass area.")]
    public Texture2D grassTexture;

    [Tooltip("Existing Terrain Layer containing the grass texture.")]
    public TerrainLayer grassTerrainLayer;

    [Tooltip("Texture used where grass is not suitable.")]
    public Texture2D rockTexture;

    [Tooltip("Existing Terrain Layer containing the rock texture.")]
    public TerrainLayer rockTerrainLayer;

    [Range(0f, 1f)]
    [Tooltip("Grass Map values equal to or above this value are treated as grass.")]
    public float grassMapThreshold = 0.5f;


    // =========================================================
    // GRASS MODELS
    // =========================================================

    [Header("Grass Models")]

    [Tooltip("Pool of grass prefabs. A random prefab is selected for each placed instance.")]
    public List<GameObject> grassPrefabs =
        new List<GameObject>();

    [Range(0f, 1f)]
    [Tooltip("Percentage of suitable grass locations receiving a model.")]
    public float grassDensity = 0.6f;

    [Min(0.05f)]
    [Tooltip("Minimum distance between grass sampling points in meters.")]
    public float grassSpacing = 1f;

    [Min(1)]
    [Tooltip("Maximum number of grass GameObjects that can be generated.")]
    public int maxGrassInstances = 100000;


    // =========================================================
    // RANDOMIZATION
    // =========================================================

    [Header("Randomization")]

    public bool randomRotation = true;

    [Min(0.01f)]
    public float minimumScale = 0.8f;

    [Min(0.01f)]
    public float maximumScale = 1.2f;

    public bool useFixedRandomSeed = true;

    public int randomSeed = 12345;


    // =========================================================
    // PLACEMENT
    // =========================================================

    [Header("Placement")]

    [Tooltip("Additional vertical offset above the terrain.")]
    public float heightOffset = 0f;

    [Tooltip("Parent object for generated grass models.")]
    public Transform grassParent;


    // =========================================================
    // STATISTICS
    // =========================================================

    [Header("Statistics")]

    [SerializeField]
    private int lastPlacedGrassCount;

    [SerializeField]
    private int lastEligibleSampleCount;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }
    }


    private void OnValidate()
    {
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        grassDensity =
            Mathf.Clamp01(grassDensity);

        grassMapThreshold =
            Mathf.Clamp01(grassMapThreshold);

        grassSpacing =
            Mathf.Max(0.05f, grassSpacing);

        maxGrassInstances =
            Mathf.Max(1, maxGrassInstances);

        minimumScale =
            Mathf.Max(0.01f, minimumScale);

        maximumScale =
            Mathf.Max(
                minimumScale,
                maximumScale
            );
    }


    // =========================================================
    // PUBLIC API
    // =========================================================

    /// <summary>
    /// Applies Grass/Rock textures to the Terrain
    /// and generates grass models.
    /// </summary>
    public void GenerateGrassAndTexture()
    {
        ApplyTerrainTextures();
        GenerateGrass();
    }


    /// <summary>
    /// Applies Grass/Rock Terrain Layers according to Grass Map.
    /// </summary>
    public void ApplyTerrainTextures()
    {
        if (!ValidateTerrain())
        {
            return;
        }

        if (grassTerrainLayer == null)
        {
            Debug.LogError(
                "Grass Terrain Layer is not assigned."
            );

            return;
        }

        if (rockTerrainLayer == null)
        {
            Debug.LogError(
                "Rock Terrain Layer is not assigned."
            );

            return;
        }

        Texture2D grassMap =
            LoadGrassMap();

        if (grassMap == null)
        {
            return;
        }

        EnsureTerrainLayers();

        ApplyGrassRockAlphamap(
            grassMap
        );

        Debug.Log(
            "Grass/Rock Terrain textures applied."
        );
    }


    /// <summary>
    /// Generates grass models using the Grass Map.
    /// </summary>
    public void GenerateGrass()
    {
        if (!ValidateReferences())
        {
            return;
        }

        Texture2D grassMap =
            LoadGrassMap();

        if (grassMap == null)
        {
            return;
        }

        EnsureGrassParent();

        ClearGeneratedGrass();

        PlaceGrassModels(
            grassMap
        );

        Debug.Log(
            $"Grass generation finished. " +
            $"Placed: {lastPlacedGrassCount} | " +
            $"Eligible samples: {lastEligibleSampleCount}"
        );
    }


    /// <summary>
    /// Removes all generated grass models.
    /// </summary>
    public void ClearGeneratedGrass()
    {
        if (grassParent == null)
        {
            Debug.LogWarning(
                "Grass Parent is not assigned."
            );

            return;
        }

#if UNITY_EDITOR

        if (!Application.isPlaying)
        {
            Undo.RegisterFullObjectHierarchyUndo(
                grassParent.gameObject,
                "Clear Generated Grass"
            );
        }

#endif

        while (grassParent.childCount > 0)
        {
            Transform child =
                grassParent.GetChild(0);

#if UNITY_EDITOR

            if (!Application.isPlaying)
            {
                DestroyImmediate(
                    child.gameObject
                );
            }
            else
            {
                Destroy(
                    child.gameObject
                );
            }

#else

            Destroy(
                child.gameObject
            );

#endif
        }

        lastPlacedGrassCount = 0;

        Debug.Log(
            "Generated grass cleared."
        );
    }


    // =========================================================
    // VALIDATION
    // =========================================================

    private bool ValidateReferences()
    {
        if (!ValidateTerrain())
        {
            return false;
        }

        if (grassPrefabs == null ||
            grassPrefabs.Count == 0)
        {
            Debug.LogError(
                "No grass prefabs are assigned."
            );

            return false;
        }

        bool hasValidPrefab = false;

        foreach (GameObject prefab
                 in grassPrefabs)
        {
            if (prefab != null)
            {
                hasValidPrefab = true;
                break;
            }
        }

        if (!hasValidPrefab)
        {
            Debug.LogError(
                "Grass prefab pool contains no valid prefabs."
            );

            return false;
        }

        if (grassDensity <= 0f)
        {
            Debug.LogWarning(
                "Grass Density is 0. No grass models will be generated."
            );

            return false;
        }

        return true;
    }


    private bool ValidateTerrain()
    {
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        if (terrain == null)
        {
            Debug.LogError(
                "Terrain is not assigned and no active Terrain was found."
            );

            return false;
        }

        if (terrain.terrainData == null)
        {
            Debug.LogError(
                "TerrainData is missing."
            );

            return false;
        }

        return true;
    }


    // =========================================================
    // GRASS MAP
    // =========================================================

    private Texture2D LoadGrassMap()
    {
        if (string.IsNullOrWhiteSpace(
            grassMapFileName))
        {
            Debug.LogError(
                "Grass Map file name is empty."
            );

            return null;
        }

        string fileName =
            grassMapFileName;

        if (!fileName.EndsWith(".exr"))
        {
            fileName += ".exr";
        }

        string path =
            Path.Combine(
                map_helper.GetMapsPath(),
                fileName
            );

        Texture2D grassMap =
            map_helper.LoadMap(path);

        if (grassMap == null)
        {
            Debug.LogError(
                $"Could not load Grass Map from: {path}"
            );
        }

        return grassMap;
    }


    private bool IsGrass(
        Texture2D grassMap,
        float normalizedX,
        float normalizedZ)
    {
        if (grassMap == null)
        {
            return false;
        }

        float value =
            grassMap.GetPixelBilinear(
                normalizedX,
                normalizedZ
            ).r;

        return value >= grassMapThreshold;
    }


    // =========================================================
    // TERRAIN LAYERS
    // =========================================================

#if UNITY_EDITOR

    private void EnsureTerrainLayers()
    {
        TerrainData terrainData =
            terrain.terrainData;

        TerrainLayer[] existingLayers =
            terrainData.terrainLayers;

        List<TerrainLayer> layers =
            new List<TerrainLayer>();

        if (existingLayers != null)
        {
            layers.AddRange(
                existingLayers
            );
        }

        bool grassExists =
            layers.Contains(
                grassTerrainLayer
            );

        bool rockExists =
            layers.Contains(
                rockTerrainLayer
            );

        if (!grassExists)
        {
            layers.Add(
                grassTerrainLayer
            );
        }

        if (!rockExists)
        {
            layers.Add(
                rockTerrainLayer
            );
        }

        terrainData.terrainLayers =
            layers.ToArray();

        if (grassTexture != null)
        {
            grassTerrainLayer.diffuseTexture =
                grassTexture;
        }

        if (rockTexture != null)
        {
            rockTerrainLayer.diffuseTexture =
                rockTexture;
        }

        EditorUtility.SetDirty(
            terrainData
        );

        AssetDatabase.SaveAssets();
    }


    private void ApplyGrassRockAlphamap(
        Texture2D grassMap)
    {
        TerrainData terrainData =
            terrain.terrainData;

        TerrainLayer[] layers =
            terrainData.terrainLayers;

        if (layers == null ||
            layers.Length == 0)
        {
            Debug.LogError(
                "Terrain has no Terrain Layers."
            );

            return;
        }

        int grassLayerIndex =
            System.Array.IndexOf(
                layers,
                grassTerrainLayer
            );

        int rockLayerIndex =
            System.Array.IndexOf(
                layers,
                rockTerrainLayer
            );

        if (grassLayerIndex < 0)
        {
            Debug.LogError(
                "Grass Terrain Layer is not assigned to the Terrain."
            );

            return;
        }

        if (rockLayerIndex < 0)
        {
            Debug.LogError(
                "Rock Terrain Layer is not assigned to the Terrain."
            );

            return;
        }

        int alphaWidth =
            terrainData.alphamapWidth;

        int alphaHeight =
            terrainData.alphamapHeight;

        int layerCount =
            terrainData.alphamapLayers;

        float[,,] alphaMaps =
            terrainData.GetAlphamaps(
                0,
                0,
                alphaWidth,
                alphaHeight
            );

        for (int y = 0;
             y < alphaHeight;
             y++)
        {
            float normalizedY =
                alphaHeight > 1
                    ? (float)y /
                      (alphaHeight - 1)
                    : 0f;

            for (int x = 0;
                 x < alphaWidth;
                 x++)
            {
                float normalizedX =
                    alphaWidth > 1
                        ? (float)x /
                          (alphaWidth - 1)
                        : 0f;

                bool hasGrass =
                    IsGrass(
                        grassMap,
                        normalizedX,
                        normalizedY
                    );

                // Reset all layers at this point.
                for (int layer = 0;
                     layer < layerCount;
                     layer++)
                {
                    alphaMaps[
                        y,
                        x,
                        layer
                    ] = 0f;
                }

                if (hasGrass)
                {
                    alphaMaps[
                        y,
                        x,
                        grassLayerIndex
                    ] = 1f;
                }
                else
                {
                    alphaMaps[
                        y,
                        x,
                        rockLayerIndex
                    ] = 1f;
                }
            }
        }

        terrainData.SetAlphamaps(
            0,
            0,
            alphaMaps
        );

        EditorUtility.SetDirty(
            terrainData
        );
    }

#endif


    // =========================================================
    // GRASS PARENT
    // =========================================================

    private void EnsureGrassParent()
    {
        if (grassParent != null)
        {
            return;
        }

        GameObject grassObject =
            new GameObject(
                "Generated Grass"
            );

        grassParent =
            grassObject.transform;

#if UNITY_EDITOR

        if (!Application.isPlaying)
        {
            Undo.RegisterCreatedObjectUndo(
                grassObject,
                "Create Generated Grass"
            );
        }

#endif
    }


    // =========================================================
    // GRASS MODEL PLACEMENT
    // =========================================================

    private void PlaceGrassModels(
        Texture2D grassMap)
    {
        TerrainData terrainData =
            terrain.terrainData;

        Vector3 terrainSize =
            terrainData.size;

        System.Random random =
            useFixedRandomSeed
                ? new System.Random(randomSeed)
                : new System.Random();

        int samplesX =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    terrainSize.x /
                    grassSpacing
                )
            );

        int samplesZ =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    terrainSize.z /
                    grassSpacing
                )
            );

        lastPlacedGrassCount = 0;
        lastEligibleSampleCount = 0;

        for (int z = 0;
             z < samplesZ;
             z++)
        {
            for (int x = 0;
                 x < samplesX;
                 x++)
            {
                if (lastPlacedGrassCount >=
                    maxGrassInstances)
                {
                    Debug.LogWarning(
                        $"Grass generation stopped at " +
                        $"maxGrassInstances = " +
                        $"{maxGrassInstances}."
                    );

                    return;
                }

                float normalizedX =
                    (x + 0.5f) /
                    samplesX;

                float normalizedZ =
                    (z + 0.5f) /
                    samplesZ;

                if (!IsGrass(
                    grassMap,
                    normalizedX,
                    normalizedZ))
                {
                    continue;
                }

                lastEligibleSampleCount++;

                // Only a percentage of suitable
                // positions receives a model.
                if (random.NextDouble() >
                    grassDensity)
                {
                    continue;
                }

                // Random position inside the sample cell.
                float randomX =
                    (float)random.NextDouble();

                float randomZ =
                    (float)random.NextDouble();

                float finalX =
                    (x + randomX) /
                    samplesX;

                float finalZ =
                    (z + randomZ) /
                    samplesZ;

                // Check Grass Map again after
                // randomizing the position.
                if (!IsGrass(
                    grassMap,
                    finalX,
                    finalZ))
                {
                    continue;
                }

                GameObject prefab =
                    GetRandomGrassPrefab(
                        random
                    );

                if (prefab == null)
                {
                    continue;
                }

                Vector3 worldPosition =
                    NormalizedToWorldPosition(
                        finalX,
                        finalZ
                    );

                worldPosition.y +=
                    heightOffset;

                GameObject grassObject;

#if UNITY_EDITOR

                if (!Application.isPlaying)
                {
                    grassObject =
                        PrefabUtility.InstantiatePrefab(
                            prefab,
                            grassParent
                        ) as GameObject;
                }
                else
                {
                    grassObject =
                        Instantiate(
                            prefab,
                            worldPosition,
                            Quaternion.identity,
                            grassParent
                        );
                }

#else

                grassObject =
                    Instantiate(
                        prefab,
                        worldPosition,
                        Quaternion.identity,
                        grassParent
                    );

#endif

                if (grassObject == null)
                {
                    continue;
                }

                grassObject.transform.position =
                    worldPosition;

                ApplyRandomTransform(
                    grassObject,
                    random
                );

#if UNITY_EDITOR

                if (!Application.isPlaying)
                {
                    Undo.RegisterCreatedObjectUndo(
                        grassObject,
                        "Place Grass"
                    );
                }

#endif

                lastPlacedGrassCount++;
            }
        }
    }


    private GameObject GetRandomGrassPrefab(
        System.Random random)
    {
        if (grassPrefabs == null ||
            grassPrefabs.Count == 0)
        {
            return null;
        }

        List<GameObject> validPrefabs =
            new List<GameObject>();

        foreach (GameObject prefab
                 in grassPrefabs)
        {
            if (prefab != null)
            {
                validPrefabs.Add(
                    prefab
                );
            }
        }

        if (validPrefabs.Count == 0)
        {
            return null;
        }

        int index =
            random.Next(
                validPrefabs.Count
            );

        return validPrefabs[index];
    }


    private void ApplyRandomTransform(
        GameObject grassObject,
        System.Random random)
    {
        float scale =
            Mathf.Lerp(
                minimumScale,
                maximumScale,
                (float)random.NextDouble()
            );

        grassObject.transform.localScale *=
            scale;

        if (randomRotation)
        {
            float rotation =
                (float)random.NextDouble() *
                360f;

            grassObject.transform.rotation =
                Quaternion.Euler(
                    0f,
                    rotation,
                    0f
                );
        }
    }


    private Vector3 NormalizedToWorldPosition(
        float normalizedX,
        float normalizedZ)
    {
        TerrainData terrainData =
            terrain.terrainData;

        Vector3 terrainPosition =
            terrain.transform.position;

        Vector3 terrainSize =
            terrainData.size;

        float worldX =
            terrainPosition.x +
            normalizedX *
            terrainSize.x;

        float worldZ =
            terrainPosition.z +
            normalizedZ *
            terrainSize.z;

        Vector3 samplePosition =
            new Vector3(
                worldX,
                terrainPosition.y,
                worldZ
            );

        float worldY =
            terrain.SampleHeight(
                samplePosition
            ) +
            terrainPosition.y;

        return new Vector3(
            worldX,
            worldY,
            worldZ
        );
    }
}