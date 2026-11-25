using UnityEngine;

public class NPCAppearance : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    static readonly Color[] BodyColors =
    {
        new Color(0.18f, 0.38f, 0.62f),
        new Color(0.18f, 0.5f, 0.42f),
        new Color(0.62f, 0.22f, 0.2f),
        new Color(0.75f, 0.45f, 0.14f),
        new Color(0.38f, 0.28f, 0.55f),
        new Color(0.3f, 0.34f, 0.38f)
    };

    static readonly Color[] SkinColors =
    {
        new Color(0.78f, 0.58f, 0.42f),
        new Color(0.62f, 0.42f, 0.28f),
        new Color(0.9f, 0.72f, 0.56f),
        new Color(0.46f, 0.3f, 0.2f)
    };

    void Start()
    {
        Apply();
    }

    void Apply()
    {
        Color bodyColor = BodyColors[Random.Range(0, BodyColors.Length)];
        Color skinColor = SkinColors[Random.Range(0, SkinColors.Length)];
        float height = Random.Range(0.95f, 1.08f);
        float width = Random.Range(0.95f, 1.05f);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            string partName = renderers[i].transform.name;
            if (partName == "Head")
            {
                block.SetColor(BaseColorId, skinColor);
            }
            else if (partName == "Body" || partName.StartsWith("Arm"))
            {
                block.SetColor(BaseColorId, bodyColor);
            }
            else
            {
                continue;
            }

            renderers[i].SetPropertyBlock(block);
        }

        Transform body = transform.Find("Body");
        if (body != null)
        {
            body.localScale = new Vector3(0.48f * width, 0.7f * height, 0.48f * width);
        }

        Transform head = transform.Find("Head");
        if (head != null)
        {
            float headSize = Random.Range(0.32f, 0.36f);
            head.localScale = Vector3.one * headSize;
            head.localPosition = new Vector3(0f, 1.6f + (height - 1f) * 0.4f, 0f);
        }

        Transform leftArm = transform.Find("ArmLeft");
        Transform rightArm = transform.Find("ArmRight");
        float armY = 1.12f + (height - 1f) * 0.35f;
        if (leftArm != null) leftArm.localPosition = new Vector3(-0.3f, armY, 0f);
        if (rightArm != null) rightArm.localPosition = new Vector3(0.3f, armY, 0f);
    }
}
