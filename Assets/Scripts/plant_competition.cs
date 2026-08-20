using UnityEngine;
using System.Collections.Generic;

public class plant_competition : MonoBehaviour
{
    [Header("References")]
    public Terrain terrain;
    public plant_analysis plantAnalysis;

    [Header("Simulation")]
    public int simulationIterations = 20;

    [Tooltip("Initial energy of every plant.")]
    public float initialEnergy = 1f;

    [Tooltip("Energy lost from the plant based on competition.")]
    public float competitionEnergyLoss = 0.1f;

    [Tooltip("Plants with energy below this value die.")]
    [Range(0f, 1f)]
    public float deathEnergyThreshold = 0.05f;

    [Header("Debug")]
    public bool logSimulation = true;

    public readonly List<Plant> finalPlants = new List<Plant>();


    public void RunCompetition()
    {
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        if (plantAnalysis == null)
        {
            plantAnalysis = FindAnyObjectByType<plant_analysis>();
        }

        if (plantAnalysis == null)
        {
            Debug.LogError("Plant Analysis is not assigned.");
            return;
        }

        List<Seed> seeds = plantAnalysis.lastGeneratedSeeds;

        if (seeds == null || seeds.Count == 0)
        {
            Debug.LogWarning("No seeds available for plant competition.");
            return;
        }

        CreatePlantsFromSeeds(seeds);

        if (logSimulation)
        {
            Debug.Log(
                $"Starting plant competition with {finalPlants.Count} plants."
            );
        }

        for (int iteration = 0; iteration < simulationIterations; iteration++)
        {
            SimulateIteration(iteration);
        }

        if (logSimulation)
        {
            int aliveCount = CountAlivePlants();

            Debug.Log(
                $"Plant competition finished. " +
                $"Alive plants: {aliveCount}/{finalPlants.Count}"
            );
        }
    }


    void CreatePlantsFromSeeds(List<Seed> seeds)
    {
        finalPlants.Clear();

        for (int i = 0; i < seeds.Count; i++)
        {
            Seed seed = seeds[i];

            if (seed == null || seed.species == null)
            {
                continue;
            }

            Plant plant = new Plant(
                seed,
                seed.species.seedRadius,
                0,
                true,
                true,
                0f,
                initialEnergy,
                seed.species.competetivness,
                i
            );

            finalPlants.Add(plant);
        }
    }


    void SimulateIteration(int iteration)
    {
        ResetCompetitionValues();

        GrowPlants();

        CalculateCompetition();

        UpdateEnergy();

        CheckDeaths();

        IncreaseAge();

        if (logSimulation)
        {
            Debug.Log(
                $"Competition iteration {iteration + 1}/{simulationIterations}: " +
                $"{CountAlivePlants()} plants alive."
            );
        }
    }


    void ResetCompetitionValues()
    {
        foreach (Plant plant in finalPlants)
        {
            if (plant == null || !plant.isAlive)
            {
                continue;
            }

            plant.competition = 0f;
        }
    }


    void GrowPlants()
    {
        foreach (Plant plant in finalPlants)
        {
            if (plant == null || !plant.isAlive || !plant.growing)
            {
                continue;
            }

            Species species = plant.seed.species;

            if (species == null)
            {
                continue;
            }

            float growth = species.growthRate;

            // Competition will reduce growth in later iterations.
            growth *= Mathf.Clamp01(1f - plant.competition);

            plant.radius += growth;

            plant.radius = Mathf.Min(
                plant.radius,
                species.maxRadius
            );

            if (plant.radius >= species.maxRadius)
            {
                plant.growing = false;
            }
        }
    }


