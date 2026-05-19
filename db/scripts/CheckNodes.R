# 1. Load necessary libraries
# install.packages(c("DBI", "RSQLite")) # Uncomment if not installed
library(DBI)
library(RSQLite)
suppressPackageStartupMessages(
  suppressWarnings(
    library(readr)
  )
)

# 2. Connect to the MAKEDOC.db database
# Ensure the .db file is in your current working directory
con <- dbConnect(RSQLite::SQLite(), "../MAKEDOC.db")

# 3. Define the SQL query
# This looks for cases where Content is NULL or contains an empty string
query <- "
  SELECT * 
  FROM Node 
  WHERE Content IS NULL OR Content = ''
"

# 4. Execute the query and fetch results into a data frame
empty_nodes <- dbGetQuery(con, query)

# 5. Review the results
if (nrow(empty_nodes) > 0) {
  print(paste("Found", nrow(empty_nodes), "nodes with empty content:"))
  print(empty_nodes)
  dbDisconnect(con)
  quit(status = 1)
} else {
  print("No nodes with empty or NULL content were found.")
}

# 6. Disconnect from the database
dbDisconnect(con)
quit(status = 0)
