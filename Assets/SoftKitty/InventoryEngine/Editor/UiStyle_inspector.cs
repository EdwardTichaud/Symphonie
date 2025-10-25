using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace SoftKitty.InventoryEngine
{
    [CustomEditor(typeof(UiStyle))]
    public class UiStyle_inspector : Editor
    {
        bool _colorExpand = false;
        bool _rectExpand = false;

        Color _activeColor = new Color(0.1F, 0.3F, 0.5F);
        Color _disableColor = new Color(0F, 0.1F, 0.3F);
        Color _titleColor = new Color(0.5F, 0.7F, 1F);
        Color _buttonColor = new Color(0F, 0.8F, 0.3F);
        GUIStyle _titleButtonStyle;

        public override void OnInspectorGUI()
        {
            GUI.changed = false;
            bool _valueChanged = false;
            _titleButtonStyle = new GUIStyle(GUI.skin.button);
            _titleButtonStyle.alignment = TextAnchor.MiddleLeft;
            Color _backgroundColor = GUI.backgroundColor;
            var script = MonoScript.FromScriptableObject(this);
            UiStyle myTarget = (UiStyle)target;

            string _thePath = AssetDatabase.GetAssetPath(script);
            _thePath = _thePath.Replace("UiStyle_inspector.cs", "");
            Texture logoIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "Logo.png", typeof(Texture));
            Texture warningIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "warning.png", typeof(Texture));
            GUILayout.BeginHorizontal();
            GUILayout.Box(logoIcon);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.color = _titleColor;
            GUILayout.Label("Apply Style Settings", GUILayout.Width(120));
            GUI.color = Color.white;
            myTarget.ApplyStyle = GUILayout.Toggle(myTarget.ApplyStyle,"", GUILayout.Width(30));
           
            GUILayout.EndHorizontal();

            if (myTarget.ApplyStyle )
            {

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _colorExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _colorExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUILayout.Label(_colorExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(" Color Setting (" + myTarget.References.Length.ToString() + ")", _titleButtonStyle))
                {
                    _colorExpand = !_colorExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUI.backgroundColor = _backgroundColor;
                GUILayout.EndHorizontal();
                if (_colorExpand)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = _buttonColor;
                    if (GUILayout.Button("Reset Default", GUILayout.Width(200)))
                    {
                        for (int i = 0; i < myTarget.References.Length; i++)
                        {
                            myTarget.SetColor(i, myTarget.References[i].defaultColor);
                            if (myTarget.References[i].visibleAdjustable) myTarget.SetVisible(i, true);
                        }
                        _valueChanged = true;
                    }
                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.EndHorizontal();

                    for (int i = 0; i < myTarget.References.Length; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        Color _color = EditorGUILayout.ColorField(myTarget.References[i].color, GUILayout.Width(50));
                        if (_color != myTarget.References[i].color)
                        {
                            myTarget.SetColor(i, _color);
                            _valueChanged = true;
                        }
                        GUI.color = _titleColor;
                        GUILayout.Label(myTarget.References[i].name, GUILayout.Width(200));
                        GUI.color = Color.white;
                        if (myTarget.References[i].visibleAdjustable)
                        {
                            bool _visible = GUILayout.Toggle(myTarget.References[i].visible, "Visible", GUILayout.Width(100));
                            if (_visible != myTarget.References[i].visible)
                            {
                                myTarget.SetVisible(i, _visible);
                                _valueChanged = true;
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }


                GUILayout.BeginHorizontal();
                GUI.backgroundColor = _rectExpand ? _activeColor : _disableColor;
                _titleButtonStyle.normal.textColor = _rectExpand ? Color.white : new Color(0.65F, 0.65F, 0.65F);
                GUILayout.Label(_rectExpand ? "[-]" : "[+]", GUILayout.Width(20));
                if (GUILayout.Button(" Size Setting (" + myTarget.Rects.Length.ToString() + ")", _titleButtonStyle))
                {
                    _rectExpand = !_rectExpand;
                    EditorGUI.FocusTextInControl(null);
                }
                GUI.backgroundColor = _backgroundColor;
                GUILayout.EndHorizontal();
                if (_rectExpand)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = _buttonColor;
                    if (GUILayout.Button("Reset Default", GUILayout.Width(200)))
                    {
                        for (int i = 0; i < myTarget.Rects.Length; i++)
                        {
                            myTarget.SetWidth(i, 0.5F);
                            myTarget.SetHeight(i, 0.5F);
                        }
                        _valueChanged = true;
                    }
                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.EndHorizontal();

                    for (int i = 0; i < myTarget.Rects.Length; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUI.color = _titleColor;
                        GUILayout.Label(myTarget.Rects[i].name, GUILayout.Width(200));
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();

                        if (myTarget.Rects[i].widthAdjustable)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(50);
                            GUILayout.Label("Width: ", GUILayout.Width(70));
                            float _width = GUILayout.HorizontalSlider(myTarget.Rects[i].widthLerp, 0F, 1F, GUILayout.Width(250));
                            if (_width != myTarget.Rects[i].widthLerp)
                            {
                                myTarget.SetWidth(i, _width);
                                _valueChanged = true;
                            }
                            GUILayout.Label(Mathf.FloorToInt((_width + 0.5F) * 100F).ToString() + "%", GUILayout.Width(50));
                            GUILayout.EndHorizontal();
                        }

                        if (myTarget.Rects[i].heightAdjustable)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(50);
                            GUILayout.Label("Height: ", GUILayout.Width(70));
                            float _height = GUILayout.HorizontalSlider(myTarget.Rects[i].heightLerp, 0F, 1F, GUILayout.Width(250));
                            if (_height != myTarget.Rects[i].heightLerp)
                            {
                                myTarget.SetHeight(i, _height);
                                _valueChanged = true;
                            }
                            GUILayout.Label(Mathf.FloorToInt((_height + 0.5F) * 100F).ToString() + "%", GUILayout.Width(50));
                            GUILayout.EndHorizontal();
                        }
                    }
                }
            }

            GUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("Please don't modify the following settings unless you know what they do.", MessageType.Info);
            GUILayout.EndHorizontal();

            base.OnInspectorGUI();

            if ((_valueChanged || GUI.changed) && !Application.isPlaying) myTarget.UpdatePrefab();
        }
    }
}
