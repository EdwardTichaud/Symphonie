using UnityEngine;
using UnityEditor;

namespace SoftKitty
{
    public class MeshPreview : EditorWindow
    {
        Editor gameObjectEditor;
        static GameObject gameObject;
        public static MeshRenderer renderer;
        public static MeshPreview instance;
        public static void ShowPreview(string _meshPath, string _matPath, string _path)
        {
            
            gameObject = (GameObject)AssetDatabase.LoadAssetAtPath(_path, typeof(GameObject));
            MeshFilter _mf = gameObject.GetComponentInChildren<MeshFilter>();
            _mf.mesh = Instantiate(Resources.Load<Mesh>(_meshPath));
            renderer = gameObject.GetComponentInChildren<MeshRenderer>();
            Material _mat = Instantiate(Resources.Load<Material>(_matPath)); ;
            renderer.material = _mat;
            renderer.sharedMaterial = _mat;
            instance=GetWindowWithRect<MeshPreview>(new Rect(0, 0, 256, 256));
        }

        private void OnDestroy()
        {
            renderer = null;
        }

        void OnGUI()
        {
            GUIStyle bgColor = new GUIStyle();

            if (gameObjectEditor == null)
            {
                gameObjectEditor = Editor.CreateEditor(gameObject);
            }

            gameObjectEditor.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(256, 256), bgColor);

        }

    }
}
