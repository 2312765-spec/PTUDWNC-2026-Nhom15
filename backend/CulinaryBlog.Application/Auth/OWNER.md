# Application/Auth — người phụ trách: **A**

FR-AUTH-001 → 007. Slice S2 (tuần 2–3), S9 (tuần 4–5).

```
Auth/
├── Commands/
│   ├── Register/          RegisterCommand + Handler + Validator      FR-AUTH-001
│   ├── Login/                                                        FR-AUTH-002
│   ├── GoogleLogin/                                                  FR-AUTH-003  (D9: body { idToken })
│   ├── RefreshToken/                                                 FR-AUTH-004  (D20: rotation + reuse detection)
│   ├── Logout/                                                       FR-AUTH-005
│   └── UpdateProfile/                                                FR-AUTH-007
├── Queries/GetCurrentUser/                                           FR-AUTH-006
└── Dtos/  AuthResponseDto, UserProfileDto
```

**Quyết định bắt buộc đọc trước:** D5 (`displayName`/`avatarUrl`/`bio`, **không** `fullName`),
D20 (RefreshToken schema), D4 (validation → 400), D11 (`IsActive` → 403), D17 (lockout → 423),
D9 (Google ID Token), D12 (không xác nhận email).
