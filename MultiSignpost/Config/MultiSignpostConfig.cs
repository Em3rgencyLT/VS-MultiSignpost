using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MultiSignpost.Config;

public class MultiSignpostConfig
{
    public static int Version = 1;

    public static MultiSignpostConfig Current = new MultiSignpostConfig();

    [Range(1, 128)]
    [Description("Maximum height in blocks for a signpost.")]
    public int MaxExtensions = 5;

    [Range(0.1, 8.0)]
    [Description("Minimum allowed signpost scale.")]
    public float MinScale = 1f;

    [Range(0.1, 8.0)]
    [Description("Maximum allowed signpost scale. If equal to MinScale, the scale slider is hidden.")]
    public float MaxScale = 1f;

    [Range(0.1, 8.0)]
    [Description("Default scale for newly placed signposts.")]
    public float DefaultScale = 1f;

    public void Sanitize()
    {
        if (MaxExtensions < 1)
        {
            MaxExtensions = 1;
        }

        if (MaxExtensions > 128)
        {
            MaxExtensions = 128;
        }

        MinScale = Clamp(MinScale, 0.1f, 8f);
        MaxScale = Clamp(MaxScale, 0.1f, 8f);

        if (MaxScale < MinScale)
        {
            float temp = MinScale;
            MinScale = MaxScale;
            MaxScale = temp;
        }

        DefaultScale = Clamp(DefaultScale, MinScale, MaxScale);
    }

    public bool HasScaleSlider()
    {
        return Math.Abs(MaxScale - MinScale) > 0.001f;
    }

    private static float Clamp(float value, float min, float max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}