using System;
using PC2D;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Plugin to allow motor to Grab other motors, the other motor should no be
/// mobable just react to the enviroment like a box
/// </summary>
[RequireComponent(typeof(PlatformerMotor2D))]
public class PlatformerMotor2DGrab : MonoBehaviour
{
    #region Public

    // init
    void Start()
    {
        _motor = GetComponent<PlatformerMotor2D>();
        onEnable();
    }

    void onEnable()
    {
        _motor.onAfterUpdateMotor += UpdateGrab;
    }

    void onDisable()
    {
        _motor.onAfterUpdateMotor -= UpdateGrab;
        if (IsGrabing())
        {
            Drop();
        }
    }

    ///<sumary>
    /// Given a game object return if this motor consider the object as grabbable.
    /// object should not be falling, and in the same platform or grounded.
    ///</sumary>
    public bool IsGrabable(GameObject obj)
    {
        //maybe: ((0x1 << obj.layer) & grabbableLayerMask) != 0;

        PlatformerMotor2D target = obj.GetComponent<PlatformerMotor2D>();
        if (target)
        {
            return !target.IsFalling() && !target.IsFallingFast() && target.IsOnPlatform() == _motor.IsOnPlatform();
        }
        return false;
    }

    ///<sumary>
    /// Is there something grabbed by the motor?
    ///</sumary>
    public bool IsGrabing()
    {
        return _grabObject != null;
    }
    ///<sumary>
    /// Grab an object, the object should be grabbable
    ///</sumary>
    public void Grab(GameObject obj)
    {
        if (IsGrabing())
        {
            Drop();
        }

        _grabObject = obj;
        _grabMotor = obj.GetComponent<PlatformerMotor2D>();
        previousPos = transform.position;
        _grabPreviousLayer = (int) obj.layer;
        obj.layer = (int) Mathf.Log((float) _motor.ignoreRaycastsLayerMask, 2);
    }
    ///<sumary>
    /// Drop grabbed object
    ///</sumary>
    public void Drop()
    {
        _grabObject.layer = _grabPreviousLayer;
        _grabPreviousLayer = 0;
        _grabObject = null;
        _grabMotor = null;
        _motor.CheckSurroundingsAndUpdate(true);
    }

    #endregion

    #region Private

    private PlatformerMotor2D _motor;
    private GameObject _grabObject;
    private PlatformerMotor2D _grabMotor;
    private LayerMask _grabPreviousLayer;
    private Vector3 previousPos;

    private void UpdateGrab() {
      if (!IsGrabing()) return;

      // grab falling, or someone on platform the other not.
      if ((_grabMotor.IsFalling() || _grabMotor.IsFallingFast()) ||
          (_grabMotor.IsOnPlatform() != _motor.IsOnPlatform())) {
          Drop();
          return;
      }

      Vector3 posDiff = transform.position - previousPos;

      if (posDiff.x == 0) {
        previousPos = transform.position;
        return;
      }

      posDiff.y = 0;
      posDiff.z = 0;

      RaycastHit2D hit = _grabMotor.GetClosestHit(_grabMotor._collider2D.bounds.center,
          posDiff.normalized, posDiff.magnitude, true, true);

      if (hit.collider != null)
      {
          // cannot move...
          // TODO this should call RaycastAndSeparate, or something like that
          transform.position = previousPos;
          return;
      }

      _grabObject.transform.position += posDiff;
      previousPos = transform.position;
    }

    #endregion
}
