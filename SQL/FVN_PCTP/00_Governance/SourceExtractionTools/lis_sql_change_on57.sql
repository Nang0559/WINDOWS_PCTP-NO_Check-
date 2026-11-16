SELECT 
    SCHEMA_NAME(schema_id) AS SchemaName,
    name AS ObjectName,
    type_desc AS ObjectType,
    create_date AS CreatedDate,
    modify_date AS LastModifiedDate,
    CASE 
        WHEN create_date = modify_date THEN N'Tạo mới (Created)'
        ELSE N'Cập nhật/Thay đổi cấu trúc (Modified)'
    END AS ChangeStatus
FROM sys.objects
WHERE type IN (
    'U',  -- Table (Bảng người dùng)
    'V',  -- View
    'P',  -- Stored Procedure
    'FN', 'IF', 'TF', -- Function (Hàm)
    'PK', -- Primary Key (Khoá chính)
    'F',  -- Foreign Key (Khoá ngoại)
    'UQ', -- Unique Constraint
    'D'   -- Default Constraint
)
AND (
    create_date >= DATEADD(MONTH, -2, GETDATE()) 
    OR modify_date >= DATEADD(MONTH, -2, GETDATE())
)
AND is_ms_shipped = 0 -- Loại bỏ các object hệ thống của Microsoft
ORDER BY modify_date DESC;