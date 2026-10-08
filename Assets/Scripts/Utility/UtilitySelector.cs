using System.Collections.Generic;
using UnityEngine;

// Pemilihan action Utility AI, dipakai oleh EnemyUtilityController
// dan EnemyBTUtilityController.
public static class UtilitySelector
{
    public static T Select<T>(
        IList<T> actions,
        System.Func<T, float> scoreOf,
        T current,
        float currentStartTime,
        float minimumActionDuration,
        float hysteresisMargin) where T : class
    {
        // Modul 71 — pilih skor tertinggi
        T best = null;
        float bestScore = -1f;

        foreach (T action in actions)
        {
            float score = scoreOf(action);

            if (score > bestScore)
            {
                best = action;
                bestScore = score;
            }
        }

        if (current == null || best == current)
            return best;

        float currentScore = scoreOf(current);

        // Action sekarang sudah tidak valid (skor 0), atau ada keadaan
        // darurat (skor 1): boleh langsung berganti.
        if (currentScore <= 0f || bestScore >= 1f)
            return best;

        // Modul 73 — Action Commitment
        bool committed =
            Time.time - currentStartTime < minimumActionDuration;

        // Modul 74 — Hysteresis
        bool notMuchBetter =
            bestScore < currentScore + hysteresisMargin;

        return committed || notMuchBetter ? current : best;
    }
}
