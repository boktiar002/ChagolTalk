START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901060024_AddUniqueReportPerConversation') THEN
    CREATE UNIQUE INDEX "IX_Reports_ReporterId_ConversationId" ON "Reports" ("ReporterId", "ConversationId") WHERE "ConversationId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260901060024_AddUniqueReportPerConversation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260901060024_AddUniqueReportPerConversation', '10.0.10');
    END IF;
END $EF$;
COMMIT;

