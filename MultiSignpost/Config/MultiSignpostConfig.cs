namespace MultiSignpost.Config;

public class MultiSignpostConfig
{
    public static MultiSignpostConfig Current = new MultiSignpostConfig();

    public int MaxExtensions = 5;

    public void Sanitize()
    {
        if (MaxExtensions < 0)
        {
            MaxExtensions = 0;
        }

        //Hardcoded limit
        if (MaxExtensions > 128)
        {
            MaxExtensions = 128;
        }
    }
}