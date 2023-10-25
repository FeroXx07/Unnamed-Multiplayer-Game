using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerUpMaxAmmo : PowerUpBase
{
    private void Awake()
    {
        instance = this;
    }

    public override void ApplyPowerUp()
    {
        base.ApplyPowerUp();

        Weapon[] allWeapons = FindObjectsOfType<Weapon>();
        foreach (Weapon weapon in allWeapons)
        {
            if (weapon != null)
                weapon.GiveMaxAmmo();
        }
    }
}
