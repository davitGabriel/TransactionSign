SET NOCOUNT ON;

-- Get required signatures setting
DECLARE @Required INT = (
    SELECT TOP 1 TRY_CONVERT(INT, Value)
    FROM SiteSettings
    WHERE [Name] IN ('RequiredSignatures', 'NumberOfRequiredAmSignatures')
);
IF @Required IS NULL SET @Required = 3;

-- Count totals for summary
DECLARE @TotalTx INT, @CompletedTx INT, @IssueCount INT;

SELECT @TotalTx = COUNT(*) FROM Transactions WHERE [Source] != 5;
SELECT @CompletedTx = COUNT(*) FROM Transactions WHERE [Source] != 5 AND Status = 2;

SELECT @IssueCount = COUNT(*)
FROM Transactions t
LEFT JOIN (SELECT TransactionId, COUNT(*) AS Cnt, COUNT(DISTINCT UserId) AS Unique_Cnt FROM Signatures GROUP BY TransactionId) s ON s.TransactionId = t.Id
LEFT JOIN (SELECT TransactionId, COUNT(*) AS Cnt FROM TransactionFinalizations GROUP BY TransactionId) f ON f.TransactionId = t.Id
LEFT JOIN (SELECT TransactionId, COUNT(*) AS Cnt FROM Settlements GROUP BY TransactionId) st ON st.TransactionId = t.Id
LEFT JOIN (SELECT TRY_CONVERT(INT, REPLACE(Reason, 'Fee for transaction ', '')) AS ParentId, COUNT(*) AS Cnt FROM Transactions WHERE [Source] = 5 GROUP BY Reason) fee ON fee.ParentId = t.Id
WHERE t.[Source] != 5 AND (s.Cnt != s.Unique_Cnt OR f.Cnt > 1 OR st.Cnt > 1 OR fee.Cnt > 1 OR (f.Cnt = 1 AND (st.Cnt != 1 OR fee.Cnt != 1 OR t.Status != 2)));

-- Report header
PRINT '============================================';
PRINT '       DATABASE VALIDATION REPORT';
PRINT '============================================';
PRINT '';
PRINT 'SUMMARY';
PRINT '-------';
PRINT '  Total Transactions:    ' + CAST(@TotalTx AS VARCHAR);
PRINT '  Completed:             ' + CAST(@CompletedTx AS VARCHAR);
PRINT '  Pending:               ' + CAST(@TotalTx - @CompletedTx AS VARCHAR);
PRINT '  Issues Found:          ' + CAST(@IssueCount AS VARCHAR);
PRINT '  Required Signatures:   ' + CAST(@Required AS VARCHAR);
PRINT '';
PRINT '  Result:                ' + CASE WHEN @IssueCount = 0 THEN 'PASS' ELSE 'FAIL' END;
PRINT '';
PRINT '============================================';
PRINT '       TRANSACTION DETAILS';
PRINT '============================================';
PRINT '';
PRINT '  TxId     Amount       Status     Sigs  Finalized  Settled  Fee';
PRINT '  ----     ------       ------     ----  ---------  -------  ---';

-- Transaction details using cursor for formatted output
DECLARE @Id INT, @Amount DECIMAL(18,2), @Status INT, @Sigs INT, @Signers INT, @Fin INT, @Set INT, @Fee INT;
DECLARE @Line NVARCHAR(200);

DECLARE tx_cursor CURSOR FOR
SELECT
    t.Id,
    t.Amount,
    t.Status,
    ISNULL(s.Cnt, 0),
    ISNULL(s.Unique_Cnt, 0),
    ISNULL(f.Cnt, 0),
    ISNULL(st.Cnt, 0),
    ISNULL(fee.Cnt, 0)
FROM Transactions t
LEFT JOIN (
    SELECT TransactionId, COUNT(*) AS Cnt, COUNT(DISTINCT UserId) AS Unique_Cnt
    FROM Signatures GROUP BY TransactionId
) s ON s.TransactionId = t.Id
LEFT JOIN (
    SELECT TransactionId, COUNT(*) AS Cnt
    FROM TransactionFinalizations GROUP BY TransactionId
) f ON f.TransactionId = t.Id
LEFT JOIN (
    SELECT TransactionId, COUNT(*) AS Cnt
    FROM Settlements GROUP BY TransactionId
) st ON st.TransactionId = t.Id
LEFT JOIN (
    SELECT TRY_CONVERT(INT, REPLACE(Reason, 'Fee for transaction ', '')) AS ParentId, COUNT(*) AS Cnt
    FROM Transactions WHERE [Source] = 5 GROUP BY Reason
) fee ON fee.ParentId = t.Id
WHERE t.[Source] != 5
ORDER BY t.Id;

