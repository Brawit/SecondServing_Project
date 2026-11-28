using System;

// [v2] ฤดูกาล (คอลัมน์ Season ในชีต Ingredients v2) เลือกได้หลายฤดูพร้อมกัน
// ตอนนี้เก็บไว้ในข้อมูลเฉยๆ ระบบฤดูกาล/ปฏิทินจริงมาเฟส 3
[Flags]
public enum Season
{
    None = 0,
    Spring = 1,
    Summer = 2,
    Fall = 4,
    Winter = 8,
    All = Spring | Summer | Fall | Winter
}

public static class SeasonUtil
{
    // แปลงข้อความในตาราง เช่น "All", "Fall, Winter", "Spring, Summer, Fall"
    public static bool TryParse(string sheetText, out Season season)
    {
        season = Season.None;
        if (string.IsNullOrWhiteSpace(sheetText))
        {
            return false;
        }

        foreach (string part in sheetText.Split(','))
        {
            string word = part.Trim();
            if (word.Length == 0)
            {
                continue;
            }

            if (Enum.TryParse(word, true, out Season one) && Enum.IsDefined(typeof(Season), one))
            {
                season |= one;
            }
            else
            {
                return false;
            }
        }

        return season != Season.None;
    }
}
