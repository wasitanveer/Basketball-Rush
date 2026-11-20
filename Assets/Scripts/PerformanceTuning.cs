using UnityEngine;

public static class PerformanceTuning
{
    private const int GameplayFrameRate = 60;
    private const int MenuFrameRate = 30;

    public static void ApplyGameplay()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = GameplayFrameRate;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    public static void ApplyMenu()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = MenuFrameRate;
        Screen.sleepTimeout = SleepTimeout.SystemSetting;
    }

    public static void ApplyPaused()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = MenuFrameRate;
        Screen.sleepTimeout = SleepTimeout.SystemSetting;
    }
}
