using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
#endif

/// <summary>
/// One-click AAA-like Animator setup (Unity 6.2 compatible).
/// Ajoute layers/masks façon "slots": Base locomotion, UpperBody (armes), Head/Neck (visée),
/// Face (expressions), Additive overlay, Special full-body override.
/// </summary>
[DisallowMultipleComponent]
public class AAAAnimatorSlotsOneClick : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Assets destination")]
    public string assetsFolder = "Assets/AutoAnimatorSetup";

    [Header("File/Asset names")]
    public string controllerName = "Auto_AnimatorController";
    public string maskFullBodyNoFaceName = "Mask_FullBody_NoFaceBones";
    public string maskUpperBodyArmsName  = "Mask_UpperBody_Arms";
    public string maskHeadNeckNoFaceName = "Mask_HeadNeck_NoFace";
    public string maskFaceOnlyName       = "Mask_FaceOnly";
    public string maskTorsoOnlyName      = "Mask_TorsoOnly";
    public string maskHairSecondaryName  = "Mask_Hair_SecondaryMotion"; // pour Hair_SecondaryMotion
    public string maskClothesSimName     = "Mask_Clothes_Sim";          // pour Clothes_Sim

    [Header("Layer options")]
    public bool faceLayerAdditive = false;    // override par défaut
    public bool additiveTorsoOnly  = true;    // si true, Additive overlay cible TorsoOnly

    [Header("Poids par défaut des layers supplémentaires")]
    [Range(0f,1f)] public float hairLayerDefaultWeight    = 1f; // Hair_SecondaryMotion -> override continu
    [Range(0f,1f)] public float clothesLayerDefaultWeight = 1f; // Clothes_Sim -> override continu

    [Header("Facial bone name fragments (case-insensitive)")]
    public string[] faceNameFragments = new[]
    {
        "jaw","tongue","eye","eyelid","brow","brows","blink","lip","lips","cheek","nose",
        "mouth","orbicularis","levator","depressor","zygomatic","oris","oculi","risorius",
        "lash","eyeball","pupil"
    };

    [Header("Hair / Clothes name fragments (case-insensitive)")]
    [Tooltip("Fragments utilisés pour construire les Avatar Masks des layers Hair/Clothes.")]
    public string[] hairNameFragments = new[]
    {
        "hair","real_hair","bang","bun","ponytail","braid","strand"
    };

    [Tooltip("Fragments d'exclusion (hair). Permet d'éviter par ex. certains bones du visage si nécessaire.")]
    public string[] hairExcludeFragments = new[]
    {
        "brow","lash","lid"
    };

    [Tooltip("Fragments d'inclusion pour Clothes_Sim.")]
    public string[] clothesNameFragments = new[]
    {
        "dress","pants","skirt","coat","cape","cloth","sim","jacket","sleeve","trouser"
    };

    [Tooltip("Fragments d'exclusion pour Clothes_Sim (par défaut aucun).")]
    public string[] clothesExcludeFragments = new string[0];

    [Header("Misc")]
    public bool writeDefaultsOff = true;
    public bool logDetails = true;

    Animator _animator;

    void Reset() => _animator = GetComponent<Animator>();

    // ---------- Utilities ----------
    bool IsFacialBone(Transform t)
    {
        string n = t.name.ToLowerInvariant();
        foreach (var frag in faceNameFragments)
            if (!string.IsNullOrEmpty(frag) && n.Contains(frag)) return true;
        return false;
    }

    static IEnumerable<Transform> EnumerateChildren(Transform root)
    {
        var stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            yield return cur;
            for (int i = 0; i < cur.childCount; i++)
                stack.Push(cur.GetChild(i));
        }
    }

    void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parent = "Assets";
            foreach (var part in path.Replace('\\','/').Split('/'))
            {
                if (string.IsNullOrEmpty(part) || part == "Assets") continue;
                var cur = $"{parent}/{part}";
                if (!AssetDatabase.IsValidFolder(cur))
                    AssetDatabase.CreateFolder(parent, part);
                parent = cur;
            }
        }
    }

    static AnimatorController EnsureAnimatorController(Animator animator, string assetPath, string controllerName, bool log)
    {
        var ctrl = animator.runtimeAnimatorController as AnimatorController;
        if (ctrl == null)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(assetPath, $"{controllerName}.controller"));
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            animator.runtimeAnimatorController = ctrl;
            if (log) Debug.Log($"[AAAAnimatorSlots] Created AnimatorController: {path}");
        }
        return ctrl;
    }

    static void EnsureParameter(AnimatorController ctrl, string name, AnimatorControllerParameterType type, float f=0, int i=0, bool b=false)
    {
        foreach (var p in ctrl.parameters) if (p.name == name) return;
        var param = new AnimatorControllerParameter { name = name, type = type };
        if (type == AnimatorControllerParameterType.Float) param.defaultFloat = f;
        if (type == AnimatorControllerParameterType.Int)   param.defaultInt   = i;
        if (type == AnimatorControllerParameterType.Bool)  param.defaultBool  = b;
        ctrl.AddParameter(param);
    }

    static AnimatorControllerLayer EnsureLayer(AnimatorController ctrl, string layerName)
    {
        foreach (var l in ctrl.layers)
            if (l.name == layerName) return l;

        var newLayer = new AnimatorControllerLayer
        {
            name = layerName,
            defaultWeight = 1f,
            stateMachine = new AnimatorStateMachine { name = $"{layerName}_SM" }
        };
        ctrl.AddLayer(newLayer);
        return newLayer;
    }

    static void EnsureIdleState(AnimatorController ctrl, string layerName, bool writeDefaultsOff)
    {
        for (int i = 0; i < ctrl.layers.Length; i++)
        {
            var l = ctrl.layers[i];
            if (l.name != layerName) continue;
            if (l.stateMachine.states.Length == 0)
            {
                var idle = l.stateMachine.AddState("Idle");
                idle.writeDefaultValues = !writeDefaultsOff ? true : false;
                l.stateMachine.defaultState = idle;
                ctrl.layers[i] = l;
            }
        }
    }

    // ---------- Mask creation helpers ----------
