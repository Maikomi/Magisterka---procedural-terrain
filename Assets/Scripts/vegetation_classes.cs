using System;
using UnityEngine;

[Serializable]
public class Species
{
    public string plantName;
    public bool generateSuitabilityMap = true;
    public Color color = Color.white;
    public float seedRadius = 1f;
    public float growthRate;
    public float maxRadius;

    [Range(0f, 1f)] public float heightPreference;
    [Range(0f, 1f)] public float slopePreference;
    [Range(0f, 1f)] public float exposurePreference;
    [Range(0f, 1f)] public float moisturePreference;

    public float competetivness;
    public float shadePreference;

    public Species(
        string plantName,
        float heightPreference,
        float slopePreference,
        float exposurePreference,
        float moisturePreference,
        float seedRadius,
        float growthRate,
        float competetivness,
        float shadePreference,
        Color color = default
    )
    {
        this.plantName = plantName;
        this.heightPreference = heightPreference;
        this.slopePreference = slopePreference;
        this.exposurePreference = exposurePreference;
        this.moisturePreference = moisturePreference;
        this.color = color != default ? color : Color.white;
        this.seedRadius = seedRadius;
        this.growthRate = growthRate;
        this.competetivness = competetivness;
        this.shadePreference = shadePreference;
    }

    public float CalculateSuitability(float height, float slope, float exposure, float moisture)
    {
        float heightSuitability = 1f - Mathf.Abs(height - heightPreference);
        float slopeSuitability = 1f - Mathf.Abs(slope - slopePreference);
        float exposureSuitability = 1f - Mathf.Abs(exposure - exposurePreference);
        float moistureSuitability = 1f - Mathf.Abs(moisture - moisturePreference);

        return Mathf.Clamp01(
            (
                heightSuitability
                + slopeSuitability
                + exposureSuitability
                + moistureSuitability
            ) / 4f
        );
    }
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
    public int index;

    public Plant(Seed seed, float radius, int age, bool isAlive, bool growing, float shadow, float energy, int index)
    {
        this.seed = seed;
        this.radius = radius;
        this.age = age;
        this.isAlive = isAlive;
        this.growing = growing;
        this.shadow = shadow;
        this.energy = energy;
        this.index = index;
    }
}