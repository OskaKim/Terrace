---
status: 歴史的な記録
sources:
  - docs/prompts/fable-prompts.md
---

# 指示書

[fable-prompts.md](fable-prompts.md) は、2026-09-04 に Terrace.MasterData・Terrace.Map・Terrace.Server を最初に作らせた指示書の写し。元は `../docs/for_ai_prompts/fable-prompts.md`(このフォルダの外)にあった。

**今の仕様ではない。** 作った後で変わった点がある。食い違ったら `docs/` の他の文書とコードを正とする。

| 指示書の記述 | 今 |
|---|---|
| .NET は `net9.0` | `net10.0` |
| Portal は `Id, Name, X, Y, TargetMapId, TargetPortalName` | `Kind`(Portal / Spawn)が増えた。マップに NPC・飾り・テーマも増えた |
| Server は「ダミーのスポーン設定」 | マップの湧き点とマスタの敵から組み立てる(実装中) |
| Server は「インベントリはやらない」 | 変わらず Server には無い。Client にはオフラインの持ち物と店がある |
| Receiver は `OnJoin` 〜 `OnSnapshot` の 7 つ | 敵の位置とドロップの通知が増えた([contracts/protocol.md](../contracts/protocol.md)) |

新しい指示書を書いたら、このフォルダに日付の分かる名前で足す。
