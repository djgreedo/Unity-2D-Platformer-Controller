using System;
using PC2D;
using UnityEngine;
using UnityEngine.Assertions;

// TODO AllowState, ask all plugin if it's possible to change the state
// useful to deny jumping while X
// TODO move Dash to plugin
// TODO move Jump to plugin

/// <summary>
/// Plugin to allow motor to Grab other motors, the other motor should no be
/// mobable just react to the enviroment like a box
/// </summary>
[RequireComponent(typeof(PlatformerMotor2D))]
public class PlatformerMotor2DPlugin : MonoBehaviour
{
    public enum Action {
      Modified, // modified, continue
      Continue, // not modified, continue
      Handled, // modified stop
      Stop // not modified, but stop
    };

    virtual public Action GetCurrentVelocity(Vector3 currentVelocity, out Vector3 velocity)
    {
        velocity = Vector3.zero;
        return Action.Continue;
    }

    virtual public void OnEnable()
    {
        _motor = GetComponent<PlatformerMotor2D>();
        _motor.plugins.Add(this);
    }

    virtual public void OnDisable()
    {
        _motor.plugins.Remove(this);
        _motor = null;
    }

    protected PlatformerMotor2D _motor;
}
