using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spelunky2LeaderboardGenerator
{
    public enum Character
    {
        Unknown = -1,
        Ana = 0x00,
        Margaret = 0x01,
        Colin = 0x02,
        Roffy = 0x03,
        Alto = 0x04,
        Liz = 0x05,
        Nekka = 0x06,
        LISE = 0x07,
        Coco = 0x08,
        Manfred = 0x09,
        Jay = 0x0A,
        Tina = 0x0B,
        Valerie = 0x0C,
        Au = 0x0D,
        Demi = 0x0E,
        Pilot = 0x0F,
        Airyn = 0x10,
        Dirk = 0x11,
        Guy = 0x12,
        ClassicGuy = 0x13
    }

    public enum DataBlocks
    {
        IDs,
        Names,
        Meta,
        RunData,
        Scores,
        Block7,
        Block8
    }

    public enum DataSource
    {
        Unknown,
        Download,
        Json,
        LocalFile
    }

    public enum PageType
    {
        Unknown,
        Depth,
        Score,
        Time,
        Stats,
        Player
    }

    public enum Platform
    {
        Unknown = 0x00,
        PC = 0x12,
        PS4 = 0x30
    }

    public enum RunEndCause
    {
        Unknown = 0,
        SpecialClear = 0x008A,
        HardClear = 0x01E9,
        NormalClear = 0x02ED
    }

    public enum WorkType
    {
        Unknown,
        UpdateCurrent,
        GetDaily,
        Rebuild
    }
}
