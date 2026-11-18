
## Story 2224 implementation gaps

| Gap | Severity | Evidence |
|---|---|---|
| **Partial valid-row processing is not implemented.** | High | Preview blocks confirmation when any worksheet has blocking errors via `TenantDataImportFeature.cs` and `ImportDataPane.vue`. Valid rows are not imported independently. |
| **Foreign-key failures are blocking rather than warnings.** | High | Native split validation reports missing or mismatched transaction references as errors in `NativeYoFiSplitWorksheetHandler.cs`, rather than non-blocking warnings. |
| **Success reporting does not match the required aggregate summary.** | Medium | The UI reports per-worksheet created, updated, and skipped counts in `ImportDataPane.vue`. It does not report `"X records imported successfully, Y records failed"` and omits zero/nonzero failed counts. |
| **`Failed` is never populated.** | Medium | The result DTO documents `Failed` as always zero, and failed worksheet operations throw instead of returning a result (`TenantDataImportDto.cs`). |
| **CSV reports omit original data.** | Medium | The CSV contains worksheet, row, field, error code, and message, but no original cell value (`ImportDataPane.vue`). The issue DTO has no original-value field (`TenantDataImportDto.cs`). |
| **Formula rejection is incomplete in numeric fields.** | Medium | Text and date fields commonly check `HasFormula`, but decimal handling uses `TryGetValue` without an explicit formula check in `NativeYoFiTransactionWorksheetHandler.cs` and `OriginalYoFiTransactionWorksheetHandler.cs`. |
| **Persistence may commit worksheets independently.** | Medium | Transactions and splits call `SaveChangesAsync` separately. A later worksheet or persistence failure could leave earlier worksheets committed, particularly since the feature processes worksheets sequentially (`TenantDataImportFeature.cs`). |
| **Import logging lacks required identity and result detail.** | Medium | Logs include counts but not the importing actor, workspace key/name, entity types, or failed count (`TenantDataAdminController.cs`). The route tenant key is not passed into the log methods. |

## Implemented portions

- Worksheet, row, field, code, and reason are presented during preview.
- Blocking validation occurs before persistence.
- Formula-safe CSV escaping is implemented for issue values.
- XLSX content and size restrictions are enforced.
- Tenant Owner authorization is enforced.
- `[LoggerMessage]` conventions are used at the controller boundary.

The design also explicitly notes that final partial-success and persistence-boundary behavior remains unresolved, so these gaps are likely intended follow-up work rather than completed Story 2224 behavior.
