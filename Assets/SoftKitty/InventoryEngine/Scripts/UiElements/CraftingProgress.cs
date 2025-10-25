using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    public class CraftingProgress : MonoBehaviour
    {
        [Header("Assign the InventoryHolder")]
        public InventoryHolder Holder;

        #region Variables
        [Header("References")]
        public RectTransform Progress;
        public Text NumberText;
        public ItemIcon ResultItem;
        public AdvAnimation ResultPop;
        public CanvasGroup Root;
        private float FadeTime = 0F;
        #endregion


        #region Internal Method

        void Update()
        {
            if (Holder.isCrafting())
            {
                if (FadeTime < 1F)
                {
                    FadeTime = 1F;
                    Root.alpha = FadeTime;
                    Root.gameObject.SetActive(true);
                    if(ItemManager.TryGetItem(Holder.GetCraftingItemId())!=null) ResultItem.SetAppearance(ItemManager.TryGetItem(Holder.GetCraftingItemId()), true, false);
                    ResultItem.SetItemId(Holder.GetCraftingItemId());
                }
                Progress.localScale = new Vector3(Holder.GetCraftingProgress(),1F,1F);
                if (Holder.GetCraftingProgress() >= 1F)
                {
                    if (Holder.GetCraftingFailed())
                    {
                        NumberText.text = "Failed!";
                    }
                    else
                    {
                        ResultPop.Stop();
                        ResultPop.Play();
                    }
                }
                else
                {
                    NumberText.text = Holder.GetCraftedItemNumber().ToString() + "/" + Holder.GetCraftingItemNumber().ToString();
                }

            }
            else
            {
                FadeTime = Mathf.MoveTowards(FadeTime,0F,Time.unscaledDeltaTime);
                if (FadeTime > 0F)
                {
                    NumberText.text = "Done!";
                    Root.alpha = FadeTime;
                }
                else if (Root.gameObject.activeSelf)
                {
                    Root.gameObject.SetActive(false);
                }
            }
        }
        #endregion
    }
}
