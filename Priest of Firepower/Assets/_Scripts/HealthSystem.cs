using System;
using System.IO;
using _Scripts.Interfaces;
using _Scripts.Networking;
using UnityEngine;

namespace _Scripts
{
    public class HealthSystem : NetworkBehaviour,IDamageable
    {
        [SerializeField] private int health;
        [SerializeField] private int maxHealth;
        [SerializeField] private LayerMask layer;
        public LayerMask Layers { get => layer; set => layer = value; }
        public int Health { get => health; set => health = value; }
        public int MaxHealth { get => maxHealth; set => maxHealth = value; }

        public event Action<GameObject, GameObject> OnDamageableDestroyed;
        public event Action<GameObject, GameObject> OnDamageTaken;

        protected override void InitNetworkVariablesList()
        {
        }

        public override void Awake()
        {
            base.Awake();
            InitNetworkVariablesList();
            BITTracker = new ChangeTracker(NetworkVariableList.Count);
        }

        public override void OnEnable()
        {
            base.OnEnable();
            
            health = maxHealth;
        }

        public void RaiseEventOnDamageableDestroyed(GameObject destroyer)
        {
            OnDamageableDestroyed?.Invoke(gameObject, destroyer);
        }
        
        public void ProcessHit(IDamageDealer damageDealer, Vector3 dir, GameObject hitOwnerGameObject, GameObject hitterGameObject,
            GameObject hittedGameObject)
        {
            Debug.Log($"Health system: Processed Hit. Owner: {hitOwnerGameObject.name}, Hitter: {hitterGameObject}, Hitted: {hittedGameObject}");
            TakeDamage(damageDealer, dir, hitOwnerGameObject);
        }

        public void TakeDamage(IDamageDealer damageDealer, Vector3 dir, GameObject owner)
        {
            health -= damageDealer.Damage;
            OnDamageTaken?.Invoke(gameObject, owner);
            
            if (TryGetComponent<IPointsProvider>(out IPointsProvider pointsProvider ))
            {
                if (owner.TryGetComponent<PointSystem>(out PointSystem pointSystem))
                {
                    pointSystem.PointsOnHit(pointsProvider);
                }
            }

            if (health <= 0)
            {
                if (TryGetComponent<IPointsProvider>(out IPointsProvider pointsProviders))
                {
                    if (owner.TryGetComponent<PointSystem>(out PointSystem pointSystem))
                    {
                        pointSystem.PointsOnDeath(pointsProviders);
                    }
                }
            
                health = 0;
                RaiseEventOnDamageableDestroyed(owner);
            }
            
            SendReplicationData(ReplicationAction.UPDATE);
        }

        protected override ReplicationHeader WriteReplicationPacket(MemoryStream outputMemoryStream, ReplicationAction action)
        {
            BinaryWriter writer = new BinaryWriter(outputMemoryStream);
            writer.Write(Health);
            writer.Write(MaxHealth);
            ReplicationHeader replicationHeader = new ReplicationHeader(NetworkObject.GetNetworkId(), this.GetType().FullName, action, outputMemoryStream.ToArray().Length);
            return replicationHeader;
        }

        public override bool ReadReplicationPacket(BinaryReader reader, long currentPosition = 0)
        {
            Health = reader.ReadInt32();
            MaxHealth = reader.ReadInt32();
            return true;
        }
    }
}
