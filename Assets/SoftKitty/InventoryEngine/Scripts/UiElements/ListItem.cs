using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    public class ListItem : MonoBehaviour
    {

        #region Variables
        public Image[] mImages;
        public RawImage[] mRawimages;
        public Text[] mTexts;
        public Button[] mButtons;
        public GameObject[] mObjects;
        public CanvasGroup[] mCanvas;
        public string mEvent;
        public GameObject mEventTarget;
        public ListItem[] SubItems;
        public int mID;
        public float mValue;
        #endregion

        #region Internal Methods
        public void OnClickWithID()
        {
            mEventTarget.SendMessage(mEvent,mID, SendMessageOptions.DontRequireReceiver);
        }
        public void OnClick()
        {
            mEventTarget.SendMessage(mEvent,SendMessageOptions.DontRequireReceiver);
        }
        #endregion
    }
}
