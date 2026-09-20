# Application/Categories — người phụ trách: **B**

FR-CAT-001 → 005. Slice S3 (tuần 2–3).

```
Categories/
├── Commands/{Create,Update,Delete}/    FR-CAT-003/004/005
├── Queries/{GetCategories,GetCategoryBySlug}/   FR-CAT-001/002
└── Dtos/  CategoryDto, CategoryDetailDto
```

**Quyết định bắt buộc:** D8 (cache Redis, `categories:all` TTL 30 phút, invalidate **cả**
tag `recipes`), D2 (soft delete), D10 (slug auto-suffix).

**Bạn cũng sở hữu `ISlugHelper` và `ICacheService`** — hai hợp đồng chung cả nhóm dùng.
Chốt interface tuần 1, đừng đổi sau.
