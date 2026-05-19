.mode csv
PRAGMA foreign_keys = OFF;
PRAGMA ignore_check_constraints = ON;
.print *** LOADING CSV FROM: ../seed/seed_Node_table.csv ***
DELETE FROM Node;
.import --skip 1 ../seed/seed_Node_table.csv Node
PRAGMA foreign_keys = ON;