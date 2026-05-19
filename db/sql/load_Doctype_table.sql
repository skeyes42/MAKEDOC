.mode csv
PRAGMA foreign_keys = OFF;
PRAGMA ignore_check_constraints = ON;
DELETE FROM Doctype;
.import --skip 1 ../seed/seed_Doctype_table.csv Doctype
PRAGMA foreign_keys = ON;