    void CalculateCompetition()
    {
        for (int i = 0; i < finalPlants.Count; i++)
        {
            Plant plantA = finalPlants[i];

            if (plantA == null || !plantA.isAlive)
            {
                continue;
            }

            Vector3 positionA = GetPlantWorldPosition(plantA);

            for (int j = i + 1; j < finalPlants.Count; j++)
            {
                Plant plantB = finalPlants[j];

                if (plantB == null || !plantB.isAlive)
                {
                    continue;
                }

                Vector3 positionB = GetPlantWorldPosition(plantB);

                float distance = Vector3.Distance(
                    positionA,
                    positionB
                );

                float combinedRadius =
                    plantA.radius + plantB.radius;

                if (distance >= combinedRadius)
                {
                    continue;
                }

                float overlap = combinedRadius - distance;

                float competitionA = CalculateCompetitionStrength(
                    plantA,
                    plantB,
                    overlap,
                    combinedRadius
                );

                float competitionB = CalculateCompetitionStrength(
                    plantB,
                    plantA,
                    overlap,
                    combinedRadius
                );

                plantA.competition += competitionA;
                plantB.competition += competitionB;
            }
        }

        NormalizeCompetitionValues();
    }


    float CalculateCompetitionStrength(
        Plant plant,
        Plant opponent,
        float overlap,
        float combinedRadius)
    {
        if (plant.seed == null ||
            plant.seed.species == null ||
            opponent.seed == null ||
            opponent.seed.species == null)
        {
            return 0f;
        }

        float overlapFactor =
            Mathf.Clamp01(overlap / combinedRadius);

        float ownCompetitiveness =
            Mathf.Max(0f, plant.seed.species.competetivness);

        float opponentCompetitiveness =
            Mathf.Max(0f, opponent.seed.species.competetivness);

        float totalCompetitiveness =
            ownCompetitiveness + opponentCompetitiveness;

        if (totalCompetitiveness <= 0f)
        {
            return 0f;
        }

        float opponentStrength =
            opponentCompetitiveness / totalCompetitiveness;

        return overlapFactor * opponentStrength;
    }


    void NormalizeCompetitionValues()
    {
        foreach (Plant plant in finalPlants)
        {
            if (plant == null || !plant.isAlive)
            {
                continue;
            }

            plant.competition = Mathf.Clamp01(
                plant.competition
            );
        }
    }


    void UpdateEnergy()
    {
        foreach (Plant plant in finalPlants)
        {
            if (plant == null || !plant.isAlive)
            {
                continue;
            }

            float energyLoss =
                plant.competition * competitionEnergyLoss;

            plant.energy -= energyLoss;

            plant.energy = Mathf.Clamp01(
                plant.energy
            );
        }
    }


    void CheckDeaths()
    {
        foreach (Plant plant in finalPlants)
        {
            if (plant == null || !plant.isAlive)
            {
                continue;
            }

            if (plant.energy <= deathEnergyThreshold)
            {
                plant.isAlive = false;
                plant.growing = false;

                if (logSimulation)
                {
                    Debug.Log(
                        $"Plant died: " +
                        $"{plant.seed.species.plantName} " +
                        $"(index {plant.index})"
                    );
                }
            }
        }
    }


    void IncreaseAge()
    {
        foreach (Plant plant in finalPlants)
        {
            if (plant == null || !plant.isAlive)
            {
                continue;
            }

            plant.age++;
        }
    }


    int CountAlivePlants()
    {
        int count = 0;

        foreach (Plant plant in finalPlants)
        {
            if (plant != null && plant.isAlive)
            {
                count++;
            }
        }

        return count;
    }


    Vector3 GetPlantWorldPosition(Plant plant)
    {
        Vector2Int pixel = plant.seed.pixel;

        TerrainData terrainData = terrain.terrainData;

        int resolution = terrainData.heightmapResolution;

        float normalizedX =
            (float)pixel.x / (resolution - 1);

        float normalizedZ =
            (float)pixel.y / (resolution - 1);

        Vector3 terrainPosition =
            terrain.transform.position;

        Vector3 terrainSize =
            terrainData.size;

        float worldX =
            terrainPosition.x +
            normalizedX * terrainSize.x;

        float worldZ =
            terrainPosition.z +
            normalizedZ * terrainSize.z;

        float worldY =
            terrain.SampleHeight(
                new Vector3(
                    worldX,
                    terrainPosition.y,
                    worldZ
                )
            ) + terrainPosition.y;

        return new Vector3(
            worldX,
            worldY,
            worldZ
        );
    }
}