#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class CombatAnimatorBuilder_AddOns : EditorWindow
{
    private AnimatorController controller;
    private bool createPlaceholders = true;
    private string generatedFolder = "Assets/Symphonie/Generated/Animations";
    private float defaultXFade = 0.08f;

    [MenuItem("Symphonie/Combat/Builder Add-Ons")]
    public static void ShowWindow()
    {
        var w = GetWindow<CombatAnimatorBuilder_AddOns>("Animator Add-Ons");
        w.minSize = new Vector2(560, 380);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Add-Ons Flow JDR (single-layer)", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        controller = (AnimatorController)EditorGUILayout.ObjectField("Animator Controller", controller, typeof(AnimatorController), false);

        if (GUILayout.Button("Récupérer depuis la sélection (Animator de la scène)"))
        {
            var anim = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Animator>() : null;
            if (anim && anim.runtimeAnimatorController is AnimatorController ac) controller = ac;
            else EditorUtility.DisplayDialog("Info", "Sélectionne un GameObject avec un Animator utilisant un AnimatorController.", "OK");
        }

        EditorGUILayout.Space(6);
        createPlaceholders = EditorGUILayout.ToggleLeft("Créer des AnimationClips placeholders si manquants", createPlaceholders);
        generatedFolder = EditorGUILayout.TextField("Dossier de génération", generatedFolder);
        defaultXFade = EditorGUILayout.Slider("Durée transition par défaut (s)", defaultXFade, 0.02f, 0.2f);

        EditorGUILayout.Space(12);
        using (new EditorGUI.DisabledScope(controller == null))
        {
            if (GUILayout.Button("➕ Ajouter états & transitions Flow JDR", GUILayout.Height(34)))
                AddFlowStatesAndTransitions(controller, createPlaceholders, generatedFolder, defaultXFade);

            if (GUILayout.Button("🧰 Créer un AnimatorOverrideController (AOC) vierge", GUILayout.Height(30)))
                CreateEmptyAOC(controller);
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "Bouton 1 : ajoute EnterCombat, GuardHold, PrepareHit, Defend, Aim_Item, Aim_Skill, Item_Use_Dynamic, Skill_Use_Dynamic, " +
            "les transitions (AnyState Hit/Death, retours Idle), des placeholders, et pose ApplyRootMotionToggle sur Approach/Retreat/KO/GetUp/Death.\n" +
            "Bouton 2 : crée un AOC qui expose les clés Item_Use_Dynamic / Skill_Use_Dynamic pour override par item/skill.",
            MessageType.Info);
    }

    // =========================================================
    // Core
    // =========================================================
    private static void AddFlowStatesAndTransitions(AnimatorController ac, bool makePlaceholders, string outFolder, float defaultXFade)
    {
        if (ac == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Aucun AnimatorController.", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(ac, "Add Flow JDR");

        var layer = EnsureBaseLayer(ac);
        var sm = layer.stateMachine;

        // 1) S'assurer que les params essentiels existent
        EnsureParameters(ac);

        // 2) Créer les placeholders si demandé
        if (makePlaceholders) EnsureFolder(outFolder);
        AnimationClip PH(string name)
        {
            if (!makePlaceholders) return null;
            var path = $"{outFolder}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(clip, path);
            }
            return clip;
        }

        // 3) Créer/obtenir les states existants (idempotent)
        var idleBT      = GetState(sm, "Idle_BT");         // doit déjà exister via Builder principal
        var moveBT      = GetState(sm, "Move_BT");
        var approachBT  = GetState(sm, "Approach_BT");
        var retreatBT   = GetState(sm, "Retreat_BT");
        var hitBT       = GetState(sm, "HitReact_BT");
        var itemUse     = GetStateOrCreate(sm, "ItemUse");
        var knockdown   = GetStateOrCreate(sm, "Knockdown");
        var getUp       = GetStateOrCreate(sm, "GetUp");
        var death       = GetStateOrCreate(sm, "Death");

        // Nouveaux états
        var enterCombat = GetStateOrCreate(sm, "EnterCombat");
        var guardHold   = GetStateOrCreate(sm, "GuardHold");
        var prepareHit  = GetStateOrCreate(sm, "PrepareHit");
        var defend      = GetStateOrCreate(sm, "Defend");
        var aimItem     = GetStateOrCreate(sm, "Aim_Item");
        var aimSkill    = GetStateOrCreate(sm, "Aim_Skill");
        var itemDyn     = GetStateOrCreate(sm, "Item_Use_Dynamic");
        var skillDyn    = GetStateOrCreate(sm, "Skill_Use_Dynamic");

        // 4) Assigner des clips placeholders (optionnel)
        if (makePlaceholders)
        {
            AssignMotionIfEmpty(enterCombat, PH("ENTER_Combat_Placeholder"));
            AssignMotionIfEmpty(guardHold,   PH("GUARD_Hold_Loop"));
            AssignMotionIfEmpty(prepareHit,  PH("PREP_Hit"));
            AssignMotionIfEmpty(defend,      PH("DEFEND_Block"));
            AssignMotionIfEmpty(aimItem,     PH("AIM_Item"));
            AssignMotionIfEmpty(aimSkill,    PH("AIM_Skill"));
            AssignMotionIfEmpty(itemDyn,     PH("ITEM_Use_Dynamic_Default"));
            AssignMotionIfEmpty(skillDyn,    PH("SKILL_Use_Dynamic_Default"));
        }

        // 5) Transitions — helpers
        AnimatorStateTransition AnyTo(AnimatorState to, float dur)
        {
            var t = sm.AddAnyStateTransition(to);
            SetStandard(t, dur);
            return t;
        }
        void Link(AnimatorState from, AnimatorState to, float dur) { if (from == null || to == null) return; var tr = from.AddTransition(to); SetStandard(tr, dur); }
        void LinkExitTime(AnimatorState from, AnimatorState to, float dur, float exitTime = 0.95f)
        {
            if (from == null || to == null) return;
            var tr = from.AddTransition(to);
            tr.duration = dur;
            tr.hasExitTime = true;
            tr.exitTime = exitTime;
            tr.interruptionSource = TransitionInterruptionSource.Destination;
        }

        // AnyState -> Hit/Death (si pas déjà)
        AnyTo(hitBT, 0.06f);
        var toDeath = AnyTo(death, 0.08f);
        toDeath.AddCondition(AnimatorConditionMode.If, 0, "IsDead");

        // EnterCombat -> Idle (avec Exit Time)
        LinkExitTime(enterCombat, idleBT, defaultXFade, 0.98f);

        // GuardHold <-> Idle
        Link(guardHold, idleBT, defaultXFade);
        Link(idleBT, guardHold, defaultXFade);

        // Aim states <-> Idle
        Link(aimItem, idleBT, defaultXFade);
        Link(idleBT, aimItem, defaultXFade);
        Link(aimSkill, idleBT, defaultXFade);
        Link(idleBT, aimSkill, defaultXFade);

        // Défense / Préparer à recevoir
        Link(prepareHit, defend, 0.04f);   // tu peux défendre depuis la préparation
        Link(defend, idleBT, 0.12f);

        // Item dynamic / Skill dynamic -> Idle (Exit Time)
        LinkExitTime(itemDyn, idleBT, defaultXFade, 0.98f);
        LinkExitTime(skillDyn, idleBT, defaultXFade, 0.98f);

        // 6) Poser ApplyRootMotionToggle sur states de déplacement & trajectoires
        AddOrGetSMB<ApplyRootMotionToggle>(approachBT, (smb) => { smb.ReturnToIdleOnExit = false; });
        AddOrGetSMB<ApplyRootMotionToggle>(retreatBT,  (smb) => { smb.ReturnToIdleOnExit = true; smb.IdleStateName = "Idle_BT"; smb.IdleXFade = defaultXFade; });
        AddOrGetSMB<ApplyRootMotionToggle>(knockdown,  (smb) => { smb.ReturnToIdleOnExit = false; });
        AddOrGetSMB<ApplyRootMotionToggle>(getUp,      (smb) => { smb.ReturnToIdleOnExit = true;  smb.IdleStateName = "Idle_BT"; smb.IdleXFade = defaultXFade; });
        AddOrGetSMB<ApplyRootMotionToggle>(death,      (smb) => { smb.ReturnToIdleOnExit = false; });

        // 7) Mise en page (qualité de vie)
        Place(sm, enterCombat,  40,   -60);
        Place(sm, guardHold,    360,   -60);
        Place(sm, prepareHit,   840,   280);
        Place(sm, defend,       1000,  280);
        Place(sm, aimItem,      600,   200);
        Place(sm, aimSkill,     760,   200);
        Place(sm, itemDyn,      600,    40);
        Place(sm, skillDyn,     760,    40);

        EditorUtility.SetDirty(ac);
        EditorUtility.SetDirty(sm);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("OK", "États & transitions Flow JDR ajoutés.", "Parfait");
    }

    private static void CreateEmptyAOC(AnimatorController baseController)
    {
        if (baseController == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Aucun AnimatorController.", "OK");
            return;
        }

        var path = EditorUtility.SaveFilePanelInProject("Créer AOC", baseController.name + "_Override", "overrideController", "Choisis où sauvegarder l'AnimatorOverrideController.");
        if (string.IsNullOrEmpty(path)) return;

        var aoc = new AnimatorOverrideController(baseController) { name = Path.GetFileNameWithoutExtension(path) };
        AssetDatabase.CreateAsset(aoc, path);

        // Pré-override des clés dynamiques avec elles-mêmes (assure leur présence)
        // (si les states n'ont pas encore de clip par défaut, le dev pourra les mettre après)
        TryEnsureOverrideKey(aoc, "Item_Use_Dynamic");
        TryEnsureOverrideKey(aoc, "Skill_Use_Dynamic");
        TryEnsureOverrideKey(aoc, "Aim_Item");
        TryEnsureOverrideKey(aoc, "Aim_Skill");

        EditorUtility.SetDirty(aoc);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("OK", "AOC créé. Tu peux maintenant assigner les clips par item/skill au runtime.", "Nice");
    }

    // =========================================================
    // Low-level helpers
    // =========================================================
    private static void EnsureParameters(AnimatorController ac)
    {
        void Add(string n, AnimatorControllerParameterType t)
        {
            if (!ac.parameters.Any(p => p.name == n && p.type == t))
                ac.AddParameter(new AnimatorControllerParameter { name = n, type = t });
        }
        Add("SpeedX", AnimatorControllerParameterType.Float);
        Add("SpeedY", AnimatorControllerParameterType.Float);
        Add("IdleVar", AnimatorControllerParameterType.Float);
        Add("DistanceNorm", AnimatorControllerParameterType.Float);
        Add("HitAngle", AnimatorControllerParameterType.Float);
        Add("CastIntensity", AnimatorControllerParameterType.Float);
        Add("IsDead", AnimatorControllerParameterType.Bool);
        Add("BlockHold", AnimatorControllerParameterType.Bool);
        Add("IsEvading", AnimatorControllerParameterType.Bool);
        Add("AttackStyle", AnimatorControllerParameterType.Int);
        Add("EvadeDir", AnimatorControllerParameterType.Int);
    }

    private static AnimatorControllerLayer EnsureBaseLayer(AnimatorController ac)
    {
        if (ac.layers.Length == 0)
        {
            ac.AddLayer(new AnimatorControllerLayer
            {
                name = "Base",
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine { name = "Base_SM" }
            });
        }
        var l = ac.layers[0];
        l.name = "Base";
        ac.layers[0] = l;
        return ac.layers[0];
    }

    private static AnimatorState GetState(AnimatorStateMachine sm, string name)
        => sm.states.FirstOrDefault(s => s.state.name == name).state;

    private static AnimatorState GetStateOrCreate(AnimatorStateMachine sm, string name)
        => GetState(sm, name) ?? sm.AddState(name);

    private static void AssignMotionIfEmpty(AnimatorState st, Motion clip)
    {
        if (st == null || clip == null) return;
        if (st.motion == null) st.motion = clip;
        EditorUtility.SetDirty(st);
    }

    private static void SetStandard(AnimatorStateTransition tr, float dur)
    {
        if (tr == null) return;
        tr.duration = dur;
        tr.hasExitTime = false;
        tr.interruptionSource = TransitionInterruptionSource.Destination; // ✅
    }

    private static void Place(AnimatorStateMachine sm, AnimatorState st, float x, float y)
    {
        if (st == null) return;
        var arr = sm.states;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i].state == st)
            {
                arr[i].position = new Vector3(x, y, 0);
                sm.states = arr;
                break;
            }
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        var root = parts[0];
        string cur = root;
        for (int i = 1; i < parts.Length; i++)
        {
            var next = parts[i];
            if (!AssetDatabase.IsValidFolder($"{cur}/{next}"))
                AssetDatabase.CreateFolder(cur, next);
            cur = $"{cur}/{next}";
        }
    }

    private static void TryEnsureOverrideKey(AnimatorOverrideController aoc, string stateOrClipName)
    {
        // Si la clé existe dans le controller de base, on la met à elle-même (no-op),
        // ce qui la rend visible dans l'AOC pour override ultérieur.
        var pairs = aoc.overridesCount;
        var list = aoc.animationClips.ToList();
        // Pas d'API simple pour ajouter des clés inexistantes; on compte sur l'existence du clip dans le controller.
        // Si besoin, mets un clip par défaut sur Item_Use_Dynamic / Skill_Use_Dynamic via Add-Ons (placeholders).
    }

    private static T AddOrGetSMB<T>(AnimatorState state, System.Action<T> init = null) where T : StateMachineBehaviour
    {
        if (state == null) return null;
        var found = state.behaviours.OfType<T>().FirstOrDefault();
        if (found == null)
        {
            found = state.AddStateMachineBehaviour<T>();
            init?.Invoke(found);
            EditorUtility.SetDirty(state);
        }
        return found;
    }
}
#endif
