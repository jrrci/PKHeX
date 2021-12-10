﻿using System;
using static PKHeX.Core.ContestStatGranting;

namespace PKHeX.Core;

public static class ContestStatInfo
{
    private const int WorstFeelBlock = 3;
    private const int WorstFeelPoffin = 17;
    private const int MaxContestStat = 255;

    public static void SetSuggestedContestStats(this PKM pk, IEncounterTemplate enc)
    {
        if (pk is not IContestStatsMutable s)
            return;

        var restrict = GetContestStatRestriction(pk, pk.Generation);
        var baseStat = GetReferenceTemplate(enc);
        if (restrict == None || pk.Species is not (int)Species.Milotic)
            baseStat.CopyContestStatsTo(s); // reset
        else
            s.SetAllContestStatsTo(MaxContestStat, restrict == NoSheen ? baseStat.CNT_Sheen : (byte)255);
    }

    public static void SetMaxContestStats(this PKM pk, IEncounterTemplate enc)
    {
        if (pk is not IContestStatsMutable s)
            return;
        var restrict = GetContestStatRestriction(pk, pk.Generation);
        var baseStat = GetReferenceTemplate(enc);
        if (restrict == None)
            return;
        s.SetAllContestStatsTo(MaxContestStat, restrict == NoSheen ? baseStat.CNT_Sheen : (byte)255);
    }

    public static ContestStatGranting GetContestStatRestriction(PKM pk, int origin) => origin switch
    {
        3 => pk.Format < 6    ? CorrelateSheen : Mixed,
        4 => pk.Format < 6    ? CorrelateSheen : Mixed,

        5 => pk.Format >= 6          ? NoSheen : None, // ORAS Contests
        6 => pk.AO || !pk.IsUntraded ? NoSheen : None,
        8 => pk.BDSP          ? CorrelateSheen : None, // BDSP Contests
        _ => None,
    };

    public static int CalculateMaximumSheen(IContestStats s, int nature, IContestStats initial, bool pokeBlock3)
    {
        if (s.IsAnyContestStatMax())
            return MaxContestStat;

        if (s.IsContestEqual(initial))
            return initial.CNT_Sheen;

        var avg = GetAverageFeel(s, nature, initial);
        if (avg <= 0)
            return initial.CNT_Sheen;

        if (pokeBlock3)
        {
            var fudge = (avg * 225) / 100;
            return Math.Min(MaxContestStat, Math.Max(WorstFeelBlock, fudge));
        }

        // Can get trash poffins by burning and spilling on purpose.
        return Math.Min(MaxContestStat, avg * WorstFeelPoffin);
    }

    public static int CalculateMinimumSheen(IContestStats s, int nature, IContestStats initial, bool pokeBlock3)
    {
        if (s.IsContestEqual(initial))
            return initial.CNT_Sheen;

        var rawAvg = GetAverageFeel(s, 0, initial);
        if (rawAvg == MaxContestStat)
            return MaxContestStat;

        var avg = Math.Max(1, nature % 6 == 0 ? rawAvg : GetAverageFeel(s, nature, initial));
        avg = Math.Min(rawAvg, avg); // be generous

        var worst = pokeBlock3 ? WorstFeelBlock : WorstFeelPoffin;
        return Math.Min(MaxContestStat, Math.Max(worst, avg));
    }

    private static int GetAverageFeel(IContestStats s, int nature, IContestStats initial)
    {
        ReadOnlySpan<sbyte> span = NatureAmpTable.AsSpan(5 * nature, 5);
        int sum = 0;
        sum += GetAmpedStat(span, 0, s.CNT_Cool - initial.CNT_Cool);
        sum += GetAmpedStat(span, 1, s.CNT_Beauty - initial.CNT_Beauty);
        sum += GetAmpedStat(span, 2, s.CNT_Cute - initial.CNT_Cute);
        sum += GetAmpedStat(span, 3, s.CNT_Smart - initial.CNT_Smart);
        sum += GetAmpedStat(span, 4, s.CNT_Tough - initial.CNT_Tough);
        return sum / 5;
    }

    private static int GetAmpedStat(ReadOnlySpan<sbyte> amps, int index, int gain)
    {
        var amp = amps[index];
        if (amp == 0)
            return gain;
        return gain + GetStatAdjustment(gain, amp);
    }

    private static int GetStatAdjustment(int gain, sbyte amp)
    {
        // Undo the favor factor
        var undoFactor = amp == 1 ? 11 : 9;
        var boost = Boost(gain, undoFactor);
        return amp == -1 ? boost : -boost;

        static int Boost(int stat, int factor)
        {
            var remainder = stat % factor;
            var boost = stat / factor;

            if (remainder >= 5)
                ++boost;
            return boost;
        }
    }

    private static readonly DummyContestNone DummyNone = new();

    public static IContestStats GetReferenceTemplate(IEncounterTemplate initial) => initial is IContestStats s ? s : DummyNone;

    private class DummyContestNone : IContestStats
    {
        public byte CNT_Cool => 0;
        public byte CNT_Beauty => 0;
        public byte CNT_Cute => 0;
        public byte CNT_Smart => 0;
        public byte CNT_Tough => 0;
        public byte CNT_Sheen => 0;
    }

    private static readonly sbyte[] NatureAmpTable =
    {
        // Spicy,  Dry, Sweet, Bitter, Sour
        0, 0, 0, 0, 0, // Hardy
        1, 0, 0, 0,-1, // Lonely
        1, 0,-1, 0, 0, // Brave
        1,-1, 0, 0, 0, // Adamant
        1, 0, 0,-1, 0, // Naughty
       -1, 0, 0, 0, 1, // Bold
        0, 0, 0, 0, 0, // Docile
        0, 0,-1, 0, 1, // Relaxed
        0,-1, 0, 0, 1, // Impish
        0, 0, 0,-1, 1, // Lax
       -1, 0, 1, 0, 0, // Timid
        0, 0, 1, 0,-1, // Hasty
        0, 0, 0, 0, 0, // Serious
        0,-1, 1, 0, 0, // Jolly
        0, 0, 1,-1, 0, // Naive
       -1, 1, 0, 0, 0, // Modest
        0, 1, 0, 0,-1, // Mild
        0, 1,-1, 0, 0, // Quiet
        0, 0, 0, 0, 0, // Bashful
        0, 1, 0,-1, 0, // Rash
       -1, 0, 0, 1, 0, // Calm
        0, 0, 0, 1,-1, // Gentle
        0, 0,-1, 1, 0, // Sassy
        0,-1, 0, 1, 0, // Careful
        0, 0, 0, 0, 0, // Quirky
    };
}