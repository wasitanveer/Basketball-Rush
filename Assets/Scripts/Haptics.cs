using UnityEngine;

public static class Haptics
{
    private const string EnabledKey = "BasketballRush.HapticsEnabled";
    private const float MinimumInterval = 0.045f;

    private static bool? cachedEnabled;
    private static float lastHapticTime = -10f;

    public static bool Enabled
    {
        get
        {
            if (!cachedEnabled.HasValue)
            {
                cachedEnabled = PlayerPrefs.GetInt(EnabledKey, 1) == 1;
            }

            return cachedEnabled.Value;
        }
        set
        {
            cachedEnabled = value;
            PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void Light()
    {
        Play(28, 45);
    }

    public static void Medium()
    {
        Play(55, 70);
    }

    public static void Heavy()
    {
        Play(95, 110);
    }

    private static void Play(int amplitude, int durationMilliseconds)
    {
        if (!Enabled || Time.unscaledTime - lastHapticTime < MinimumInterval)
        {
            return;
        }

        lastHapticTime = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            {
                if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                {
                    return;
                }

                using (AndroidJavaClass vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect"))
                using (AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>(
                    "createOneShot", (long)durationMilliseconds, amplitude))
                {
                    vibrator.Call("vibrate", effect);
                }
            }
        }
        catch
        {
        }
#elif UNITY_IOS && !UNITY_EDITOR
        try
        {
            Handheld.Vibrate();
        }
        catch
        {
        }
#endif
    }
}
