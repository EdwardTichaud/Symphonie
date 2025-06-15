using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public interface IBuffable
{
    public void ApplyBuff(int value);
    public void RemoveBuff(int value);
}