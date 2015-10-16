using System;
using PC2D;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Plugin to allow motor to Grab other motors, the other motor should no be
/// mobable just react to the enviroment like a box
/// </summary>
[RequireComponent(typeof(PlatformerMotor2D))]
public class PlatformerMotor2DPlugin : MonoBehaviour
{
    virtual public void GetCurrentVelocity(out Vector3 velocity, out bool handled)
    {
        handled = false;
        velocity = Vector3.zero;
    }

    virtual public void OnEnable()
    {
        _motor = GetComponent<PlatformerMotor2D>();
        _motor.plugins.Add(this);
    }

    virtual public void OnDisable()
    {
        _motor.plugins.Remove(this);
    }

    protected PlatformerMotor2D _motor;
}
