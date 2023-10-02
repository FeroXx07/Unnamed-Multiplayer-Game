using System.Collections;
using System.Collections.Generic;
using System.Data;
using Unity.VisualScripting;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    public WeaponData data;
    public GameObject bulletPrefab;
    [SerializeField] Transform firePoint;
    float timeSinceLastShoot;

    SpriteRenderer renderer;

    private void Start()
    {
        renderer = GetComponent<SpriteRenderer>();
        renderer.sprite = data.sprite;
    }
    private void OnEnable()
    {
        PlayerShooter.OnShoot += Shoot;
        PlayerShooter.OnReload += Reload;
        data.currentMaxAmmo = data.maxAmmo;
        data.currentAmmo = data.magazineSize;
    }
    private void OnDisable()
    {
        data.Reloading = false;
        PlayerShooter.OnShoot -= Shoot;
        PlayerShooter.OnReload -= Reload;
    }
    void Reload()
    {
        if (data.Reloading || data.currentMaxAmmo <= 0 || data.currentAmmo >= data.magazineSize || !gameObject.activeSelf) return;

        StartCoroutine(Realoading());
    }
    IEnumerator Realoading()
    { 
        data.Reloading = true;

        yield return new WaitForSeconds(data.reloadSpeed);

        if(data.currentMaxAmmo > 0)
        {
            int bulletsToReload = (data.magazineSize - data.currentAmmo);

            data.currentMaxAmmo -= bulletsToReload;
            data.currentAmmo = data.magazineSize;
            data.Reloading = false;
        }

    }
    bool CanShoot()
    {
        //if is reloading or the fire rate is less than the current fire time
        return !data.Reloading && timeSinceLastShoot > 1 / data.fireRate / 60;
    }
    void Shoot()
    {

        if (data.currentAmmo > 0)
        {
            if (CanShoot())
            {
              
                GameObject bullet = Instantiate(bulletPrefab);
                
                bullet.GetComponent<Bullet>().Damage = data.damage;

                transform.localRotation = transform.parent.rotation;

                float dispersion;
                dispersion = Random.Range(-data.dispersion, data.dispersion);

                Quaternion newRot = Quaternion.Euler(transform.localEulerAngles.x,
                        transform.localEulerAngles.y,
                        transform.localEulerAngles.z + dispersion);

                transform.rotation = newRot;

                bullet.transform.position = firePoint.position;
                bullet.GetComponent<Rigidbody2D>().velocity = transform.right * data.bulletSpeed;

                data.currentAmmo--;
                timeSinceLastShoot = 0;
                OnGunShoot();
            }
        }
        else
        {
            Reload();
        }

    }
    private void Update()
    {
        timeSinceLastShoot += Time.deltaTime;
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (mousePos.x < transform.position.x)
        {
          renderer.flipY = true;
        }
        else
        {
            renderer.flipY = false;
        }
    }
    void OnGunShoot()
    {
        //VFX, sound
    }
}
