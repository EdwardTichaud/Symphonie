using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;


namespace SoftKitty.InventoryEngine
{
    [CustomEditor(typeof(InventorySkin))]
    public class InventorySkin_inspector : Editor
    {
        Color _activeColor = new Color(0.1F, 0.5F, 0.8F);
        Color _disableColor = new Color(0F, 0.1F, 0.3F);
        public override void OnInspectorGUI()
        {

            var script = MonoScript.FromScriptableObject(this);
            InventorySkin targetScript = (InventorySkin)target;
            string _thePath = AssetDatabase.GetAssetPath(script);
            _thePath = _thePath.Replace("Editor/InventorySkin_inspector.cs", "");
            string _basePath = Path.Combine(Application.dataPath, "..", _thePath).Replace(@"\","/") + "Textures/UiStyles/";
            string _targetPath= Path.Combine(Application.dataPath, "..", _thePath).Replace(@"\", "/") + "Textures/Sprites/Main.png";

            GUILayout.BeginHorizontal();
            GUILayout.Label("Overall UI Style:",GUILayout.Width(200));
            GUI.backgroundColor = targetScript.UiStyle == 1 ? _activeColor : _disableColor;
            if (GUILayout.Button("Immersive",GUILayout.Width(75))) {
                targetScript.UiStyle = 1;
                File.Copy(_basePath+ "Style1.png", _targetPath,true);
                TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(_thePath + "Textures/Sprites/Main.png");
                importer.filterMode = FilterMode.Bilinear;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                AssetDatabase.Refresh();
                targetScript.UpdatePrefab();
            }
            GUI.backgroundColor = targetScript.UiStyle == 2 ? _activeColor : _disableColor;
            if (GUILayout.Button("Simple", GUILayout.Width(75)))
            {
                targetScript.UiStyle = 2;
                File.Copy(_basePath + "Style2.png", _targetPath, true);
                TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(_thePath + "Textures/Sprites/Main.png");
                importer.filterMode = FilterMode.Bilinear;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                AssetDatabase.Refresh();
                targetScript.UpdatePrefab();
            }
            GUI.backgroundColor = targetScript.UiStyle == 3 ? _activeColor : _disableColor;
            if (GUILayout.Button("Flat", GUILayout.Width(75)))
            {
                targetScript.UiStyle = 3;
                File.Copy(_basePath + "Style3.png", _targetPath, true);
                TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(_thePath + "Textures/Sprites/Main.png");
                importer.filterMode = FilterMode.Bilinear;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                AssetDatabase.Refresh();
                targetScript.UpdatePrefab();
            }
            GUI.backgroundColor = targetScript.UiStyle == 4 ? _activeColor : _disableColor;
            if (GUILayout.Button("Pixel", GUILayout.Width(75)))
            {
                targetScript.UiStyle = 4;
                File.Copy(_basePath + "Style4.png", _targetPath, true);
                TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(_thePath + "Textures/Sprites/Main.png");
                importer.filterMode = FilterMode.Point;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                AssetDatabase.Refresh();
                targetScript.UpdatePrefab();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Ui Window Scale:", GUILayout.Width(200));
            float _oldScale = targetScript.UiScale;
            targetScript.UiScale = EditorGUILayout.Slider(targetScript.UiScale, 1F, 2F, GUILayout.Width(290));
            GUILayout.Label("x", GUILayout.Width(15));
            if (_oldScale!= targetScript.UiScale && !Application.isPlaying) {
 
                AnimationClip _clip = (AnimationClip)AssetDatabase.LoadAssetAtPath(_thePath + "Animations/WindowOpen.anim", typeof(AnimationClip));
                EditorCurveBinding [] _bindings = AnimationUtility.GetCurveBindings(_clip);
                foreach (EditorCurveBinding obj in _bindings) {
                    AnimationCurve _curve = AnimationUtility.GetEditorCurve(_clip, obj);
                    Keyframe[] _keys = new Keyframe[_curve.keys.Length];
                    for (int i=0;i<_keys.Length;i++) {
                        _keys[i].time = _curve.keys[i].time;
                        _keys[i].value = _curve.keys[i].value;
                        _keys[i].inTangent = _curve.keys[i].inTangent;
                        _keys[i].outTangent = _curve.keys[i].outTangent;
                        _keys[i].inWeight = _curve.keys[i].inWeight;
                        _keys[i].outWeight = _curve.keys[i].outWeight;
                    }
                    _keys[_curve.keys.Length - 1].value = targetScript.UiScale;
                    _curve.keys = _keys;
                    AnimationUtility.SetEditorCurve(_clip,obj, _curve);
                }
                targetScript.UpdatePrefab();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Item Slots Scale:", GUILayout.Width(200));
            float _oldSlotScale = targetScript.InventorySlotScale;
            targetScript.InventorySlotScale = EditorGUILayout.Slider(targetScript.InventorySlotScale, 1F, 2F, GUILayout.Width(290));
            GUILayout.Label("x", GUILayout.Width(15));

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Empty Item Slot Background:", GUILayout.Width(200));
            targetScript.EmptyItemBackColor = EditorGUILayout.ColorField(targetScript.EmptyItemBackColor, GUILayout.Width(305));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Selected Item Outline:", GUILayout.Width(200));
            targetScript.ItemSelectedColor = EditorGUILayout.ColorField(targetScript.ItemSelectedColor, GUILayout.Width(305));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Mouse Hover Item Effect:", GUILayout.Width(200));
            targetScript.ItemHoverColor = EditorGUILayout.ColorField(targetScript.ItemHoverColor, GUILayout.Width(305));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Favorite Item Indicator:", GUILayout.Width(200));
            targetScript.FavoriteColor = EditorGUILayout.ColorField(targetScript.FavoriteColor, GUILayout.Width(305));
            GUILayout.EndHorizontal();

 

            if ( GUI.changed && !Application.isPlaying) targetScript.UpdatePrefab();
        }
    }
}
