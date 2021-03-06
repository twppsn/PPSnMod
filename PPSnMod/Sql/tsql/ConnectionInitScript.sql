﻿-- user objects aka tables
SELECT
		u.object_id
		, s.name
		, u.name
		, ic.column_id
	FROM sys.objects u
		INNER JOIN sys.schemas s ON (u.schema_id = s.schema_id)
		LEFT OUTER JOIN sys.indexes pk ON (u.object_id = pk.object_id and pk.is_primary_key = 1)
		LEFT OUTER JOIN sys.index_columns ic ON (pk.object_id = ic.object_id and pk.index_id = ic.index_id)
	WHERE u.type = 'U';

-- user columns
SELECT
		c.object_id
		, c.column_id
		, c.name
		, c.system_type_id
		, cast(case when c.system_type_id in (231, 239) then c.max_length / 2 else c.max_length end as smallint)
		, c.precision
		, c.scale
		, c.is_nullable
		, c.is_identity
	FROM sys.columns c
		INNER JOIN sys.objects t ON (c.object_id = t.object_id)
	WHERE t.type = 'U' and c.is_computed = 0
	order by c.object_id, c.column_id;
-- foreign keys
SELECT 
		o.object_id
		, o.name
		, f.parent_object_id
		, f.parent_column_id
		, f.referenced_object_id
		, f.referenced_column_id
	FROM sys.objects o 
		INNER JOIN sys.foreign_key_columns f ON (o.object_id = f.constraint_object_id) 
;	
