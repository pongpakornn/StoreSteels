-- Run this ONCE against the Stock DB (StoreSteels / GlobalConfig.ConnStr) - NOT the ERP (CHR) database.
-- Keeps a print history for the Packing Card page so a ticket/item that has already been printed
-- drops out of the pending list (see PackingCardErpService.GetPendingPackingCards).

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PackingPrintLog' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.PackingPrintLog (
        PrintID         INT IDENTITY(1,1) PRIMARY KEY,
        TicketNo        VARCHAR(20)     NOT NULL,
        ItemNo          INT             NOT NULL,
        Warehouse       VARCHAR(10)     NULL,
        LotNo           VARCHAR(30)     NULL,
        MaterialCode    VARCHAR(30)     NOT NULL,
        WorkOrder       VARCHAR(30)     NULL,
        TicketDate      DATE            NULL,
        JobName         NVARCHAR(200)   NULL,
        Qty             DECIMAL(18,2)   NOT NULL,
        PrintedBy       VARCHAR(50)     NOT NULL,
        PrintedDate     DATETIME        NOT NULL DEFAULT GETDATE(),
        LabelRef        VARCHAR(200)    NULL,
        CONSTRAINT UQ_PackingPrintLog UNIQUE (TicketNo, ItemNo)
    );

    CREATE INDEX IX_PackingPrintLog_Ticket ON dbo.PackingPrintLog (TicketNo);
END
