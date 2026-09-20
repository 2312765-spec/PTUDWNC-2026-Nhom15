# Application/Recipes/Commands — người phụ trách: **C**

FR-RCP-003, 004, 005, 006, 007, 009, 010. Slice S5, S6, S7.

```
Commands/
├── Create/  Update/                        FR-RCP-003/004
├── Publish/  Archive/  Delete/             FR-RCP-005/006/007
├── Ingredients/{Add,Update,Delete}/        FR-RCP-009
└── Steps/{Add,Update,Delete}/              FR-RCP-010
```

**Business rule nằm trong Domain method, KHÔNG nằm trong handler.**
`recipe.Publish()` ném `DomainException` — viết unit test cho domain trước, không cần DB.

**Quyết định bắt buộc:** **D1** (soft delete — SRS FR-RCP-007 nói hard delete + cascade +
xóa MinIO, **sai cả ba**), **D3** (publish cần ≥1 step VÀ ≥1 ingredient), D4 (concurrency → 409),
D6 (`timerMinutes`, server sinh `stepNumber`), D7 (`quantity`/`unit` nullable, `orderIndex`),
D10, D13 (slug bất biến), D19 (`cookTime >= 0`).
