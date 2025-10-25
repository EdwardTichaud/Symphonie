using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace SoftKitty
{

    [CustomEditor(typeof(AssetLoader))]
    public class AssetLoader_Inspector : Editor
    {
        Color _activeColor = new Color(0.1F, 0.3F, 0.5F);
        Color _disableColor = new Color(0F, 0.1F, 0.3F);
        GUIStyle _titleButtonStyle;
        private bool _expand1 = false;
        private bool _expand2 = false;
        private bool _expand3 = false;
        private bool _expand4 = false;
        private bool _expand5 = false;
        private bool _expand6 = false;
        public override void OnInspectorGUI()
        {
            _titleButtonStyle = new GUIStyle(GUI.skin.button);
            _titleButtonStyle.alignment = TextAnchor.MiddleLeft;
 

            GUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("You can inherit from this class to create your loader script, and use your loader script to replace this one.", MessageType.Info,true);
            GUILayout.EndHorizontal();

            TitleExpand(ref _expand1, "PreloadBundle(string _bundlePath)");
            if (_expand1) {
                GUILayout.BeginHorizontal();
                GUILayout.Space(50);
                EditorGUILayout.HelpBox("Preload an AssetBundle, for example you can pack icons for frequently used items,\n" +
                    "and preload it in the loading interface.", MessageType.Info, true);
                GUILayout.EndHorizontal();
            }

            TitleExpand(ref _expand2, "ReleaseAllBundles(bool _unloadAllLoadedObjects)");
            if (_expand2)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(50);
                EditorGUILayout.HelpBox("Unload all loaded AssetBundles, to free the memory", MessageType.Info, true);
                GUILayout.EndHorizontal();
            }

            TitleExpand(ref _expand3, "ReleaseAsset(string _path)");
            if (_expand3)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(50);
                EditorGUILayout.HelpBox("The loaded icons and custom objects will keep in the memory for faster access later,\n" +
                    "but you could release specified asset by its path to free the memory.", MessageType.Info, true);
                GUILayout.EndHorizontal();
            }

            TitleExpand(ref _expand4, "ReleaseAllLoadedAsset()");
            if (_expand4)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(50);
                EditorGUILayout.HelpBox("The loaded icons and custom objects will keep in the memory for faster access later,\n" +
                    "but you could release all assets to free the memory. It is recommended to do it when scene switch.", MessageType.Info, true);
                GUILayout.EndHorizontal();
            }

            TitleExpand(ref _expand5, "Using Unity Addressable","Help");
            if (_expand5)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(50);
                EditorGUILayout.HelpBox("You can find comment out code for Addressable in the script,\n" +
                    "inherit from this class to create your loader script, override the load methods and use those comment out code to replace the origial code.", MessageType.Info, true);
                GUILayout.EndHorizontal();
            }

            base.OnInspectorGUI();
        }


        private void TitleExpand(ref bool _value, string _text, string _title="Api")
        {
            GUILayout.BeginHorizontal();
            
            _titleButtonStyle.normal.textColor = _value ? Color.white : new Color(0.65F, 0.65F, 0.65F);
            GUI.color = Color.white;
            GUILayout.Label(_value ? "[-]" : "[+]", GUILayout.Width(20));
            GUI.backgroundColor = _title == "Api"? Color.green:Color.yellow;
            GUILayout.Box(_title, _titleButtonStyle, GUILayout.Width(40));
            GUI.backgroundColor = _value ? _activeColor : _disableColor;
            if (GUILayout.Button(_text, _titleButtonStyle))
            {
                _value = !_value;
                EditorGUI.FocusTextInControl(null);
            }
            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }
    }
}
