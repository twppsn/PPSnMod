﻿CREATE TABLE [dbo].[ObjT]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkObjTId PRIMARY KEY CLUSTERED IDENTITY (1, 1),
	[ObjKId] BIGINT NOT NULL CONSTRAINT fkObjtObjkId REFERENCES dbo.ObjK ([Id]) ON DELETE CASCADE,
	[ObjRId] BIGINT NULL CONSTRAINT fkObjtObjrId REFERENCES dbo.ObjR ([Id]) ON DELETE CASCADE,
	[Key] NVARCHAR(200) NOT NULL,
	[Class] INTEGER NOT NULL DEFAULT 0,
	[Value] NVARCHAR(2048) NULL,
	[UserId] BIGINT NULL CONSTRAINT fkObjTUserId REFERENCES dbo.[User] (Id),
	[SyncToken] BIGINT NOT NULL DEFAULT 0
	CONSTRAINT uqObjkIdKey UNIQUE ([ObjKId], [Key])
);
GO

CREATE INDEX [idxObjtObjkId] ON [dbo].[ObjT] ([ObjKId])
GO
CREATE INDEX [idxObjtObjrId] ON [dbo].[ObjT] ([ObjRId])
GO
CREATE INDEX [idxObjtUserId] ON [dbo].[ObjT] ([UserId])
GO

EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Tag name',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjT',
    @level2type = N'COLUMN',
    @level2name = N'Key'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'class of the tag defined in DataSet.cs:PpsObjectTagClass',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjT',
    @level2type = N'COLUMN',
    @level2name = N'Class'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Value of the tag or null for tags',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjT',
    @level2type = N'COLUMN',
    @level2name = N'Value'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Tag creator null for system tags',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjT',
    @level2type = N'COLUMN',
    @level2name = N'UserId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Last change of the tag',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjT',
    @level2type = N'COLUMN',
    @level2name = N'SyncToken'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK to the object',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjT',
    @level2type = N'COLUMN',
    @level2name = N'ObjKId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK to the revision',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjT',
    @level2type = N'COLUMN',
    @level2name = N'ObjRId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjT',
    @level2type = N'COLUMN',
    @level2name = N'Id'