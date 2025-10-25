using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace SoftKitty.InventoryEngine
{
    public class InventoryUi : ItemContainer
    {
        #region Variables
        public CurrenyInfo CurrencyPrefab;
        public Text WeightText;
        public Image WeightBar;
        public Transform MiniModeIcon;
        private float weight = -1F;
        private float maxWeight = -1F;
        private bool miniMode = false;
        private float fullHeight = 700F;
        #endregion

        #region MonoBehaviour
        public override void Update()
        {
            if (!inited) return;
            base.Update();
            UpdateWeight();
        }
        #endregion 

        public override void Initialize(InventoryHolder _inventoryHolder, InventoryHolder _equipHolder, string _name = "Inventory")//Initialize this interface
        {
            base.Initialize(_inventoryHolder, _equipHolder, _name);
            fullHeight = GetComponent<RectTransform>().sizeDelta.y;
            Holder.CalWeight();
            for (int i=0;i< Holder.Currency.Count;i++) {
                GameObject _newItem = Instantiate(CurrencyPrefab.gameObject, CurrencyPrefab.transform.parent);
                _newItem.transform.localScale = Vector3.one;
                _newItem.SetActive(true);
                _newItem.GetComponent<CurrenyInfo>().Initialize(i, Holder);
            }
            inited = true;
        }

        public void ExpandSwitch()//Toggle between the mini window and normal window
        {
            SoundManager.Play2D("MenuOff");
            miniMode = !miniMode;
            GetComponent<RectTransform>().sizeDelta = new Vector2(GetComponent<RectTransform>().sizeDelta.x, miniMode?450F:fullHeight);
            MiniModeIcon.localEulerAngles = new Vector3(0F,0F,miniMode?180F:0F);
        }
        
        private void UpdateWeight()//Update the total weight value of all items.
        {
            if (weight != Holder.GetWeight() || maxWeight != Holder.MaxiumCarryWeight)
            {
                weight = Holder.GetWeight();
                maxWeight = Holder.MaxiumCarryWeight;
                WeightText.text = weight.ToString("0.0") + "/" + maxWeight.ToString("0.0");
                float _weightPercentage =Mathf.Clamp01( weight / maxWeight);
                WeightText.color = new Color(0.8F, 0.8F - Mathf.Max(0F, _weightPercentage * 0.8F - 0.6F)*4F, 0.8F - Mathf.Max(0F, _weightPercentage * 0.8F - 0.4F)*2F);
                WeightBar.transform.localScale = new Vector3(_weightPercentage, 1F, 1F);
            }
        }

    }
}
