using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ApplyMaterialToChildren : MonoBehaviour
{
    public enum ApplyMode
    {
        ReplaceAllSlots,
        ReplaceFirstSlot,
        AddAsNewSlot
    }

    [Header("Target")]
    [Tooltip("Matériau à appliquer aux renderers enfants.")]
    public Material material;

    [Header("Scope")]
    [Tooltip("Inclure les renderers désactivés dans la hiérarchie.")]
    public bool includeInactive = true;

    [Tooltip("Quels slots modifier sur chaque renderer.")]
    public ApplyMode mode = ApplyMode.ReplaceAllSlots;

    [Tooltip("N'appliquer qu'aux objets de ces Layers (0 = tout).")]
    public LayerMask layerFilter = ~0;

    [Header("Materials handling")]
    [Tooltip("Utiliser sharedMaterials (recommandé en Edit Mode). Décoche pour dupliquer les instances (materials).")]
    public bool useSharedMaterials = true;

    [ContextMenu("Apply Now")]
    public void ApplyNow()
    {
        if (material == null)
        {
            Debug.LogWarning("[ApplyMaterialToChildren] Aucun matériau assigné.", this);
            return;
        }

        var renderers = GetComponentsInChildren<Renderer>(includeInactive);
        int count = 0;

        foreach (var r in renderers)
        {
            if (!r) continue;
            if ((layerFilter.value & (1 << r.gameObject.layer)) == 0) continue;

            List<Material> mats = useSharedMaterials
                ? new List<Material>(r.sharedMaterials)
                : new List<Material>(r.materials);

            if (mats.Count == 0)
            {
                mats.Add(material);
            }
            else
            {
                switch (mode)
                {
                    case ApplyMode.ReplaceAllSlots:
                        for (int i = 0; i < mats.Count; i++) mats[i] = material;
                        break;

                    case ApplyMode.ReplaceFirstSlot:
                        mats[0] = material;
                        break;

                    case ApplyMode.AddAsNewSlot:
                        if (mats[mats.Count - 1] != material)
                            mats.Add(material);
                        break;
                }
            }

            if (useSharedMaterials) r.sharedMaterials = mats.ToArray();
            else r.materials = mats.ToArray();

            count++;
        }

        Debug.Log($"[ApplyMaterialToChildren] Matériau appliqué à {count} renderer(s).", this);
    }
}
