using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Threading.Tasks;

namespace SoftKitty
{
  
    public enum LoadMethod
    {
        DirectReference,
        Resources,
        Custom
    }

    /// <summary>
    /// Inherit from this class to create your loader script, and attach your loader script to the 'InventoryEngine' prefab to replace the original one.
    /// </summary>
    public class AssetLoader : MonoBehaviour
    {
        
        protected Dictionary<string, AssetBundle> LoadedBundle = new Dictionary<string, AssetBundle>();
        public Dictionary<string, Texture2D> LoadedIcon = new Dictionary<string, Texture2D>();
        public Dictionary<string, UnityEngine.Object> LoadedCustomAsset = new Dictionary<string, UnityEngine.Object>();


        


        /// <summary>
        /// Preload an AssetBundle, you could do it during the loading interface.
        /// </summary>
        /// <param name="_bundlePath"></param>
        public virtual void PreloadBundle(string _bundlePath)
        {
            var myLoadedAssetBundle = AssetBundle.LoadFromFile(Path.Combine(Application.streamingAssetsPath, _bundlePath));
            if (myLoadedAssetBundle == null)
            {
                Debug.Log("Failed to load AssetBundle: " + _bundlePath);
            }
            else
            {
                LoadedBundle.Add(_bundlePath, myLoadedAssetBundle);
            }
        }

        /// <summary>
        /// Release all loaded AssetBundle
        /// </summary>
        /// <param name="_unloadAllLoadedObjects"></param>
        public virtual void ReleaseAllBundles(bool _unloadAllLoadedObjects)
        {
            foreach (var key in LoadedBundle.Keys) {
                LoadedBundle[key].Unload(_unloadAllLoadedObjects);
            }
            LoadedBundle.Clear();
        }

        /// <summary>
        /// Release all loaded icons and custom assets from the memory.
        /// </summary>
        public virtual void ReleaseAllLoadedAsset()
        {
            LoadedIcon.Clear();
            LoadedCustomAsset.Clear();
        }

        /// <summary>
        /// When 'ReleaseLoadedAsset()' is called from an item, this method will be called with the unloading asset 
        /// You can override this method to handle it.
        /// </summary>
        /// <param name="_path"></param>
        public virtual void ReleaseAsset(string _path)
        {
             //Your code to deal with release an asset
        }

        /// <summary>
        /// Load an Asset from AssetBundle or your custom loading method. Loaded bundle will be add to a Dictionary.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="_path"></param>
        /// <returns></returns>
        public virtual T Load<T>(string _path) where T : UnityEngine.Object
        {
            //===============Example code of load from Addressables====================
            //var op = Addressables.LoadAssetAsync<T>(_path);
            //T go = op.WaitForCompletion();
            //return go;

            //Example code of load from AssetBundle, in this example, we separate bundle path and object path with "#",
            //for example, a object with path "icons/food" stored in a bundle file called "IconData", we will fill in its path as "IconData#icons/food";
            string[] _args = _path.Split("#");
            if (_args.Length >= 2)
            {
                AssetBundle myLoadedAssetBundle=null;
                if (LoadedBundle.ContainsKey(_args[0]))
                {
                    myLoadedAssetBundle = LoadedBundle[_args[0]];
                }
                else
                {
                    myLoadedAssetBundle = AssetBundle.LoadFromFile(Path.Combine(Application.streamingAssetsPath, _args[0]));
                    if (myLoadedAssetBundle == null)
                    {
                        Debug.Log("Failed to load AssetBundle: " + _args[0]);
                        return null;
                    }
                    else
                    {
                        LoadedBundle.Add(_args[0], myLoadedAssetBundle);
                    }
                }
               
                var go = myLoadedAssetBundle.LoadAsset<T>(_args[1]);
                return go;
            }
            else
            {
                Debug.Log("The path must be in this format: BundleName#ObjectPath,  for example: IconData#icons/food" + _args[0]);
                return null;
            }
        }

        /// <summary>
        /// Load and Instantiate an Asset from AssetBundle or your custom loading method. Loaded bundle will be add to a Dictionary.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="_path"></param>
        /// <returns></returns>
        public virtual T LoadAndInstantiate<T>(string _path) where T : UnityEngine.Object
        {
            //============Example code of load from Addressables=============
            //var _handle = Addressables.LoadAssetAsync<T>(_path);
            //T _ref = _handle.WaitForCompletion();
            //if (_ref != null)
            //{
            //    var op = Addressables.InstantiateAsync(_path);
            //    T newObj = op.Result;
            //    Addressables.Release(_handle);
            //    //Make sure to call  Addressables.Release(newObj); when this object is about to destroied!!
            //    return newObj;
            //}
            //else
            //{
            //    return null;
            //}

            string[] _args = _path.Split("#");
            if (_args.Length >= 2)
            {
                AssetBundle myLoadedAssetBundle = null;
                if (LoadedBundle.ContainsKey(_args[0]))
                {
                    myLoadedAssetBundle = LoadedBundle[_args[0]];
                }
                else
                {
                    myLoadedAssetBundle = AssetBundle.LoadFromFile(Path.Combine(Application.streamingAssetsPath, _args[0]));
                    if (myLoadedAssetBundle == null)
                    {
                        Debug.Log("Failed to load AssetBundle: " + _args[0]);
                        return null;
                    }
                    else
                    {
                        LoadedBundle.Add(_args[0], myLoadedAssetBundle);
                    }
                }
                var go = myLoadedAssetBundle.LoadAsset<T>(_args[1]);
                return Instantiate(go);
            }
            else
            {
                Debug.Log("The path must be in this format: BundleName#ObjectPath,  for example: IconData#icons/food" + _args[0]);
                return null;
            }

        }
    }
}
