using System;
using System.Collections.Generic;
using System.Text;

using KinshipCalculator.Core.Models;

namespace KinshipCalculator.Core.Services.Formatting;

/// <summary>
/// Character-level Hans/Hant normalization for the closed kinship vocabulary.
/// Formatter branches historically hard-coded one script per literal, so each
/// LocalizedText slot must be normalized to its own script at assembly time.
/// Unknown characters pass through untouched.
/// </summary>
public static class KinshipScriptConverter
{
    private static readonly (char Hant, char Hans)[] Pairs =
    {
        ('兒', '儿'),
        ('孫', '孙'),
        ('親', '亲'),
        ('姪', '侄'),
        ('婦', '妇'),
        ('媽', '妈'),
        ('爺', '爷'),
        ('嬸', '婶'),
        ('內', '内'),
        ('遠', '远'),
        ('繼', '继'),
        ('養', '养'),
        ('長', '长'),
        ('輩', '辈'),
        ('後', '后'),
        ('開', '开'),
        ('雲', '云'),
        ('來', '来'),
        ('過', '过'),
        ('邊', '边'),
        ('係', '系'),
        ('屬', '属'),
        ('雙', '双'),
        ('環', '环'),
        ('迴', '回'),
        ('於', '于'),
        ('隔', '隔'),
        ('輪', '轮'),
        ('鏈', '链'),
        ('連', '连'),
        ('寫', '写'),
        ('簡', '简'),
        ('間', '间'),
        ('對', '对'),
        ('稱', '称'),
        ('謂', '谓'),
        ('輔', '辅'),
        ('離', '离'),
        ('歲', '岁'),
        ('幾', '几'),
        // Characters the LEXICON LAYERS brought in (2026-08-02 pilot). The table above covers
        // the engine's own composed vocabulary; a regional word carries characters the engine
        // never writes, and an unmapped one survives into the Traditional rendering as-is
        // (老汉 shown to a zh-Hant reader). Test-LexiconInvariants.ps1 fails on any layer
        // character that is neither mapped here nor listed as script-neutral.
        ('漢', '汉'),   // 老漢, 漢子
        ('嬤', '嬷'),   // 阿嬤, 外嬤
        ('亞', '亚'),   // 亞公, 亞婆
        ('閨', '闺'),   // 閨女, 甥閨女
        ('頭', '头'),   // 丫頭
        ('竇', '窦'),   // 老竇
        ('東', '东'),   // 東床
        ('從', '从'),   // 從母
        ('細', '细'),   // 細佬
        ('愛', '爱'),   // 愛人
        ('館', '馆'),   // 甥館
        // Second lexicon batch (2026-08-02): the 連襟 family and the deep in-law lines.
        ('擔', '担'),   // 一擔挑, 擔兒挑
        ('兩', '两'),   // 兩橋
        ('橋', '桥'),   // 連橋, 兩橋
        ('婭', '娅'),   // 婭 -- 《爾雅》兩婿相謂
        ('賢', '贤'),   // 賢內助
        // ⚠ SENSE-DEPENDENT in general Chinese: 发 is 髮 (hair) in 髮妻 = 結髮之妻, but 發 almost
        // everywhere else. This table is character-level and cannot split by sense, so the pair
        // is only safe because 髮妻 is the sole 发 word in the whole kinship vocabulary. If a
        // future batch brings a 發-sense word, the two cannot share this table -- the term must
        // be authored in its Traditional form and this pair reconsidered. The lexicon character
        // gate is what will surface that.
        ('髮', '发'),   // 髮妻
        ('彌', '弥')    // 彌甥 -- 《左傳》外甥之子
    };

    private static readonly Dictionary<char, char> ToHansMap = BuildMap(static pair => (pair.Hant, pair.Hans));
    private static readonly Dictionary<char, char> ToHantMap = BuildMap(static pair => (pair.Hans, pair.Hant));

    private static Dictionary<char, char> BuildMap(Func<(char Hant, char Hans), (char Key, char Value)> selector)
    {
        var map = new Dictionary<char, char>();
        foreach ((char Hant, char Hans) pair in Pairs)
        {
            (char key, char value) = selector(pair);
            if (key != value)
            {
                map[key] = value;
            }
        }

        return map;
    }

    public static string ToHans(string text) => Convert(text, ToHansMap);

    public static string ToHant(string text) => Convert(text, ToHantMap);

    public static LocalizedText Normalize(LocalizedText text)
    {
        string hans = ToHans(text.ZhHans);
        string hant = ToHant(text.ZhHant);
        if (string.Equals(hans, text.ZhHans, StringComparison.Ordinal) && string.Equals(hant, text.ZhHant, StringComparison.Ordinal))
        {
            return text;
        }

        return new LocalizedText(hans, hant, text.English);
    }

    private static string Convert(string text, Dictionary<char, char> map)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        StringBuilder? builder = null;
        for (int index = 0; index < text.Length; index++)
        {
            if (map.TryGetValue(text[index], out char mapped))
            {
                builder ??= new StringBuilder(text, 0, index, text.Length);
                builder.Append(mapped);
            }
            else
            {
                builder?.Append(text[index]);
            }
        }

        return builder is null ? text : builder.ToString();
    }
}
