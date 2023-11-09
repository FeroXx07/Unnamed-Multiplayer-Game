using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponUIInfo : MonoBehaviour
{
    [SerializeField] WeaponData weaponData;
    [SerializeField] Image weaponSprite;
    [SerializeField] Image magazineSprite;
    [SerializeField] TextMeshProUGUI magazineCount;
    [SerializeField] TextMeshProUGUI totalAmmo;

    private void Awake()
    {
        weaponSprite.preserveAspect = true;
    }

    public void SetWeapon(WeaponData data)
    {
        weaponData = data;
        weaponSprite.sprite = data.sprite;
    }

    private void Update()
    {
        if (weaponData == null) return;
        
        float fill = 0;
        
        //draw remaining magazines amount
        if (weaponData.maxAmmoCapacity != 0)
        {
            fill = weaponData.ammoInMagazine / (float)weaponData.magazineSize;
            magazineSprite.fillAmount = fill;
            
            
        }

        //show remaining magazines
        if (fill == 1)
        {
            magazineCount.text = "x" + Mathf.CeilToInt(weaponData.totalAmmo / (float)weaponData.maxAmmoCapacity * weaponData.magazineSize).ToString();
        }

        int currentAmmo = weaponData.totalAmmo + weaponData.ammoInMagazine;

        totalAmmo.text = currentAmmo.ToString() + " / " + (weaponData.maxAmmoCapacity + weaponData.magazineSize).ToString();
    }
}
