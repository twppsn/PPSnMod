﻿CREATE TABLE [dbo].[Lief]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkLiefId PRIMARY KEY IDENTITY (1,1), 
	[KontId] BIGINT NOT NULL CONSTRAINT fkLiefKontId REFERENCES dbo.Kont (Id), 
	[Name] NVARCHAR(100) NOT NULL, 
	[KurzName] NVARCHAR(25) NULL,
	[KundNr] NVARCHAR(50) NULL, 
	[StIdentNr] VARCHAR(25) NULL, 
	[SteuerNr] VARCHAR(25) NULL, 
	[UstIdNr] CHAR(16) NULL, 
	[Inaktiv] SMALLDATETIME NULL,
	[Abc] CHAR NULL, 
	[KgrpId] BIGINT NULL CONSTRAINT fkLiefKgrpId REFERENCES dbo.Kgrp (Id), 
	[Iban] CHAR(34) NULL, 
	[Bic] CHAR(11) NULL
)
GO
ALTER TABLE [dbo].[Lief] ENABLE CHANGE_TRACKING;
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Objk',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = N'KontId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Name des Liefranten',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = N'Name'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'ext. Kundennummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = N'KundNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Steuerliche Identifikationsnummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = 'StIdentNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Steuernummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = N'SteuerNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Mehrwertsteuer-Identifikationsnummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = 'UstIdNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Inaktiv seit',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = N'Inaktiv'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Abc-Einteilung',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = N'Abc'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Gruppe',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = N'KgrpId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Iban-Nummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = N'Iban'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Bic-Nummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = N'Bic'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Kurzname des Lieferanten, MatchCode',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = N'KurzName'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Lief',
    @level2type = N'COLUMN',
    @level2name = N'Id'