library(readr)
library(dplyr)

csv_file <- "C:\\Users\\skeye\\BOOK2\\MAKEDOC\\db\\seed\\seed_NodeHierarchy_table.csv"

df <- read_csv(csv_file)

# Create parent and child dataframes with a single column named NodeID
parent_df <- df %>% 
  select(NodeID = ParentNodeID)

child_df <- df %>% 
  select(NodeID = ChildNodeID)

# Stack them
new_df <- bind_rows(parent_df, child_df)

print(new_df)

unique_df <- new_df %>% distinct(NodeID)

print(unique_df)
