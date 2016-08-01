﻿CREATE TABLE [dbo].[Knst]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkKnstId PRIMARY KEY IDENTITY (1,1),
	[Typ] NVARCHAR(4) NOT NULL,
	[IsActive] BIT NOT NULL DEFAULT 0,
	[Sync] DATETIME2 NOT NULL DEFAULT SysUtcDateTime()
)