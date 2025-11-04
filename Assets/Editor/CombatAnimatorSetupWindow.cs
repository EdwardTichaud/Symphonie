#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class CombatAnimatorSetupWindow : EditorWindow
{
    private AnimatorController controller;
    private bool createStatesAndBlendTrees = true;
    private float defaultTransition = 0.08f;

    [MenuItem("Symphonie/Combat/Configurer l'Animator Combat")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<CombatAnimatorSetupWindow>("Animator Combat Setup");
        wnd.minSize = new Vector2(420, 260);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Animator Combat – Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Sélectionne un AnimatorController cible (asset .controller) ou utilise “Récupérer depuis la sélection” si tu as un GameObject avec un Animator dans la scène.",
            MessageType.Info);

        controller = (AnimatorController)EditorGUILayout.ObjectField("Animator Controller", controller, typeof(AnimatorController), false);

        if (GUILayout.Button("Récupérer depuis la sélection (Animator de la scène)"))
        {
            var anim = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Animator>() : null;
            if (anim && anim.runtimeAnimatorController is AnimatorController ac)
                controller = ac;
            else
                EditorUtility.DisplayDialog("Aucun AnimatorController", "Sélectionne un GameObject avec un Animator utilisant un AnimatorController.", "OK");
        }

        EditorGUILayout.Space(8);
        createStatesAndBlendTrees = EditorGUILayout.ToggleLeft("Créer l’ossature (états + BlendTrees + transitions)", createStatesAndBlendTrees);
        defaultTransition = EditorGUILayout.Slider(new GUIContent("Durée de transition défaut (s)"), defaultTransition, 0.02f, 0.2f);

        EditorGUILayout.Space(12);
        GUI.enabled = controller != null;
        if (GUILayout.Button("Configurer l'Animator Combat", GUILayout.Height(36)))
        {
            ConfigureController(controller, createStatesAndBlendTrees, defaultTransition);
        }
        GUI.enabled = true;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Paramètres créés :", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("Floats: SpeedX, SpeedY, IdleVar, DistanceNorm, HitAngle, CastIntensity");
        EditorGUILayout.LabelField("Bools : IsDead, BlockHold, IsEvading");
        EditorGUILayout.LabelField("Ints  : AttackStyle, EvadeDir");
    }

    // === Core ===
    private static void ConfigureController(AnimatorController ac, bool createGraph, float defaultXFade)
    {
        if (ac == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Aucun AnimatorController fourni.", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(ac, "Setup Combat Animator");
        EnsureParameters(ac);

        // Ensure Base layer
        var baseLayer = ac.layers.FirstOrDefault(l => l.name == "Base");
        if (baseLayer.stateMachine == null)
        {
            // If no layers at all, make a default
            if (ac.layers.Length == 0)
            {
                ac.AddLayer(new AnimatorControllerLayer
                {
                    name = "Base",
                    defaultWeight = 1f,
                    stateMachine = new AnimatorStateMachine { name = "Base_SM" }
                });
            }
            else
            {
                // Rename first to Base if needed
                var first = ac.layers[0];
                first.name = "Base";
                ac.layers[0] = first;
            }
            baseLayer = ac.layers[0];
        }
        else
        {
            // Force name "Base"
            if (baseLayer.name != "Base")
            {
                var arr = ac.layers;
                var idx = System.Array.FindIndex(arr, l => l.name == baseLayer.name);
                baseLayer.name = "Base";
                arr[idx] = baseLayer;
                ac.layers = arr;
            }
        }

        if (createGraph)
        {
            BuildGraph(ac, defaultXFade);
        }

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Terminé", "L’Animator a été configuré avec succès.", "OK");
    }

    private static void EnsureParameters(AnimatorController ac)
    {
        // Helper local
        void AddParamIfMissing(string name, AnimatorControllerParameterType type)
        {
            if (!ac.parameters.Any(p => p.name == name && p.type == type))
            {
                ac.AddParameter(new AnimatorControllerParameter { name = name, type = type });
            }
        }

        // Floats
        AddParamIfMissing("SpeedX", AnimatorControllerParameterType.Float);
        AddParamIfMissing("SpeedY", AnimatorControllerParameterType.Float);
        AddParamIfMissing("IdleVar", AnimatorControllerParameterType.Float);
        AddParamIfMissing("DistanceNorm", AnimatorControllerParameterType.Float);
        AddParamIfMissing("HitAngle", AnimatorControllerParameterType.Float);
        AddParamIfMissing("CastIntensity", AnimatorControllerParameterType.Float);

        // Bools
        AddParamIfMissing("IsDead", AnimatorControllerParameterType.Bool);
        AddParamIfMissing("BlockHold", AnimatorControllerParameterType.Bool);
        AddParamIfMissing("IsEvading", AnimatorControllerParameterType.Bool);

        // Ints
        AddParamIfMissing("AttackStyle", AnimatorControllerParameterType.Int);
        AddParamIfMissing("EvadeDir", AnimatorControllerParameterType.Int);
    }

    private static void BuildGraph(AnimatorController ac, float defaultXFade)
    {
        var baseLayer = ac.layers[0];
        var sm = baseLayer.stateMachine;
        Undo.RegisterCompleteObjectUndo(sm, "Build Combat Graph");

        // Clean existing states with same names (optional: keep others)
        string[] wantedNames = {
            "Idle_BT","Move_BT","Approach_BT","Retreat_BT","TurnInPlace",
            "HitReact_BT","Evade_Select","Evade_F","Evade_B","Evade_L","Evade_R",
            "Cast_BT","ItemUse","Attack_BT_A","Attack_BT_B",
            "Knockdown","GetUp","Death"
        };

        foreach (var st in sm.states.Where(s => wantedNames.Contains(s.state.name)).ToArray())
            sm.RemoveState(st.state);
        foreach (var ssm in sm.stateMachines.Where(s => wantedNames.Contains(s.stateMachine.name)).ToArray())
            sm.RemoveStateMachine(ssm.stateMachine);

        // === Create states / blend trees ===
        var idleBT     = CreateOrGetBlendTree(ac, sm, "Idle_BT", BlendTreeType.Simple1D, "IdleVar");
        var moveBT     = CreateOrGetBlendTree2D(ac, sm, "Move_BT", "SpeedX", "SpeedY");
        var approachBT = CreateOrGetBlendTree(ac, sm, "Approach_BT", BlendTreeType.Simple1D, "DistanceNorm");
        var retreatBT  = CreateOrGetBlendTree(ac, sm, "Retreat_BT", BlendTreeType.Simple1D, "DistanceNorm");
        var hitBT      = CreateOrGetBlendTree2DDirectional(ac, sm, "HitReact_BT", "HitAngle");
        var castBT     = CreateOrGetBlendTree(ac, sm, "Cast_BT", BlendTreeType.Simple1D, "CastIntensity");
        var atkA_BT    = CreateOrGetBlendTree(ac, sm, "Attack_BT_A", BlendTreeType.Simple1D, "AttackStyle");
        var atkB_BT    = CreateOrGetBlendTree(ac, sm, "Attack_BT_B", BlendTreeType.Simple1D, "AttackStyle");

        var itemUse    = CreateOrGetState(sm, "ItemUse");
        var knockdown  = CreateOrGetState(sm, "Knockdown");
        var getUp      = CreateOrGetState(sm, "GetUp");
        var death      = CreateOrGetState(sm, "Death");
        var turnIn     = CreateOrGetState(sm, "TurnInPlace");

        // Evade sub-state machine
        var evadeSSM = sm.AddStateMachine("Evade_Select", new Vector3(480, 120, 0));
        var evadeF = evadeSSM.AddState("Evade_F");
        var evadeB = evadeSSM.AddState("Evade_B");
        var evadeL = evadeSSM.AddState("Evade_L");
        var evadeR = evadeSSM.AddState("Evade_R");
        evadeSSM.defaultState = evadeF;

        // Default state
        sm.defaultState = idleBT;

        // === Transitions helpers ===
        AnimatorStateTransition AnyTo(AnimatorState to, float dur) {
            var t = sm.AddAnyStateTransition(to);
            t.duration = dur;
            t.hasExitTime = false;
            t.interruptionSource = TransitionInterruptionSource.Destination;

            return t;
        }

        // AnyState → HitReact (rapide)
        var toHit = AnyTo(hitBT, 0.06f);
        // (Pas de condition obligatoire ici; tu déclenches par CrossFade en code)

        // AnyState → Death (si IsDead)
        var toDeath = AnyTo(death, 0.08f);
        toDeath.AddCondition(AnimatorConditionMode.If, 0, "IsDead");

        // Simple transitions par défaut (tu crossfade en code de toute façon)
        void Link(AnimatorState from, AnimatorState to, float dur)
        {
            var tr = from.AddTransition(to);
            tr.duration = dur;
            tr.hasExitTime = false;
            tr.interruptionSource = TransitionInterruptionSource.Destination;

        }

        // Quelques liens utiles
        Link(idleBT, moveBT, defaultXFade);
        Link(moveBT, idleBT, defaultXFade);
        Link(idleBT, approachBT, 0.06f);
        Link(idleBT, retreatBT, 0.06f);

        // EvadeDir redirection (facultatif, on peut tout faire en code)
        // Ici on crée 4 transitions depuis Evade_Select vers les 4 états
        AddEvadeConditional(evadeSSM, evadeF, 0, 0.05f);
        AddEvadeConditional(evadeSSM, evadeB, 1, 0.05f);
        AddEvadeConditional(evadeSSM, evadeL, 2, 0.05f);
        AddEvadeConditional(evadeSSM, evadeR, 3, 0.05f);

        // Positionnement visuel des nodes (qualité de vie)
        Place(sm, idleBT,     120,  40);
        Place(sm, moveBT,     360,  40);
        Place(sm, approachBT, 120, 200);
        Place(sm, retreatBT,  360, 200);
        Place(sm, hitBT,      600,  40);
        Place(sm, castBT,     600, 200);
        Place(sm, atkA_BT,    840,  40);
        Place(sm, atkB_BT,   1080,  40);
        Place(sm, itemUse,    840, 200);
        Place(sm, turnIn,     1080,200);
        Place(sm, knockdown,  1320, 40);
        Place(sm, getUp,      1320, 200);
        Place(sm, death,      1560, 120);

        EditorUtility.SetDirty(ac);
        EditorUtility.SetDirty(sm);
    }

    // === Helpers (graph) ===
    private static AnimatorState CreateOrGetState(AnimatorStateMachine sm, string name)
    {
        var found = sm.states.FirstOrDefault(s => s.state.name == name).state;
        if (found != null) return found;
        var st = sm.AddState(name);
        return st;
    }

    private static AnimatorState CreateOrGetBlendTree(AnimatorController ac, AnimatorStateMachine sm, string stateName, BlendTreeType type, string param)
    {
        var st = CreateOrGetState(sm, stateName);
        if (st.motion is BlendTree bt && bt.blendType == type && bt.blendParameter == param) return st;

        var newBT = new BlendTree
        {
            name = stateName,
            blendType = type,
            useAutomaticThresholds = true,
            hideFlags = HideFlags.HideInHierarchy
        };
        if (type == BlendTreeType.Simple1D)
            newBT.blendParameter = param;

        AssetDatabase.AddObjectToAsset(newBT, ac);
        st.motion = newBT;
        return st;
    }

    private static AnimatorState CreateOrGetBlendTree2D(AnimatorController ac, AnimatorStateMachine sm, string stateName, string paramX, string paramY)
    {
        var st = CreateOrGetState(sm, stateName);
        var existing = st.motion as BlendTree;
        if (existing != null && existing.blendType == BlendTreeType.FreeformCartesian2D &&
            existing.blendParameter == paramX && existing.blendParameterY == paramY)
            return st;

        var bt = new BlendTree
        {
            name = stateName,
            blendType = BlendTreeType.FreeformCartesian2D,
            useAutomaticThresholds = false,
            blendParameter = paramX,
            blendParameterY = paramY,
            hideFlags = HideFlags.HideInHierarchy
        };
        // Donne 4 emplacements vides (tu remplaceras les motions)
        bt.AddChild(null, new Vector2(-1, 0)); // Strafe_L
        bt.AddChild(null, new Vector2( 1, 0)); // Strafe_R
        bt.AddChild(null, new Vector2( 0, 1)); // Forward_Shuffle
        bt.AddChild(null, new Vector2( 0,-1)); // Back_Shuffle

        AssetDatabase.AddObjectToAsset(bt, ac);
        st.motion = bt;
        return st;
    }

    private static AnimatorState CreateOrGetBlendTree2DDirectional(AnimatorController ac, AnimatorStateMachine sm, string stateName, string angleParam)
    {
        // On utilise un 2D Freeform (on mappe grossièrement des angles sur un cercle unit)
        // Tu pourras remplacer par une logique custom si tu préfères un 1D.
        var st = CreateOrGetState(sm, stateName);
        var bt = new BlendTree
        {
            name = stateName,
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = angleParam,
            blendParameterY = "HitAngleY", // fantôme, non utilisé mais requis par l’API pour certains modes
            hideFlags = HideFlags.HideInHierarchy
        };

        // Emplacements vides pour F/B/L/R (tu poseras les clips)
        // NB: FreeformDirectional2D attend des directions XY; on pose 4 points cardinaux
        bt.AddChild(null, new Vector2( 0,  1)); // F
        bt.AddChild(null, new Vector2( 0, -1)); // B
        bt.AddChild(null, new Vector2(-1,  0)); // L
        bt.AddChild(null, new Vector2( 1,  0)); // R

        AssetDatabase.AddObjectToAsset(bt, ac);
        st.motion = bt;
        return st;
    }

    private static void AddEvadeConditional(AnimatorStateMachine ssm, AnimatorState target, int dirValue, float dur)
    {
        var any = ssm.AddAnyStateTransition(target);
        any.duration = dur;
        any.hasExitTime = false;
        any.interruptionSource = TransitionInterruptionSource.Destination;

        any.AddCondition(AnimatorConditionMode.Equals, dirValue, "EvadeDir");
    }

    private static void Place(AnimatorStateMachine sm, AnimatorState st, float x, float y)
    {
        var state = sm.states.FirstOrDefault(s => s.state == st);
        if (state.state != null)
        {
            state.position = new Vector3(x, y, 0);
            sm.states = sm.states; // force serialize
        }
    }

    private static void Place(AnimatorStateMachine sm, AnimatorState st, int x, int y) => Place(sm, st, (float)x, (float)y);
}
#endif
