# Answer key — engine output + provenance (DO NOT give to the blind agent)

Regenerated after release-audit round 2 (all previously-suspect rows repaired;
engine outputs current as of this commit). Compare the blind agent's `your term`
against `ours`: agreement on attested rows calibrates trust; disagreement on an
un-attested row is a cell for us to adjudicate.

| # | chain | ours | mumuy | note |
|---|---|---|---|---|
| 1 | `F.M` | **祖母** | 奶奶 | calibration |
| 2 | `M.M.F` | **外曾外祖父** | 外曾外祖父 | calibration |
| 3 | `F.OB.S` | **堂兄** | 堂哥 / 堂弟 | calibration |
| 4 | `M.OB.SP` | **舅母** | 大舅妈 | calibration |
| 5 | `F.F.OB` | **伯祖父** | 伯公 | calibration |
| 6 | `M.YS` | **姨母** | 小姨 | calibration |
| 7 | `F.F.OB.SP.YB` | **伯祖眷舅祖父** | 叔祖眷舅祖父 | REPAIRED juan-law cell (was 叔姻祖伯 garble) |
| 8 | `F.OB.SP.YB` | **伯眷舅父** | 叔眷舅父 | REPAIRED juan-law cell |
| 9 | `M.OB.SP.YB` | **舅眷舅父** | 舅眷舅父 | REPAIRED juan-law cell (mumuy exact) |
| 10 | `F.F.OB.SP.YS` | **伯祖眷姨祖母** | 叔祖眷姨祖母 | REPAIRED juan-law cell |
| 11 | `F.M.M.OB.OS.D` | **姨表祖母** | (silent) | un-attested deep affinal (forked bridge) |
| 12 | `F.M.M.OB.OS.D.SP.YB` | **姨表祖姻叔祖父** | (silent) | un-attested deep affinal (composed) |
| 13 | `F.M.M.OB.OS.D.SP.YS` | **姨表祖姻姑祖母** | (silent) | un-attested deep affinal (composed) |
| 14 | `M.YS.S.SP.OS` | **姨表兄弟眷姊妹** | 从母兄弟眷姊妹 | un-attested affinal |
| 15 | `F.OS.D.SP.YB.S` | **姑表姐姻姪子** | (silent) | un-attested affinal |
| 16 | `F.F.OB.S.SP.OB.D` | **從父叔眷舅表姊** | 从父叔眷舅表姊 / 从父叔眷舅表妹 | un-attested affinal |
| 17 | `M.F.YS.OB.YS` | **姑外祖母** | (silent) | REPAIRED overlap-loop cell (was 外祖父) |
| 18 | `YS.F.YS.S.M.S` | **姑表弟** | (silent) | REPAIRED collapse cell (was 姑母) |
| 19 | `OB.YS.M.D.S.F` | **父的女的配偶** | (silent) | REPAIRED co-parent identity family (was 嫂-class) |
| 20 | `D.D.F.OS.YB.OS` | **姻姪女** | (silent) | loop shape (terminal female) |
| 21 | `F.F.F.OB.S.S` | **從伯** | 从伯父 / 从叔父 | deep collateral (pure blood) |
| 22 | `M.M.OB.D.D` | **舅表姨表姐** | 舅表姨表姐 / 舅表姨表妹 | deep collateral (pure blood) |
| 23 | `YB.M.YS.S.F.M.YB.OS.YS.SP.F.M.M.OB.OS.D.SP.YB` | **姨母姻姨祖姻姨表曾祖姻叔曾祖父** | (silent) | the 18-hop monster (un-attested) |
