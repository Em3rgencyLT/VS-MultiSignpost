using System;

namespace MultiSignpost.Config;

public class MultiSignpostConfig
{
    public static MultiSignpostConfig Current = new MultiSignpostConfig();

    public int MaxExtensions = 5;
    public float MinScale = 1f;
    public float MaxScale = 1f;
    public float DefaultScale = 1f;

    public void Sanitize()
    {
        if (MaxExtensions < 0)
        {
            MaxExtensions = 0;
        }

        DefaultScale = Clamp(DefaultScale, MinScale, MaxScale);

        if (MaxScale < MinScale)
        {
            float temp = MinScale;
            MinScale = MaxScale;
            MaxScale = temp;
        }

        //Hardcoded limits
        if (MaxExtensions > 128)
        {
            MaxExtensions = 128;
        }
        MinScale = Clamp(MinScale, 0.1f, 8f);
        MaxScale = Clamp(MaxScale, 0.1f, 8f);
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