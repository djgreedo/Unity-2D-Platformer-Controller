using UnityEngine;
using System.Collections.Generic;

public
class TriggerWater : MonoBehaviour
{
  public
  List<PlatformerMotor2D> whitelistMotors;

  void Start() {}

  void OnTriggerEnter2D(Collider2D o)
  {
    PlatformerMotor2DWater motor_water =
        o.gameObject.GetComponent<PlatformerMotor2DWater>();

    if (motor_water)
    {
      PlatformerMotor2D motor = o.gameObject.GetComponent<PlatformerMotor2D>();
      if (whitelistMotors.Count > 0 && whitelistMotors.IndexOf(motor) == -1)
        return;

      motor_water.OnWater(this.gameObject);
    }
  }

  void OnTriggerStay2D(Collider2D o) {}

  void OnTriggerExit2D(Collider2D o)
  {
    PlatformerMotor2DWater motor_water =
        o.gameObject.GetComponent<PlatformerMotor2DWater>();

    if (motor_water)
    {
      PlatformerMotor2D motor = o.gameObject.GetComponent<PlatformerMotor2D>();
      if (whitelistMotors.Count > 0 && whitelistMotors.IndexOf(motor) == -1)
        return;

      motor_water.OffWater();
    }
  }
}
