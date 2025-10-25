using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach this script to a ui element, when mouse hover it, a small floating panel with hint text will show up
/// </summary>
namespace SoftKitty
{
    public class HintText : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        #region Variables
        public string HintString = "";
        private bool isHover = false;
        #endregion


        #region Internal Methods
        public void OnPointerEnter(PointerEventData eventData)
        {
            isHover = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHover = false;
        }

        private void OnDisable()
        {
            isHover = false;
        }

        public bool GetHover()
        {
            return isHover;
        }

        void Update()
        {
            if (isHover)
            {
                if (HintString != "") HintManager.ShowHint(HintString,this);
            }
        }
        #endregion
    }
}
