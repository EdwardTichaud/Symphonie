using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    public class CurrenyInfo : MonoBehaviour
    {
        #region Variables
        public Image Icon;
        public Text ValueText;
        public Text ChangeText;
        public bool AutoCalculateChanges = true;
        public HintText HintScript;
        private int mIndex=0;
        private int value = -1;
        private int startValue = 0;
        private InventoryHolder Holder;
        private bool inited = false;
        #endregion

        #region Internal Methods
        private void Update()
        {
            if (!inited) return;
            if (value != Holder.GetCurrency(mIndex,false))
            {
                value = Holder.GetCurrency(mIndex,false);
                ValueText.text = value.ToString();
                ValueText.color = value < 0 ? Color.red : ItemManager.instance.currencies[mIndex].color;
            }
            if (ChangeText && AutoCalculateChanges) SetChangeText(value - startValue);
            
        }


        #endregion
        public void SetChangeText(int _changeValue)
        {
            if (_changeValue == 0)
                ChangeText.text = "";
            else
                ChangeText.text = (_changeValue >= 0 ? "<color=#469824>" : "<color=#BF3126>") + "(" + (_changeValue > 0 ? "+" : "") + _changeValue.ToString() + ")</color>";
        }


        public void Initialize(int _index, InventoryHolder _holder, bool _update=true)//_index=Currency ID, _update= if the value will auto update with the linked InventoryHolder.
        {
            mIndex = _index;
            Holder = _holder;
            Icon.sprite = ItemManager.instance.currencies[mIndex].icon;
            ValueText.color = ItemManager.instance.currencies[mIndex].color;
            if(HintScript) HintScript.HintString = ItemManager.instance.currencies[mIndex].name;
            SetStartValue();
            if(ChangeText) ChangeText.text = "";
            inited = _update;
        }

        public int GetCurrencyId()
        {
            return mIndex;
        }

        public int GetCurrencyValue()
        {
            return value;
        }

        public void SetStartValue()
        {
            startValue = Holder.GetCurrency(mIndex,false);
        } //This is useful if you want to compare the current value with the value by the point you call this method.

        public void SetValue(int _value)
        {
            ValueText.text = _value.ToString();
        }

        public void SetColor(Color _color)
        {
            ValueText.color = _color;
        }

        public void RevertColor()
        {
            ValueText.color = ItemManager.instance.currencies[mIndex].color;
        }
        

    }
}
