namespace Learnexia.Modules.Gamification.Application.Caching;

/// <summary>
/// Packed-integer leaderboard score encoder. Locks XP into the high 27 bits, JoinOrder
/// into the low 24 bits (inverted so lower JoinOrder ranks higher within an XP tie).
///
///   score = (long)weeklyXp * (1 &lt;&lt; 24) + ((1 &lt;&lt; 24) - 1 - joinOrder)
///
/// Range: weeklyXp up to ~1.8B (29 bits headroom), joinOrder 0..16,777,215. Total fits
/// safely in double precision (53-bit mantissa) — no precision loss for any realistic
/// cohort.
/// </summary>
public static class LeaderboardScoreEncoder
{
    private const long JoinOrderRange = 1L << 24; // 16,777,216
    private const long JoinOrderMax = JoinOrderRange - 1;

    public static long Encode(int weeklyXp, int joinOrder)
    {
        if (weeklyXp < 0)   throw new ArgumentOutOfRangeException(nameof(weeklyXp));
        if (joinOrder < 0 || joinOrder > JoinOrderMax)
            throw new ArgumentOutOfRangeException(nameof(joinOrder));

        return (long)weeklyXp * JoinOrderRange + (JoinOrderMax - joinOrder);
    }

    public static (int weeklyXp, int joinOrder) Decode(long score)
    {
        if (score < 0) throw new ArgumentOutOfRangeException(nameof(score));
        var weeklyXp = (int)(score / JoinOrderRange);
        var joinOrder = (int)(JoinOrderMax - (score % JoinOrderRange));
        return (weeklyXp, joinOrder);
    }
}
