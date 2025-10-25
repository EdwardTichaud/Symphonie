using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    /// <summary>
    /// This module prompts the player to input numbers. For example, when a player tries to split an item stack, this module can be used to ask how many items they want to split.
    /// </summary>
    public class NumberInput : MonoBehaviour
    {
        #region Variables
        public static NumberInput instance;
        public delegate void NumberCallback(int _result);
        private NumberCallback Callback;
        public RectTransform Root;
        public NumberPanel NumberPanel;
        #endregion

        #region Internal Methods
        public void ShowInput(int _startValue, int _maxiumValue, RectTransform _rect, NumberCallback _callback)
        {
            Callback = _callback;
            Root.position = _rect.position;
            NumberPanel.Initialize(_startValue, _maxiumValue);
            Root.gameObject.SetActive(true);
        }

        public void Confirm()
        {
            Callback(NumberPanel.GetNumber());
            Destroy(gameObject);
        }

        public void Cancel()
        {
            Callback(-1);
            Destroy(gameObject);
        }

        private void Update()
        {
            if (InputProxy.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
            }
        }
        #endregion


        /// <summary>
        /// Return if this interface is visible
        /// </summary>
        /// <returns></returns>
        public static bool isVisible()
        {
            return instance != null;
        }

        /// <summary>
        /// Displays a panel prompting the player to input a number. After the player confirms, the _callback is called with the resulting number.
        /// </summary>
        /// <param name="_startValue"></param>
        /// <param name="_maxiumValue"></param>
        /// <param name="_rect"></param>
        /// <param name="_callback"></param>
        public static void GetNumber(int _startValue,int _maxiumValue,RectTransform _rect ,NumberCallback _callback)
        {
            if (instance == null)
            {
                GameObject newObj = Instantiate(Resources.Load<GameObject>("InventoryEngine/NumberInput"), _rect.GetComponentInParent<UiWindow>().transform);
                newObj.transform.SetAsLastSibling();
                newObj.transform.localPosition = Vector3.zero;
                newObj.transform.localScale = Vector3.one;
                instance = newObj.GetComponent<NumberInput>();
            }
            instance.ShowInput(_startValue, _maxiumValue, _rect, _callback);
        }

       

    }
}
