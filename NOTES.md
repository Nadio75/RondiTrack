| # | Verb + Route | 400 when... | 404 when... | 409 when... | 422 when... |
|---|---|---|---|---|---|
| 1 | GET /api/users | none | none | none | none |
| 2 | POST /api/users | Name empty/whitespace; Name over 100 chars; ContactNumber empty/whitespace; ContactNumber not exactly 10 digits; malformed body* | none | none | none |
| 3 | GET /api/users/{id} | id not a GUID* | user not found | none | none |
| 4 | PUT /api/users/{id} | id not a GUID*; same 4 body rules as create (it reuses CreateUserRequest) | user not found | none | none |
| 5 | DELETE /api/users/{id} | id not a GUID* | user not found | none | none |
| 6 | GET /api/stokvels | none | none | none | none |
| 7 | POST /api/stokvels | Name empty/whitespace; Name over 100 chars; ContributionAmount 0 or less; malformed body* | none | none | none |
| 8 | GET /api/stokvels/{id} | id not a GUID* | stokvel not found | none | none |
| 9 | PUT /api/stokvels/{id} | id not a GUID*; same 3 body rules as create | stokvel not found | none | none |
| 10 | DELETE /api/stokvels/{id} | id not a GUID* | stokvel not found | none | none |
| 11 | POST /api/stokvels/{id}/members | id not a GUID*; UserId is the empty GUID; malformed body* | stokvel not found; user not found | user is already a member | none |
| 12 | POST /api/stokvels/{id}/contributions | id not a GUID*; UserId empty GUID; Amount 0 or less; ContributionCycleId empty GUID; malformed body* | stokvel not found; user not found; user not a member of this stokvel; cycle not found or belongs to another stokvel | member already paid this cycle; Idempotency-Key reused with a different body | Idempotency-Key header missing or blank |
| 13 | GET /api/stokvels/{stokvelId}/cycles | stokvelId not a GUID* | stokvel not found | none | none |
| 14 | POST /api/stokvels/{stokvelId}/cycles | stokvelId not a GUID*; Period empty; Period not YYYY-MM; TargetAmount 0 or less; malformed body* | stokvel not found | a cycle for that period already exists for this stokvel | none |
| 15 | GET /api/stokvels/{stokvelId}/cycles/{cycleId} | ids not GUIDs* | cycle not found, or it belongs to a different stokvel | none | none |
| 16 | PUT /api/stokvels/{stokvelId}/cycles/{cycleId} | ids not GUIDs*; same 3 body rules as create | cycle not found, or it belongs to a different stokvel | none (see finding 2) | none |
| 17 | DELETE /api/stokvels/{stokvelId}/cycles/{cycleId} | ids not GUIDs* | cycle not found, or it belongs to a different stokvel | none | none |

