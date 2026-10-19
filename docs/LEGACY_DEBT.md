# Legacy cleanup ledger

This file tracks legacy systems that are intentionally isolated rather than deleted blindly.

## Removed in this pass

- Removed the unused `CampaignSimulation.HasSave` indicator, which pointed at a separate campaign save file that active gameplay never uses.
- Removed standalone campaign disk `Save` / `Load` methods and the `campaign.json` path. Campaign JSON serialization remains only as a nested compatibility payload in the unified session envelope.
- Kept `GameSessionService` as the only active save/load entry point for a complete running session.

## Still legacy — do not extend

| Legacy surface | Why it remains | Removal gate |
|---|---|---|
| `CampaignState` DTO and schema-1 serializer | Existing session files embed it; migration still needs to parse it | Canonical world-ledger migration plus old-save fixtures and round-trip tests |
| `CampaignSimulation` turn/region/battle rules | Legacy checks and old-save state are still coupled to this class | Replace consumers with continuous world services; convert old tests to migration-only tests |
| `CampaignColonyBridge` instant resource transfers | Current local inventory and strategic inventory are separate authorities | One canonical resource ledger, travel-time shipments, conservation tests across save/load |
| Nested `CampaignJson` session field | Schema-v1/v2 compatibility currently requires it | Versioned migration that reads old envelopes and writes only the new world schema |
| Nine-region linked-hex generation and adjacency | Legacy save payload and tests still depend on region-shaped data | Deterministic importer from legacy data; prove new world generation has no hex dependency |

## Cleanup rules

1. Do not add new gameplay or UI features to legacy campaign code.
2. Do not delete legacy state before a migration fixture proves old saves still load without silent resource loss.
3. Prefer a one-way migration: read legacy schemas, normalize into the canonical world model, and write only the current schema.
4. Keep migration tests separate from tests for active continuous-world mechanics.
5. After each removal slice, run the root Release build and `--self-test` CI workflow before continuing.

