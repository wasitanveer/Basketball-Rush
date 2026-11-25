using UnityEngine;

public class PlayerCharacterModel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the Animator from the imported humanoid model. If empty, the component searches its children once.")]
    public Animator animator;

    [Tooltip("Renderers that should be hidden while the camera is first-person: head, face, hair, and torso sections that would block the view.")]
    public Renderer[] firstPersonHiddenRenderers;

    [Header("Animation")]
    public string speedParameter = "Speed";

    CharacterController controller;
    int speedHash;
    bool isFirstPerson = true;
    bool hasVisualModel;

    public bool HasVisualModel => hasVisualModel;
    public bool HasAnimator => animator != null;

    void Awake()
    {
        controller = GetComponentInParent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (!string.IsNullOrEmpty(speedParameter)) speedHash = Animator.StringToHash(speedParameter);
        hasVisualModel = GetComponentsInChildren<Renderer>(true).Length > 0;
        ApplyFirstPersonVisibility();
    }

    void Update()
    {
        if (animator == null || controller == null) return;

        Vector3 velocity = controller.velocity;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        animator.SetFloat(speedHash, speed);
    }

    public void SetFirstPerson(bool firstPerson)
    {
        isFirstPerson = firstPerson;
        ApplyFirstPersonVisibility();
    }

    public void ConfigureModel(Animator modelAnimator, Renderer[] hideInFirstPerson)
    {
        animator = modelAnimator;
        firstPersonHiddenRenderers = hideInFirstPerson;
        hasVisualModel = GetComponentsInChildren<Renderer>(true).Length > 0;
        ApplyFirstPersonVisibility();
    }

    void ApplyFirstPersonVisibility()
    {
        if (firstPersonHiddenRenderers == null) return;

        for (int i = 0; i < firstPersonHiddenRenderers.Length; i++)
        {
            Renderer renderer = firstPersonHiddenRenderers[i];
            if (renderer != null) renderer.enabled = !isFirstPerson;
        }
    }
}
