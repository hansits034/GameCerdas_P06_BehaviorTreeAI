using UnityEngine;

  public class EnemyAnimationEvents : MonoBehaviour
  {
      private EnemyBTController controller;

      private void Awake()
      {
          controller = GetComponentInParent<EnemyBTController>();
      }

      public void OnAttackHit()
      {
          if (controller != null)
              controller.DealAttackDamage();
      }
  }