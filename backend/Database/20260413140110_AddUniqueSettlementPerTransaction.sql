BEGIN TRANSACTION;
;WITH DuplicateSettlements AS (
    SELECT
        Id,
        ROW_NUMBER() OVER (
            PARTITION BY TransactionId
            ORDER BY CreatedAt ASC, Id ASC
        ) AS rn
    FROM Settlements
)
DELETE FROM Settlements
WHERE Id IN (
    SELECT Id
    FROM DuplicateSettlements
    WHERE rn > 1
);

DROP INDEX [IX_Settlements_TransactionId] ON [Settlements];

CREATE UNIQUE INDEX [IX_Settlements_TransactionId] ON [Settlements] ([TransactionId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260413140110_AddUniqueSettlementPerTransaction', N'10.0.5');

COMMIT;
GO

