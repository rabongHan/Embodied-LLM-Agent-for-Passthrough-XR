using UnityEngine;

public class MaterialSlotchanger : MonoBehaviour
{
    [Header("Target Mesh")]
    public SkinnedMeshRenderer skinnedMesh;

    [Header("Material Override")]
    public int materialIndex = 3; // 4th slot
    public Material overrideMaterial;

    private Material[] originalMaterials;
    private bool isOverridden = false;

    public void SetMaterialOverride(bool enable)
    {
        if (skinnedMesh == null || overrideMaterial == null)
        {
            Debug.LogError("Missing SkinnedMeshRenderer or override material.");
            return;
        }

        var materials = skinnedMesh.materials;

        if (materialIndex < 0 || materialIndex >= materials.Length)
        {
            Debug.LogError($"Material index {materialIndex} is out of range.");
            return;
        }

        if (enable && !isOverridden)
        {
            // Backup current
            originalMaterials = skinnedMesh.materials;
            Material[] modified = (Material[])originalMaterials.Clone();
            modified[materialIndex] = overrideMaterial;
            skinnedMesh.materials = modified;
            isOverridden = true;
        }
        else if (!enable && isOverridden)
        {
            // Restore
            skinnedMesh.materials = originalMaterials;
            isOverridden = false;
        }
    }
}