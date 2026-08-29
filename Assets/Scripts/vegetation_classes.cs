using System;
using UnityEngine;

[Serializable]
public class Species
{
    public string plantName;
    public bool generateSuitabilityMap = true;
    public Color color = Color.white;
    public float seedRadius = 1f;
    public int maxSeedCountPerSpecies = 0;
    [Tooltip("Growth multiplier. 1 reaches maxRadius after all simulation iterations if growth is not slowed.")]
    public float growthRate;
    public float maxRadius;

    public Vector2 heightPreference;
    public Vector2 slopePreference;
    public Vector2 exposurePreference;
    public Vector2 moisturePreference;

    public float competetivness;
    public float shadePreference;

    public Species(
        string plantName,
        Vector2 heightPreference,
        Vector2 slopePreference,
        Vector2 exposurePreference,
        Vector2 moisturePreference,
        float seedRadius,
        float growthRate,
        float maxRadius,
        float competetivness,
        float shadePreference,
        Color color = default,
        int maxSeedCountPerSpecies = 0
    )
    {
        this.plantName = plantName;
        this.heightPreference = heightPreference;
        this.slopePreference = slopePreference;
        this.exposurePreference = exposurePreference;
        this.moisturePreference = moisturePreference;
        this.color = color != default ? color : Color.white;
        this.seedRadius = seedRadius;
        this.maxSeedCountPerSpecies = maxSeedCountPerSpecies;
        this.growthRate = growthRate;
        this.maxRadius = maxRadius;
        this.competetivness = competetivness;
        this.shadePreference = shadePreference;
    }

    public float CalculateSuitability(
        float height,
        float slope,
        float exposure,
        float moisture,
        float heightWeight = 1f,
        float slopeWeight = 1f,
        float exposureWeight = 1f,
        float moistureWeight = 1f)
    {
        float heightSuitability =
            CalculateRangeSuitability(height, heightPreference);

        float slopeSuitability =
            CalculateRangeSuitability(slope, slopePreference);

        float exposureSuitability =
            CalculateRangeSuitability(exposure, exposurePreference);

        float moistureSuitability =
            CalculateRangeSuitability(moisture, moisturePreference);

        float totalWeight = heightWeight + slopeWeight + exposureWeight + moistureWeight;
        if (totalWeight <= 0f)
        {
            totalWeight = 4f;
        }

        float weightedSuitability =
            (heightSuitability * heightWeight
            + slopeSuitability * slopeWeight
            + exposureSuitability * exposureWeight
            + moistureSuitability * moistureWeight)
            / totalWeight;

        return Mathf.Clamp01(weightedSuitability);
    }
    float CalculateRangeSuitability(float value, Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);

        // Wewnątrz preferowanego zakresu.
        if (value >= min && value <= max)
        {
            return 1f;
        }

        // Poniżej zakresu.
        if (value < min)
        {
            if (min <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(value / min);
        }

        // Powyżej zakresu.
        if (max >= 1f)
        {
            return 0f;
        }

        return Mathf.Clamp01(
            (1f - value) / (1f - max)
        );
    }
}

[Serializable]
public class SpeciesPrefab
{
    public string speciesName;
    public GameObject prefab;
}

[Serializable]
public class Seed
{
    public Species species;
    public Vector2Int pixel;
    public float suitability;

    public Seed(Species species, Vector2Int pixel, float suitability)
    {
        this.species = species;
        this.pixel = pixel;
        this.suitability = suitability;
    }
}

[Serializable]
public class Plant
{
    public Seed seed;
    public float radius;
    public int age;
    public bool isAlive;
    public bool growing;
    public float shadow;
    public float energy;
    public float competition;
    public int index;

    public Plant(Seed seed, float radius, int age, bool isAlive, bool growing, float shadow, float energy, float competition, int index)
    {
        this.seed = seed;
        this.radius = radius;
        this.age = age;
        this.isAlive = isAlive;
        this.growing = growing;
        this.shadow = shadow;
        this.energy = energy;
        this.competition = competition;
        this.index = index;
    }
}