OPEN tx_cursor;
FETCH NEXT FROM tx_cursor INTO @Id, @Amount, @Status, @Sigs, @Signers, @Fin, @Set, @Fee;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Line = '  ' +
        RIGHT('    ' + CAST(@Id AS VARCHAR), 4) + '   ' +
        RIGHT('          ' + CAST(CAST(@Amount AS INT) AS VARCHAR), 10) + '   ' +
        LEFT(CASE @Status WHEN 0 THEN 'Pending' WHEN 1 THEN 'Partial' WHEN 2 THEN 'Complete' ELSE '?' END + '       ', 8) + '   ' +
        RIGHT('  ' + CAST(@Sigs AS VARCHAR), 2) + '    ' +
        LEFT(CASE WHEN @Fin > 0 THEN 'Yes' ELSE 'No' END + '       ', 7) + '    ' +
        LEFT(CASE WHEN @Set > 0 THEN 'Yes' ELSE 'No' END + '     ', 5) + '    ' +
        CASE WHEN @Fee > 0 THEN 'Yes' ELSE 'No' END;
    PRINT @Line;
    FETCH NEXT FROM tx_cursor INTO @Id, @Amount, @Status, @Sigs, @Signers, @Fin, @Set, @Fee;
END

CLOSE tx_cursor;
DEALLOCATE tx_cursor;

-- Issues section
PRINT '';
PRINT '============================================';
PRINT '       ISSUES';
PRINT '============================================';

IF @IssueCount = 0
BEGIN
    PRINT '';
    PRINT '  No issues detected.';
END
ELSE
BEGIN
    PRINT '';
    PRINT '  TxId   Issue';
    PRINT '  ----   -----';

    DECLARE @IssueText NVARCHAR(100);

    DECLARE issue_cursor CURSOR FOR
    SELECT
        t.Id,
        CASE
            WHEN s.Cnt != s.Unique_Cnt THEN 'Duplicate user signatures'
            WHEN f.Cnt > 1 THEN 'Multiple finalizations'
            WHEN st.Cnt > 1 THEN 'Multiple settlements'
            WHEN fee.Cnt > 1 THEN 'Multiple fees'
            WHEN f.Cnt = 1 AND st.Cnt != 1 THEN 'Finalized but no settlement'
            WHEN f.Cnt = 1 AND fee.Cnt != 1 THEN 'Finalized but no fee'
            WHEN f.Cnt = 1 AND t.Status != 2 THEN 'Finalized but wrong status'
        END
    FROM Transactions t
    LEFT JOIN (
        SELECT TransactionId, COUNT(*) AS Cnt, COUNT(DISTINCT UserId) AS Unique_Cnt
        FROM Signatures GROUP BY TransactionId
    ) s ON s.TransactionId = t.Id
    LEFT JOIN (
        SELECT TransactionId, COUNT(*) AS Cnt
        FROM TransactionFinalizations GROUP BY TransactionId
    ) f ON f.TransactionId = t.Id
    LEFT JOIN (
        SELECT TransactionId, COUNT(*) AS Cnt
        FROM Settlements GROUP BY TransactionId
    ) st ON st.TransactionId = t.Id
    LEFT JOIN (
        SELECT TRY_CONVERT(INT, REPLACE(Reason, 'Fee for transaction ', '')) AS ParentId, COUNT(*) AS Cnt
        FROM Transactions WHERE [Source] = 5 GROUP BY Reason
    ) fee ON fee.ParentId = t.Id
    WHERE t.[Source] != 5
      AND (
        s.Cnt != s.Unique_Cnt OR
        f.Cnt > 1 OR st.Cnt > 1 OR fee.Cnt > 1 OR
        (f.Cnt = 1 AND (st.Cnt != 1 OR fee.Cnt != 1 OR t.Status != 2))
      );

    OPEN issue_cursor;
    FETCH NEXT FROM issue_cursor INTO @Id, @IssueText;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        PRINT '  ' + RIGHT('    ' + CAST(@Id AS VARCHAR), 4) + '   ' + @IssueText;
        FETCH NEXT FROM issue_cursor INTO @Id, @IssueText;
    END

    CLOSE issue_cursor;
    DEALLOCATE issue_cursor;
END

PRINT '';
PRINT '============================================';
