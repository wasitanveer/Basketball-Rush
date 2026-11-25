using System.Reflection;
using UnityEngine;

public class Phase7StateProbe : MonoBehaviour
{
    float elapsed;
    int phase;
    float phaseTime;
    BallThrowController ball;
    Rigidbody ballRb;
    GameObject ballObject;
    GameHUD hud;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        GameObject probe = new GameObject("Phase7StateProbe");
        DontDestroyOnLoad(probe);
        probe.AddComponent<Phase7StateProbe>();
    }

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < 1f) return;

        if (phase == 0)
        {
            ball = FindAnyObjectByType<BallThrowController>();
            ballObject = ball != null ? ball.gameObject : null;
            ballRb = ball != null ? ball.GetComponent<Rigidbody>() : null;
            hud = GameHUD.Instance;
            MovePlayerNearBall();
            Invoke("TryPickupBall");
            Log("START_MENU_PICKUP_BLOCKED");
            if (hud != null) hud.StartGame();
            phase = 1;
            phaseTime = elapsed;
            return;
        }

        if (phase == 1 && elapsed - phaseTime > 4.3f)
        {
            MovePlayerNearBall();
            Invoke("TryPickupBall");
            phase = 2;
            phaseTime = elapsed;
            return;
        }

        if (phase == 2 && elapsed - phaseTime > 0.2f)
        {
            Log("AFTER_F_PICKUP");
            Invoke("DropBall");
            phase = 3;
            phaseTime = elapsed;
            return;
        }

        if (phase == 3 && elapsed - phaseTime > 0.2f)
        {
            Log("AFTER_F_RELEASE");
            MovePlayerNearBall();
            Invoke("TryPickupBall");
            phase = 4;
            phaseTime = elapsed;
            return;
        }

        if (phase == 4 && elapsed - phaseTime > 0.2f)
        {
            Log("PICKED_AGAIN");
            ball.enabled = false;
            Invoke("BeginCharge");
            phase = 5;
            phaseTime = elapsed;
            return;
        }

        if (phase == 5 && elapsed - phaseTime > 0.3f)
        {
            Log("CHARGING_PANEL");
            SetField("chargeNormalized", 1f);
            Log("FULL_CHARGE");
            Invoke("ShootBall");
            ball.enabled = true;
            phase = 6;
            phaseTime = elapsed;
            return;
        }

        if (phase == 6 && elapsed - phaseTime > 0.3f)
        {
            Log("SHOT_RELEASED");
            phase = 7;
            phaseTime = elapsed;
            return;
        }

        if (phase == 7 && elapsed - phaseTime > 9f)
        {
            Log("AFTER_RESET");
            MovePlayerNearBall();
            Invoke("TryPickupBall");
            phase = 8;
            phaseTime = elapsed;
            return;
        }

        if (phase == 8 && elapsed - phaseTime > 0.2f)
        {
            Log("PICKUP_FOR_PAUSE");
            ball.enabled = false;
            Invoke("BeginCharge");
            phase = 9;
            phaseTime = elapsed;
            return;
        }

        if (phase == 9 && elapsed - phaseTime > 0.2f)
        {
            CallPrivate("PauseGame");
            ball.enabled = true;
            phase = 10;
            phaseTime = elapsed;
            return;
        }

        if (phase == 10 && elapsed - phaseTime > 0.3f)
        {
            Log("PAUSED_WHILE_CHARGING");
            CallPrivate("ResumeGame");
            phase = 11;
            phaseTime = elapsed;
            return;
        }

        if (phase == 11 && elapsed - phaseTime > 0.3f)
        {
            Log("RESUMED");
            SetHudField("timerFinished", true);
            CallPrivate("RefreshThrowPermission");
            phase = 12;
            phaseTime = elapsed;
            return;
        }

        if (phase == 12 && elapsed - phaseTime > 0.3f)
        {
            Log("GAME_OVER");
            phase = 13;
            Destroy(gameObject);
        }
    }

    void MovePlayerNearBall()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null || ballObject == null) return;
        Vector3 ballPosition = ballObject.transform.position;
        Vector3 position = ballPosition - Vector3.right * 1.4f;
        position.y = player.transform.position.y;
        player.transform.position = position;
    }

    void Log(string label)
    {
        bool powerPanel = false;
        if (hud != null)
        {
            FieldInfo field = typeof(GameHUD).GetField("powerPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            GameObject panel = field != null ? field.GetValue(hud) as GameObject : null;
            powerPanel = panel != null && panel.activeSelf;
        }

        float distance = -1f;
        GameObject player = GameObject.Find("Player");
        if (player != null && ballObject != null) distance = Vector3.Distance(player.transform.position, ballObject.transform.position);

        Debug.Log("[Phase7StateProbe] " + label +
                  " state=" + (ball != null ? ball.State.ToString() : "null") +
                  " charging=" + (ball != null && ball.IsCharging) +
                  " power=" + (ball != null ? ball.CurrentPowerNormalized.ToString("F2") : "n/a") +
                  " powerPanel=" + powerPanel +
                  " canThrow=" + (ball != null && ball.canThrow) +
                  " inFlight=" + (ball != null && ball.IsInFlight) +
                  " kinematic=" + (ballRb != null && ballRb.isKinematic) +
                  " speed=" + (ballRb != null ? ballRb.linearVelocity.magnitude.ToString("F2") : "n/a") +
                  " distance=" + distance.ToString("F2") +
                  " ballY=" + (ballObject != null ? ballObject.transform.position.y.ToString("F2") : "n/a") +
                  " hud=" + (hud != null) +
                  " camParent=" + (Camera.main != null ? Camera.main.transform.parent?.name : "null") +
                  " listeners=" + FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length +
                  " cameras=" + FindObjectsByType<Camera>(FindObjectsSortMode.None).Length +
                  " timeScale=" + Time.timeScale.ToString("F1"));
    }

    void Invoke(string methodName)
    {
        if (ball == null) return;
        MethodInfo method = typeof(BallThrowController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method != null) method.Invoke(ball, null);
    }

    void SetField(string fieldName, object value)
    {
        if (ball == null) return;
        FieldInfo field = typeof(BallThrowController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null) field.SetValue(ball, value);
    }

    void CallPrivate(string methodName)
    {
        if (hud == null) return;
        MethodInfo method = typeof(GameHUD).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method != null) method.Invoke(hud, null);
    }

    void SetHudField(string fieldName, object value)
    {
        if (hud == null) return;
        FieldInfo field = typeof(GameHUD).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null) field.SetValue(hud, value);
    }
}

