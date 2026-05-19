.mode csv
PRAGMA foreign_keys = OFF;
PRAGMA ignore_check_constraints = ON;
DELETE FROM Instance;
.import --skip 1 ../seed/seed_NodeHierarchy_table.csv NodeHierarchy
PRAGMA foreign_keys = ON;