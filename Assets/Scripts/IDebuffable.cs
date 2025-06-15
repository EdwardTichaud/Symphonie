using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public interface IDebuffable
{
    public void ApplyDebuff(int value);
    public void RemoveDebuff(int value);
}