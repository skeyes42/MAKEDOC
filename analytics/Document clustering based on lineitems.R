###############################################################
#  Procurement Document Clustering (Low / Medium / High Dollar)
#  Using quanteda + TF-IDF + Cosine Similarity + Clustering
###############################################################

# Required packages
suppressPackageStartupMessages({
  library(docxtractr)
  library(quanteda)
  library(quanteda.textstats)
  library(pheatmap)
})

###############################################################
# 1. Define file paths (EDIT THESE)
###############################################################

files <- list(
  low_req   = "C:/temp/raw_sample/low_req.docx",
  low_sol   = "C:/temp/raw_sample/low_sol.docx",
  low_award = "C:/temp/raw_sample/low_award.docx",

  med_req   = "C:/temp/raw_sample/med_req.docx",
  med_sol   = "C:/temp/raw_sample/med_sol.docx",
  med_award = "C:/temp/raw_sample/med_award.docx",

  high_req   = "C:/temp/raw_sample/high_req.docx",
  high_sol   = "C:/temp/raw_sample/high_sol.docx",
  high_award = "C:/temp/raw_sample/high_award.docx"
)

###############################################################
# 2. Function to extract line-item text from DOCX
#    Assumes line items are in tables (common in procurement)
###############################################################

extract_line_items <- function(path) {
  doc <- docxtractr::read_docx(path)
  tables <- docxtractr::docx_extract_all_tbls(doc)

  if (length(tables) == 0) {
    warning(paste("No tables found in:", path))
    return("")
  }

  # Convert all tables to text
  table_text <- lapply(tables, function(tbl) {
    apply(tbl, 1, paste, collapse = " ") |> paste(collapse = "/n")
  })

  paste(unlist(table_text), collapse = "/n")
}

###############################################################
# 3. Extract line-item text for all nine documents
###############################################################

docs <- lapply(files, extract_line_items)
names(docs) <- names(files)

###############################################################
# 4. Build quanteda corpus
###############################################################

corp <- corpus(docs)

###############################################################
# 5. Tokenize and clean
###############################################################

toks <- tokens(
  corp,
  remove_punct = TRUE,
  remove_symbols = TRUE,
  remove_numbers = FALSE   # keep numbers if they matter
) |>
  tokens_tolower() |>
  tokens_remove(stopwords("en"))

###############################################################
# 6. Build DFM and apply TF-IDF
###############################################################

dfm_raw <- dfm(toks)
dfm_trimmed <- dfm_trim(dfm_raw, min_termfreq = 2)
dfm_tfidf <- dfm_tfidf(dfm_trimmed)

###############################################################
# 7. Compute cosine similarity
###############################################################

sim <- textstat_simil(dfm_tfidf, method = "cosine")
sim_matrix <- as.matrix(sim)

###############################################################
# 8. Hierarchical clustering
###############################################################

dist_matrix <- as.dist(1 - sim_matrix)
hc <- hclust(dist_matrix, method = "ward.D2")

# Plot dendrogram
plot(hc, main = "Document Clustering (Line-Item TF-IDF)", xlab = "", sub = "")

###############################################################
# 9. Heatmap of similarity matrix
###############################################################

pheatmap(
  sim_matrix,
  clustering_distance_rows = dist_matrix,
  clustering_distance_cols = dist_matrix,
  main = "Cosine Similarity Heatmap (Line-Item TF-IDF)"
)

###############################################################
# 10. Print similarity matrix for inspection
###############################################################

print(round(sim_matrix, 3))
