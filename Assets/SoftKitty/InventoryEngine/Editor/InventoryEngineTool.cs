using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    public class InventoryEngineTool : EditorWindow
    {

        private List<GameObject> _textComponents = new List<GameObject>();
        private UiStyle _styleScript;
        private bool _fold = false;
        private TMPro.TMP_FontAsset _font; // If you're not using TMP, and encounter console error point to this line, please remove this whole script.
        private Vector2 _scroll;

        void OnGUI()
        {
            GUIStyle box = GUI.skin.button;
            GUIStyle header = new GUIStyle();
            header.fontSize = 20;
            header.alignment = TextAnchor.MiddleCenter;

            GUILayout.BeginVertical();


            GUILayout.BeginHorizontal();
            GUI.color = Color.green;
            GUILayout.Label("TextMeshPro Converter");
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            if (_styleScript == null && (Selection.activeGameObject == null || !Selection.activeGameObject.GetComponent<UiStyle>()))
            {
                GUI.color = Color.red;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Please select the UI window object.");
                GUILayout.EndHorizontal();
                GUI.color = Color.white;
            }
            else
            {
                if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<UiStyle>())
                {
                    GUILayout.BeginHorizontal();
                    GUI.color = Color.green;
                    if (GUILayout.Button("Find all Texts (" + Selection.activeGameObject.name + ")"))
                    {
                        Text[] _texts = Selection.activeGameObject.GetComponentsInChildren<Text>();
                        _styleScript = Selection.activeGameObject.GetComponent<UiStyle>();
                        _textComponents.Clear();
                        foreach (Text obj in _texts)
                        {
                            _textComponents.Add(obj.gameObject);
                        }
                    }
                    GUILayout.Space(60);
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }

                if (_styleScript != null)
                {
                    GUILayout.BeginHorizontal();
                    _fold = EditorGUILayout.Foldout(_fold, "Text List <" + _styleScript.gameObject.name + "> (" + _textComponents.Count + "):");
                    if (GUILayout.Button("Clear List"))
                    {
                        _textComponents.Clear();
                    }
                    GUILayout.Space(60);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    if (_textComponents.Count > 0)
                    {
                        GUI.color = _font != null ? Color.yellow : Color.gray;
                        if (GUILayout.Button("Replace all texts with TMP"))
                        {
                            if (_font != null)
                            {
                                foreach (GameObject obj in _textComponents)
                                {
                                    if (obj != null)
                                    {
                                        if (obj.GetComponent<Text>())
                                        {
                                            List<Vector2> _pos = new List<Vector2>();
                                            if (_styleScript != null)
                                            {
                                                for (int x = 0; x < _styleScript.References.Length; x++)
                                                {
                                                    for (int y = 0; y < _styleScript.References[x].graphics.Length; y++)
                                                    {
                                                        if (_styleScript.References[x].graphics[y] == obj.GetComponent<MaskableGraphic>())
                                                        {
                                                            _pos.Add(new Vector2(x, y));
                                                        }
                                                    }
                                                }
                                            }
                                            string _text = obj.GetComponent<Text>().text;
                                            Color _color = obj.GetComponent<Text>().color;
                                            int _size = obj.GetComponent<Text>().fontSize;
                                            DestroyImmediate(obj.GetComponent<Text>());
                                            TMPro.TextMeshPro _tmp = obj.AddComponent<TMPro.TextMeshPro>();
                                            _tmp.text = _text;
                                            _tmp.color = _color;
                                            _tmp.fontSize = _size;
                                            _tmp.font = _font;
                                            foreach (var p in _pos)
                                            {
                                                _styleScript.References[Mathf.FloorToInt(p.x)].graphics[Mathf.FloorToInt(p.y)] = obj.GetComponent<MaskableGraphic>();
                                            }
                                            if (obj.GetComponent<Outline>()) DestroyImmediate(obj.GetComponent<Outline>());
                                            if (obj.GetComponent<Shadow>()) DestroyImmediate(obj.GetComponent<Shadow>());
                                        }
                                    }
                                }
                            }
                            else
                            {
                                EditorUtility.DisplayDialog("Warning", "Please assign the TMP Font Asset first.", "OK");
                            }
                        }
                        GUILayout.Space(10);
                        GUI.color = Color.white;
                        _font = (TMPro.TMP_FontAsset)EditorGUILayout.ObjectField(_font, typeof(TMPro.TMP_FontAsset), false);
                    }
                    GUILayout.Space(60);
                    GUILayout.EndHorizontal();



                    if (_fold)
                    {
                        _scroll = GUILayout.BeginScrollView(_scroll);
                        foreach (GameObject obj in _textComponents)
                        {
                            if (obj != null)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                GUI.color = obj.gameObject.GetComponent<TMPro.TMP_Text>() == null ? Color.gray : Color.green;
                                if (GUILayout.Button(obj.transform.parent.name + "/" + obj.gameObject.name + (obj.gameObject.GetComponent<TMPro.TMP_Text>() == null ? " <Text>" : " <TMP>")))
                                {
                                    Selection.activeGameObject = obj.gameObject;
                                }
                                GUI.color = Color.white;
                                GUILayout.Space(50);
                                GUILayout.EndHorizontal();
                            }
                            else
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Space(30);
                                GUI.color = Color.red;
                                GUILayout.Label("Missing");
                                GUI.color = Color.white;
                                GUILayout.Space(50);
                                GUILayout.EndHorizontal();
                            }
                        }
                        GUILayout.EndScrollView();
                    }
                }
            }
            GUILayout.EndVertical();
        }




    }
}
