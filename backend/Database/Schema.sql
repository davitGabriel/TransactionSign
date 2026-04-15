-- Transaction Signing System Database Schema (aligned with sample workbook)
-- SQL Server

-- Transactions table
CREATE TABLE Transactions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ParentId INT NULL,
    Source INT NOT NULL,                 -- 4 = Credit, 5 = Fee
    BeneficiaryName NVARCHAR(200) NOT NULL,
    Reason NVARCHAR(500) NULL,
    IsDebit BIT NOT NULL,
    CreateDate DATETIME2 NOT NULL,
    LastModifyDate DATETIME2 NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CurrencyId NVARCHAR(10) NOT NULL,
    Status INT NOT NULL,                 -- 6 = Internal, 2 = Completed
    SenderName NVARCHAR(200) NOT NULL,
    Note NVARCHAR(500) NULL,
    AgentId INT NULL,
    ValueDate DATETIME2 NOT NULL,
    InternalStatus INT NOT NULL          -- 2 = ToSign, 5 = Completed
);

-- Signatures table with UNIQUE constraint for concurrency safety
CREATE TABLE Signatures (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TransactionId INT NOT NULL,
    UserId INT NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    CONSTRAINT FK_Signatures_Transactions FOREIGN KEY (TransactionId) REFERENCES Transactions(Id),
    CONSTRAINT UQ_Signatures_TransactionUser UNIQUE (TransactionId, UserId)
);

-- TransactionFinalizations table with UNIQUE constraint for exactly-once finalization
CREATE TABLE TransactionFinalizations (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TransactionId INT NOT NULL,
    FinalizedAt DATETIME2 NOT NULL,
    CONSTRAINT FK_Finalizations_Transactions FOREIGN KEY (TransactionId) REFERENCES Transactions(Id),
    CONSTRAINT UQ_Finalizations_Transaction UNIQUE (TransactionId)
);

-- Settlements table (one settlement per transaction)
CREATE TABLE Settlements (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TransactionId INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    CONSTRAINT FK_Settlements_Transactions FOREIGN KEY (TransactionId) REFERENCES Transactions(Id),
    CONSTRAINT UQ_Settlements_Transaction UNIQUE (TransactionId)
);

-- SiteSettings table
CREATE TABLE SiteSettings (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Value NVARCHAR(500) NOT NULL,
    CONSTRAINT UQ_SiteSettings_Name UNIQUE (Name)
);

-- Seed required signatures setting
INSERT INTO SiteSettings (Name, Value) VALUES ('NumberOfRequiredAmSignatures', '2');

-- Sample transactions (not finalized: Status=6, InternalStatus=2)
INSERT INTO Transactions
    (ParentId, Source, BeneficiaryName, Reason, IsDebit, CreateDate, LastModifyDate, Amount, CurrencyId, Status, SenderName, Note, AgentId, ValueDate, InternalStatus)
VALUES
    (NULL, 4, 'XYZ Ltd',      'Invoice #1001', 0, '2024-01-15', '2024-01-15',  5000.00, 'EUR', 6, 'ABC Corp', NULL, NULL, '2024-01-15', 2),
    (NULL, 4, 'Partner Inc',  'Q1 Payment',    0, '2024-01-16', '2024-01-16', 25000.00, 'EUR', 6, 'ABC Corp', NULL, NULL, '2024-01-16', 2),
    (NULL, 4, 'Vendor Co',    'Services',      0, '2024-01-17', '2024-01-17', 75000.00, 'EUR', 6, 'ABC Corp', NULL, NULL, '2024-01-17', 2),
    (NULL, 4, 'Customer A',   'Refund',        0, '2024-01-18', '2024-01-18',  1500.00, 'EUR', 6, 'ABC Corp', NULL, NULL, '2024-01-18', 2);
