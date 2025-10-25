using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    /// <summary>
    /// This module displays popup messages and large flashy icons of items when players acquire new items.
    /// </summary>
    public class DynamicMsg : MonoBehaviour
    {
        #region Variables
        private static DynamicMsg instance;
        public GameObject MsgPrefab;
        public GameObject ItemPrefab;
        private List<CanvasGroup> MsgList = new List<CanvasGroup>();
        private float _alpha = 1F;
        #endregion

        #region Internal Methods
        public void ShowMsg(string _text)
        {
            GameObject newObj = Instantiate(MsgPrefab, MsgPrefab.transform.parent);
            newObj.transform.localScale = Vector3.one;
            newObj.GetComponentInChildren<Text>().text = _text;
            MsgList.Add(newObj.GetComponent<CanvasGroup>());
            newObj.SetActive(true);
            transform.SetAsLastSibling();
            SoundManager.Play2D("msg");
        }

        public void ShowItem(Item _item, int _number)
        {
            if (_item == null) return;
            GameObject newObj = Instantiate(ItemPrefab, ItemPrefab.transform.parent);
            newObj.transform.localScale = Vector3.one;
            newObj.GetComponent<ItemIcon>().SetAppearance(_item.Copy(), true, true);
            newObj.GetComponent<ItemIcon>().SetItemNumber(_number);
            newObj.GetComponent<ItemIcon>().SetUpgradeLevel(_item.upgradeLevel);
            newObj.SetActive(true);
            transform.SetAsLastSibling();
            SoundManager.Play2D("reward");
        }

        void Update()
        {
            _alpha = 1F;
            for (int i = MsgList.Count - 1; i >= 0; i--)
            {
                if (MsgList[i] == null)
                {
                    MsgList.RemoveAt(i);
                }
                else
                {
                    MsgList[i].alpha = _alpha;
                    _alpha -= 0.2F;
                }
            }
        }
        
        private static void CreateInstance()
        {
            GameObject newObj = Instantiate(Resources.Load<GameObject>("InventoryEngine/DynamicMsg"), WindowsManager.GetMainCanvas().transform);
            newObj.transform.SetAsLastSibling();
            newObj.transform.localPosition = Vector3.zero;
            newObj.transform.localScale = Vector3.one;
            instance = newObj.GetComponent<DynamicMsg>();
        }
        #endregion

        /// <summary>
        /// Displays a message with the provided text.
        /// </summary>
        /// <param name="_text"></param>
        public static void PopMsg(string _text) 
        {
            if (instance == null)CreateInstance();
            instance.ShowMsg(_text);
        }

        /// <summary>
        /// Shows a flashy big icon of an item.
        /// </summary>
        /// <param name="_item"></param>
        /// <param name="_number"></param>
        public static void PopItem(Item _item, int _number=1)
        {
            if (instance == null) CreateInstance();
            instance.ShowItem(_item, _number);
        }

        

        
    }
}