#if UNITY_EDITOR
AvatarMask CreateMask(string assetPath, string maskName, System.Func<Transform, bool> includePredicate)
{
    if (_animator == null || _animator.transform == null)
    {
        Debug.LogError("[AAAAnimatorSlots] Animator manquant sur ce GameObject.");
        return null;
    }

    var mask = new AvatarMask();

    // 1) On nettoie les body parts Humanoid (on ne sen sert pas ici)
    for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

    // 2) On construit le Transform Mask via lAPI publique
    //    (cest la partie qui évite le NRE de 'm_Elements')
    int index = 0;
    foreach (var t in EnumerateChildren(_animator.transform))
    {
        if (!includePredicate(t))
            continue;

        // Ajoute le path de ce Transform
        mask.AddTransformPath(t, false);             // false = n'ajoute pas récursivement les enfants
        mask.SetTransformActive(index, true);        // active ce transform dans le mask
        index++;
    }

    // 3) Sauvegarde en asset
    var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(assetPath, $"{maskName}.mask"));
    AssetDatabase.CreateAsset(mask, path);
    AssetDatabase.SaveAssets();
    if (logDetails) Debug.Log($"[AAAAnimatorSlots] Created AvatarMask: {path}");
    return mask;
}
#endif


    bool IsDescendantOfAny(Transform t, HashSet<Transform> roots)
    {
        var cur = t;
        while (cur != null)
        {
            if (roots.Contains(cur)) return true;
            cur = cur.parent;
        }
        return false;
    }

    AvatarMask BuildMask_FullBody_NoFace()
    {
        return CreateMask(assetsFolder, maskFullBodyNoFaceName, (t) => !IsFacialBone(t));
    }

    AvatarMask BuildMask_TorsoOnly(Transform spine, Transform chest, Transform upperChest)
    {
        var roots = new HashSet<Transform>();
        if (spine) roots.Add(spine);
        if (chest) roots.Add(chest);
        if (upperChest) roots.Add(upperChest);

        return CreateMask(assetsFolder, maskTorsoOnlyName, (t) =>
        {
            if (IsFacialBone(t)) return false;
            return IsDescendantOfAny(t, roots);
        });
    }

    AvatarMask BuildMask_UpperBodyArms(Transform spine, Transform chest, Transform upperChest,
        Transform lUpperArm, Transform lLowerArm, Transform lHand,
        Transform rUpperArm, Transform rLowerArm, Transform rHand)
    {
        var roots = new HashSet<Transform>();
        if (spine) roots.Add(spine);
        if (chest) roots.Add(chest);
        if (upperChest) roots.Add(upperChest);
        if (lUpperArm) roots.Add(lUpperArm);
        if (lLowerArm) roots.Add(lLowerArm);
        if (lHand) roots.Add(lHand);
        if (rUpperArm) roots.Add(rUpperArm);
        if (rLowerArm) roots.Add(rLowerArm);
        if (rHand) roots.Add(rHand);

        return CreateMask(assetsFolder, maskUpperBodyArmsName, (t) =>
        {
            if (IsFacialBone(t)) return false;
            return IsDescendantOfAny(t, roots);
        });
    }

    AvatarMask BuildMask_HeadNeck_NoFace(Transform neck, Transform head)
    {
        var roots = new HashSet<Transform>();
        if (neck) roots.Add(neck);
        if (head) roots.Add(head);

        return CreateMask(assetsFolder, maskHeadNeckNoFaceName, (t) =>
        {
            if (!IsDescendantOfAny(t, roots)) return false;
            if (IsFacialBone(t)) return false;
            return true;
        });
    }

    AvatarMask BuildMask_FaceOnly(Transform head, Transform jaw, Transform lEye, Transform rEye)
    {
        var special = new HashSet<Transform>();
        if (jaw)  special.Add(jaw);
        if (lEye) special.Add(lEye);
        if (rEye) special.Add(rEye);

        return CreateMask(assetsFolder, maskFaceOnlyName, (t) =>
        {
            // Inclure tout ce que lon reconnaît comme facial OU les bones spéciaux jaw/eyes
            if (special.Contains(t)) return true;
            if (IsFacialBone(t)) return true;

            // Beaucoup de rigs mettent les paupières/yeux sous Head avec des noms neutres :
            // si lancêtre direct est eye/jaw connus, le predicate au-dessus suffira.
            return false;
        });
    }

    AvatarMask BuildMask_FromFragments(string maskName, string[] includeFragments, string[] excludeFragments, bool includeAllIfEmpty)
    {
        // Listes préparées en minuscules pour faciliter les comparaisons case-insensitive.
        var include = new List<string>();
        if (includeFragments != null)
        {
            foreach (var f in includeFragments)
            {
                if (!string.IsNullOrEmpty(f)) include.Add(f.ToLowerInvariant());
            }
        }

        var exclude = new List<string>();
        if (excludeFragments != null)
        {
            foreach (var f in excludeFragments)
            {
                if (!string.IsNullOrEmpty(f)) exclude.Add(f.ToLowerInvariant());
            }
        }

        // Predicate commun : on calcule le chemin relatif et on le compare aux fragments.
        return CreateMask(assetsFolder, maskName, (t) =>
        {
            string relPath = AnimationUtility.CalculateTransformPath(t, _animator.transform) ?? string.Empty;
            relPath = relPath.ToLowerInvariant();

            foreach (var frag in exclude)
            {
                if (relPath.Contains(frag)) return false;
            }

            if (include.Count == 0)
                return includeAllIfEmpty;

            foreach (var frag in include)
            {
                if (relPath.Contains(frag)) return true;
            }

            return false;
        });
    }

    AvatarMask BuildMask_HairSecondary()
    {
        // On autorise les strings additionnelles pour couvrir d'éventuels rigs CC ou personnalisés.
        return BuildMask_FromFragments(maskHairSecondaryName, hairNameFragments, hairExcludeFragments, false);
    }

    AvatarMask BuildMask_ClothesSim()
    {
        // Le flag includeAllIfEmpty reste à false : si l'utilisateur supprime tous les fragments,
        // aucun os ne sera retenu, ce qui évite les surprises.
        return BuildMask_FromFragments(maskClothesSimName, clothesNameFragments, clothesExcludeFragments, false);
    }

    // ---------- Main setup ----------
    public void RunSetup()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
        {
            Debug.LogError("[AAAAnimatorSlots] Aucun Animator trouvé sur ce GameObject.");
            return;
        }

        EnsureFolder(assetsFolder);

        // Créer/Assigner controller
        var ctrl = EnsureAnimatorController(_animator, assetsFolder, controllerName, logDetails);

        // Récup os Humanoid clés
        Transform spine      = _animator.GetBoneTransform(HumanBodyBones.Spine);
        Transform chest      = _animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform upperChest = _animator.GetBoneTransform(HumanBodyBones.UpperChest);

        Transform neck = _animator.GetBoneTransform(HumanBodyBones.Neck);
        Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);

        Transform lUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform lLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform lHand     = _animator.GetBoneTransform(HumanBodyBones.LeftHand);

        Transform rUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform rLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform rHand     = _animator.GetBoneTransform(HumanBodyBones.RightHand);

        Transform jaw  = _animator.GetBoneTransform(HumanBodyBones.Jaw);
        Transform lEye = _animator.GetBoneTransform(HumanBodyBones.LeftEye);
        Transform rEye = _animator.GetBoneTransform(HumanBodyBones.RightEye);

        // Créer les masks
        var mFullNoFace = BuildMask_FullBody_NoFace();
        var mUpperArms  = BuildMask_UpperBodyArms(spine, chest, upperChest, lUpperArm, lLowerArm, lHand, rUpperArm, rLowerArm, rHand);
        var mHeadNeck   = BuildMask_HeadNeck_NoFace(neck, head);
        var mFaceOnly   = BuildMask_FaceOnly(head, jaw, lEye, rEye);
        var mTorsoOnly  = BuildMask_TorsoOnly(spine, chest, upperChest);
        var mHair       = BuildMask_HairSecondary();
        var mClothes    = BuildMask_ClothesSim();

        // Params
        EnsureParameter(ctrl, "UpperBodyBlend", AnimatorControllerParameterType.Float, 1f);
        EnsureParameter(ctrl, "AdditiveWeight", AnimatorControllerParameterType.Float, 0f);
        EnsureParameter(ctrl, "AimYaw",  AnimatorControllerParameterType.Float, 0f);
        EnsureParameter(ctrl, "AimPitch",AnimatorControllerParameterType.Float, 0f);
        EnsureParameter(ctrl, "FaceWeight", AnimatorControllerParameterType.Float, 1f);
        EnsureParameter(ctrl, "FaceIndex",  AnimatorControllerParameterType.Int,   0);
        EnsureParameter(ctrl, "IsFacialPlaying", AnimatorControllerParameterType.Bool, 0, 0, true);
        EnsureParameter(ctrl, "SpecialPlay", AnimatorControllerParameterType.Trigger);

        // Layers
        // 1) LowerBody (locomotion de base)
        {
            var baseLayer = ctrl.layers[0];
            baseLayer.name = "LowerBody_Locomotion";
            baseLayer.defaultWeight = 1f;
            ctrl.layers[0] = baseLayer;
            EnsureIdleState(ctrl, "LowerBody_Locomotion", writeDefaultsOff);
        }

        // 2) UpperBody (bras/armes)
        {
            var L = EnsureLayer(ctrl, "UpperBody_ArmsWeapons");
            var layers = ctrl.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name != L.name) continue;
                layers[i].defaultWeight = 1f;
                layers[i].blendingMode  = AnimatorLayerBlendingMode.Override;
                layers[i].avatarMask    = mUpperArms;
                ctrl.layers = layers;
                break;
            }
            EnsureIdleState(ctrl, "UpperBody_ArmsWeapons", writeDefaultsOff);
        }

        // 3) Head/Neck aim (orientation tête, no face)
        {
            var L = EnsureLayer(ctrl, "HeadNeck_Aim");
            var layers = ctrl.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name != L.name) continue;
                layers[i].defaultWeight = 1f;
                layers[i].blendingMode  = AnimatorLayerBlendingMode.Override;
                layers[i].avatarMask    = mHeadNeck;
                ctrl.layers = layers;
                break;
            }
            EnsureIdleState(ctrl, "HeadNeck_Aim", writeDefaultsOff);
        }

        // 4) Face (expressions)
        {
            var L = EnsureLayer(ctrl, "Face_Expressions");
            var layers = ctrl.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name != L.name) continue;
                layers[i].defaultWeight = 1f;  // pilotable avec param FaceWeight si besoin
                layers[i].blendingMode  = faceLayerAdditive ? AnimatorLayerBlendingMode.Additive
                                                            : AnimatorLayerBlendingMode.Override;
                layers[i].avatarMask    = mFaceOnly;
                ctrl.layers = layers;
                break;
            }
            EnsureIdleState(ctrl, "Face_Expressions", writeDefaultsOff);
        }

        // 5) Additive overlay (micro-mouvements respir, recoil léger)
        {
            var L = EnsureLayer(ctrl, "Additive_Overlay");
            var layers = ctrl.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name != L.name) continue;
                layers[i].defaultWeight = 0f; // pilote via AdditiveWeight
                layers[i].blendingMode  = AnimatorLayerBlendingMode.Additive;
                layers[i].avatarMask    = additiveTorsoOnly ? mTorsoOnly : null; // null = full body
                ctrl.layers = layers;
                break;
            }
            EnsureIdleState(ctrl, "Additive_Overlay", writeDefaultsOff);
        }

        // 6) Special full-body override (ultime/ciné)
        {
            var L = EnsureLayer(ctrl, "Special_FullBody_Override");
            var layers = ctrl.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name != L.name) continue;
                layers[i].defaultWeight = 0f; // activé ponctuellement
                layers[i].blendingMode  = AnimatorLayerBlendingMode.Override;
                layers[i].avatarMask    = mFullNoFace; // on laisse le visage libre
                ctrl.layers = layers;
                break;
            }
            EnsureIdleState(ctrl, "Special_FullBody_Override", writeDefaultsOff);
        }

        // 7) Hair secondary motion (cheveux dynamiques)
        {
            var L = EnsureLayer(ctrl, "Hair_SecondaryMotion");
            var layers = ctrl.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name != L.name) continue;
                layers[i].defaultWeight = Mathf.Clamp01(hairLayerDefaultWeight);
                layers[i].blendingMode  = AnimatorLayerBlendingMode.Override; // les bones cheveux nécessitent un override
                layers[i].avatarMask    = mHair; // peut être null si aucun os hair n'a été trouvé
                ctrl.layers = layers;
                break;
            }
            EnsureIdleState(ctrl, "Hair_SecondaryMotion", writeDefaultsOff);
        }

        // 8) Clothes simulation (vêtements secondaires)
        {
            var L = EnsureLayer(ctrl, "Clothes_Sim");
            var layers = ctrl.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name != L.name) continue;
                layers[i].defaultWeight = Mathf.Clamp01(clothesLayerDefaultWeight);
                layers[i].blendingMode  = AnimatorLayerBlendingMode.Override;
                layers[i].avatarMask    = mClothes; // null si aucun os vêtement n'est présent
                ctrl.layers = layers;
                break;
            }
            EnsureIdleState(ctrl, "Clothes_Sim", writeDefaultsOff);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(ctrl);
        EditorUtility.SetDirty(this.gameObject);

        if (logDetails)
        {
            Debug.Log(
@"[AAAAnimatorSlots] Configuration terminée.

Layers:
- LowerBody_Locomotion : couche de base (bassin + jambes, locomotion).
- UpperBody_ArmsWeapons : override, bras + torse haut (armes, visée torse).
- HeadNeck_Aim : override, cou+tête (orientation), sans os faciaux.
- Face_Expressions : override (ou additive), uniquement rig facial.
- Additive_Overlay : additive (torse ou full body), pour micro-poses.
- Special_FullBody_Override : override, tout le corps (sauf os faciaux).
- Hair_SecondaryMotion : override, cheveux et appendices dynamiques.
- Clothes_Sim : override, vêtements secondaires / simulations tissus.

IMPORTANT: gardez les 'blendShape.*' UNIQUEMENT dans Face_Expressions."
            );
        }
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(AAAAnimatorSlotsOneClick))]
public class AAAAnimatorSlotsOneClickEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var t = (AAAAnimatorSlotsOneClick)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Configure AAA Animator Slots"))
            t.RunSetup();

        EditorGUILayout.HelpBox(
            "Astuce: ajoute des fragments de noms (ex: 'CC_Base_Teeth', 'Eyebrows') dans 'faceNameFragments' si ton rig CC a des noms particuliers.\n" +
            "Write Defaults: si tu coches 'writeDefaultsOff', les états Idle créés ont WD=Off.",
            MessageType.Info);
    }
}
#endif
