using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    public class NumberPanel : MonoBehaviour
    {
        #region Variables
        private int MaxiumValue;
        private float ButtonHoldTime = 0F;
        private int Number = 0;

        public InputField InputNumber;
        public HoverEffect ButtonL;
        public HoverEffect ButtonR;
        float increment = 0.2F;
        [HideInInspector]
        public bool Enabled = true;
        #endregion

        #region Internal Methods
        private void Update()
        {
            if (!Enabled) return;

            if (int.Parse(InputNumber.text) != Number)
            {
                Number = Mathf.Clamp(int.Parse(InputNumber.text), 1, MaxiumValue);
                InputNumber.text = Number.ToString();
            }

            if (ButtonL.isHover)
            {
                if (InputProxy.GetMouseButtonDown(0))
                {
                    AddNum(-1);
                    ButtonHoldTime = 0F;
                }
                else if (InputProxy.GetMouseButton(0))
                {
                    ButtonHoldTime += Time.unscaledDeltaTime;
                    increment = Mathf.MoveTowards(increment, 0.05F, Time.unscaledDeltaTime * 0.1F);
                    if (ButtonHoldTime > increment)
                    {
                        AddNum(-1);
                        ButtonHoldTime = 0F;
                    }
                }
                else
                {
                    ButtonHoldTime = 0F;
                    increment = 0.2F;
                }
            }

            if (ButtonR.isHover)
            {
                if (InputProxy.GetMouseButtonDown(0))
                {
                    AddNum(1);
                    ButtonHoldTime = 0F;
                }
                else if (InputProxy.GetMouseButton(0))
                {
                    ButtonHoldTime += Time.unscaledDeltaTime;
                    increment = Mathf.MoveTowards(increment, 0.05F, Time.unscaledDeltaTime * 0.1F);
                    if (ButtonHoldTime > increment)
                    {
                        AddNum(1);
                        ButtonHoldTime = 0F;
                    }
                }
                else
                {
                    ButtonHoldTime = 0F;
                    increment = 0.2F;
                }
            }

        }
        #endregion
        public void Initialize(int _startValue, int _maxiumValue)//Initialize by default value and maximum value
        {
            Number = _startValue;
            InputNumber.text = Number.ToString();
            MaxiumValue = _maxiumValue;
        }

        public int GetNumber()//Get the number from user input.
        {
            return Number;
        }

        public void AddNum(int _add)//Add number by _add, it can be postive or negetive
        {
            Number = Mathf.Clamp(Number + _add, 1, MaxiumValue);
            InputNumber.text = Number.ToString();
        }

        public void SetNum(int _num)
        {
            Number = Mathf.Clamp(_num, 1, MaxiumValue);
            InputNumber.text = Number.ToString();
        }


        
    }
}
