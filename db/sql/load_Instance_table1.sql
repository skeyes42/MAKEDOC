.mode csv
PRAGMA foreign_keys = OFF;
PRAGMA ignore_check_constraints = ON;
DELETE FROM Instance;
.import --skip 1 seed_Instance_table.csv Instance
PRAGMA foreign_keys = ON;