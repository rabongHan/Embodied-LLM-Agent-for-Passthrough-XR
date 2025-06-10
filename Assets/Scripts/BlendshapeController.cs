using UnityEngine;

public class BlendshapeController : MonoBehaviour
{
    [Header("Target Mesh with Blendshapes")]
    public SkinnedMeshRenderer skinnedMesh;

    [Header("Blendshape Settings")]
    public string blendShape1 = "Хмур брови";
    public string blendShape2 = "глаза о";
    [Range(0f, 100f)] public float activeWeight = 100f;

    private int index1 = -1;
    private int index2 = -1;
    private float originalWeight1 = 0f;
    private float originalWeight2 = 0f;
    private bool isOverriding = false;

    void Awake()
    {
        if (skinnedMesh == null)
        {
            Debug.LogError("SkinnedMeshRenderer not assigned.");
            return;
        }

        var mesh = skinnedMesh.sharedMesh;
        index1 = mesh.GetBlendShapeIndex(blendShape1);
        index2 = mesh.GetBlendShapeIndex(blendShape2);

        if (index1 < 0 || index2 < 0)
            Debug.LogError("Invalid blendshape names provided.");
    }

    public void SetBlendshapeOverride(bool enable)
    {
        if (skinnedMesh == null || index1 < 0 || index2 < 0)
            return;

        if (enable && !isOverriding)
        {
            originalWeight1 = skinnedMesh.GetBlendShapeWeight(index1);
            originalWeight2 = skinnedMesh.GetBlendShapeWeight(index2);

            skinnedMesh.SetBlendShapeWeight(index1, activeWeight);
            skinnedMesh.SetBlendShapeWeight(index2, activeWeight);
            isOverriding = true;
        }
        else if (!enable && isOverriding)
        {
            skinnedMesh.SetBlendShapeWeight(index1, originalWeight1);
            skinnedMesh.SetBlendShapeWeight(index2, originalWeight2);
            isOverriding = false;
        }
    }
}