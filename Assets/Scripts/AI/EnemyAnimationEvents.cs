using UnityEngine;

  public class EnemyAnimationEvents : MonoBehaviour
  {
      // IEnemyAI supaya bekerja untuk BT, Utility, dan BT + Utility
      private IEnemyAI controller;

      private void Awake()
      {
          controller = GetComponentInParent<IEnemyAI>();
      }

      public void OnAttackHit()
      {
          if (controller != null)
              controller.DealAttackDamage();
      }
  }
