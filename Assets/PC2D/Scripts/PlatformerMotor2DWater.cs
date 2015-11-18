using System;
using PC2D;
using UnityEngine;
using UnityEngine.Assertions;

// TODO slope logic must be speparated from the motor, so can be used here
// TODO animations
// TODO weight affects speed ?

/// <summary>
/// Plugin to allow motor to Grab other motors, the other motor should no be
/// mobable just react to the enviroment like a box
/// </summary>
[RequireComponent(
    typeof(PlatformerMotor2D))] public class PlatformerMotor2DWater
    : PlatformerMotor2DPlugin
{
#region Public

  /// <summary>
  /// Maximum horizontal speed of the motor while on water something
  /// </summary>
  public
  float waterXSpeed = 3f;
  /// <summary>
  /// Maximum emerging speed
  /// </summary>
  public
  float opossingYSpeed = 8f;
  /// <summary>
  /// offset of water surface, it's used so you can adjust the motor
  /// </summary>
  public
  float inmersionLine = 0f;
  /// <summary>
  /// How much speed the motor lose when touch the surface.
  /// This is done to reduce the speed, considered surface friction
  /// </summary>
  public
  float firstTouchFactor = 0.2f;

  public
  float gravityMultiplier = 1f;

  private
  float _gravityMultiplier;

  private
  bool first = true;

  //
  // plugin
  //
  override public PlatformerMotor2DPlugin.Action
  GetCurrentVelocity(Vector3 currentVelocity, out Vector3 velocity)
  {
    velocity = Vector3.zero;
    velocity.x = _motor.normalizedXMovement * waterXSpeed;
    velocity.y = currentVelocity.y;

    if (IsOnWater())
    {
      // if the motor is jumping, do not modify the velocity or it won't jump...
      if (_motor.IsJumping())
        return PlatformerMotor2DPlugin.Action.Continue;

      float currentVelocityY = currentVelocity.y;
      if (first)
      {
        velocity.y *= firstTouchFactor; // reduce velocity mimic splash
        first = false;
        return PlatformerMotor2DPlugin.Action.Handled;
      }

      float surfaceY = _water.transform.position.y + _waterCollider.offset.y +
                       _waterCollider.size.y * 0.5f;
      float objectY =
          _motor.transform.position.y + _motorCollider.offset.y * 0.5f;

      float penetration = surfaceY - objectY - inmersionLine;

      if (penetration > 0)
      {
        velocity.y = _motor.Accelerate(
            currentVelocityY,
            _motor.gravityMultiplier * -Physics2D.gravity.y *
                (1 + penetration), // * (currentVelocity.y < -2 ? 2 : 0)
            Mathf.Max(1f, opossingYSpeed * penetration));

        return PlatformerMotor2DPlugin.Action.Handled;
      }

      return PlatformerMotor2DPlugin.Action.Continue;
    }
    return PlatformerMotor2DPlugin.Action.Continue;
  }

  void OnDrawGizmosSelected()
  {
    if (_waterCollider != null)
    {
      Vector3 a = _water.transform.position + (Vector3)_waterCollider.offset;
      Vector3 b = a;
      a.x = float.PositiveInfinity;
      b.x = float.NegativeInfinity;
      Gizmos.color = Color.blue;
      Gizmos.DrawLine(a, b);
      Debug.Break();
    }
  }

  override public void OnEnable()
  {
    _motorCollider = GetComponent<BoxCollider2D>();
    base.OnEnable();
  }

  override public void OnDisable()
  {
    if (IsOnWater())
    {
      OffWater();
    }
    base.OnDisable();
  }

  public
  void OnStartJump()
  {
    // restore gravity!
    OffWater();
    _motor.onJump -= OnStartJump;
  }

  public
  void OnWater(GameObject water)
  {
    if (_motor == null)
      return;

    _gravityMultiplier = _motor.gravityMultiplier; // save
    _motor.gravityMultiplier = gravityMultiplier;
    first = true;
    _water = water;
    _waterCollider = water.GetComponent<BoxCollider2D>();
    _initialVelocity = _motor.velocity;
    _motor.onJump += OnStartJump;
  }

  public
  void OffWater()
  {
    if (_water == null)
      return; // do not double exec

    _motor.gravityMultiplier = _gravityMultiplier; // restore
    _water = null;
    _waterCollider = null;
  }

  public
  bool IsOnWater() { return _motor != null && _water != null; }
#endregion

#region Private

  private
  GameObject _water;
  private
  BoxCollider2D _waterCollider;
  private
  BoxCollider2D _motorCollider;
  private
  Vector3 previousPos;
  private
  Vector3 _initialVelocity;

#endregion
}
