using System;
using UnityEngine;

[Serializable]
public class PlantPreference
{
    public string plantName;
    public bool generateSuitabilityMap = true;
    public Color plantColor = Color.white;

    [Range(0f, 1f)] public float heightPreference;
    [Range(0f, 1f)] public float slopePreference;
    [Range(0f, 1f)] public float exposurePreference;
    [Range(0f, 1f)] public float moisturePreference;

    public PlantPreference()
    {
    }

    public PlantPreference(
        string plantName,
        float heightPreference,
        float slopePreference,
        float exposurePreference,
        float moisturePreference,
        Color plantColor = default
    )
    {
        this.plantName = plantName;
        this.heightPreference = heightPreference;
        this.slopePreference = slopePreference;
        this.exposurePreference = exposurePreference;
        this.moisturePreference = moisturePreference;
        this.plantColor = plantColor != default ? plantColor : Color.white;
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
