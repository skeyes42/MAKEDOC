###############################################################
#  Procurement Document Clustering (Scalable Version)
#  - Automatic folder scanning
#  - Parallel DOCX extraction via direct XML parsing (xml2)
#  - TF-IDF + Cosine Similarity
#  - K-means clustering
#  - UMAP visualization
###############################################################

suppressPackageStartupMessages({
  library(xml2)
  library(dplyr)
  library(quanteda)
  library(quanteda.textstats)
  library(uwot)          # UMAP
  library(ggplot2)
  library(parallel)      # parallel extraction
  library(tidyverse)
})

###############################################################
# 1. Automatically scan a folder for DOCX files
###############################################################

input_dir <- "C:/temp/raw_sample"   # <-- EDIT THIS

files <- list.files(
  input_dir,
  pattern    = "\\.docx$",
  full.names = TRUE,
  recursive  = FALSE
)

cat("Found", length(files), "DOCX files\n")

###############################################################
# 2. Extract function: direct XML parsing (bypasses docxtractr
#    table-recognition bug when <w:tcPr> is absent)
###############################################################

extract_line_items <- function(path) {
  if (!file.exists(path)) return("")

  # docx is a zip; extract word/document.xml to a temp dir
  tmp <- tempfile()
  on.exit(unlink(tmp, recursive = TRUE))

  result <- tryCatch({
    unzip(path, files = "word/document.xml", exdir = tmp)
    xml_path <- file.path(tmp, "word/document.xml")
    if (!file.exists(xml_path)) return("")

    doc <- xml2::read_xml(xml_path)
    ns  <- c(w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main")

    tbls <- xml2::xml_find_all(doc, "//w:tbl", ns)

    all_text <- character(0)

    for (tbl in tbls) {
      rows <- xml2::xml_find_all(tbl, ".//w:tr", ns)
      for (row in rows) {
        cells <- xml2::xml_find_all(row, ".//w:tc", ns)
        cell_texts <- vapply(cells, function(cell) {
          paste(
            xml2::xml_text(xml2::xml_find_all(cell, ".//w:t", ns)),
            collapse = ""
          )
        }, character(1))
        # Each table row becomes one space-separated line
        all_text <- c(all_text, paste(cell_texts, collapse = " "))
      }
    }

    if (length(all_text) == 0) "" else paste(all_text, collapse = "\n")
  },
  error = function(e) {
    warning("Could not parse: ", basename(path), " — ", conditionMessage(e))
    ""
  })

  result
}

###############################################################
# 3. Parallel DOCX extraction (Windows-safe)
###############################################################

num_cores <- max(1, detectCores() - 1)
cl <- makeCluster(num_cores)

clusterExport(cl, varlist = c("extract_line_items"))
clusterEvalQ(cl, library(xml2))

docs <- parLapply(cl, files, extract_line_items)
stopCluster(cl)

names(docs) <- basename(files)

###############################################################
# 4. Build quanteda corpus
###############################################################

corp <- corpus(unlist(docs))

###############################################################
# 5. Tokenize and clean
###############################################################

toks <- tokens(
  corp,
  remove_punct   = TRUE,
  remove_symbols = TRUE,
  remove_numbers = FALSE
) |>
  tokens_tolower() |>
  tokens_remove(stopwords("en"))

###############################################################
# 6. Build DFM and apply TF-IDF
###############################################################

dfm_raw     <- dfm(toks)
dfm_trimmed <- dfm_trim(dfm_raw, min_termfreq = 2)
dfm_tfidf   <- dfm_tfidf(dfm_trimmed)

###############################################################
# 7. Compute cosine similarity (optional but useful)
###############################################################

sim        <- textstat_simil(dfm_tfidf, method = "cosine")
sim_matrix <- as.matrix(sim)

options(width = 1000) # Prevents wrapping by allowing up to 1000 characters per line
print(sim_matrix)

###############################################################
# 8. K-means clustering (scales to thousands of docs)
###############################################################

set.seed(123)

k        <- 6   # adjust as needed
km       <- kmeans(dfm_tfidf, centers = k)
clusters <- km$cluster

###############################################################
# 9. UMAP 2-D visualization (Windows + uwot compatible)
###############################################################

set.seed(123)

dense_mat  <- as.matrix(dfm_tfidf)
n_neighbors <- max(2, min(5, nrow(dense_mat) - 1))

umap_emb <- umap(
  dense_mat,
  n_neighbors = n_neighbors,
  min_dist    = 0.1,
  metric      = "cosine"
)

umap_df <- data.frame(
  x       = umap_emb[, 1],
  y       = umap_emb[, 2],
  cluster = factor(clusters),
  doc     = names(docs)
)

ggplot(umap_df, aes(x, y, color = cluster, label = doc)) +
  geom_point(size = 3, alpha = 0.8) +
  theme_minimal() +
  labs(
    title = "UMAP Document Clustering (TF-IDF)",
    color = "Cluster"
  )

ggsave("clustering.png")

###############################################################
# 10. Export cluster assignments
###############################################################

cluster_table <- data.frame(
  document = names(docs),
  cluster  = clusters
)

write.csv(cluster_table, "document_clusters.csv", row.names = FALSE)

###############################################################
# 11. Print summary
###############################################################

print(cluster_table)

###############################################################
# 12. Top TF-IDF terms per cluster
###############################################################

text_df <- data.frame(
  doc     = names(docs),
  text    = unlist(docs),
  cluster = factor(clusters)
)

corp_cluster <- corpus(text_df, text_field = "text")

toks_cluster <- tokens(
  corp_cluster,
  remove_punct   = TRUE,
  remove_symbols = TRUE
) |>
  tokens_tolower() |>
  tokens_remove(stopwords("en"))

dfm_docs         <- dfm(toks_cluster)
dfm_cluster      <- dfm_group(dfm_docs, groups = text_df$cluster)
dfm_cluster_tfidf <- dfm_tfidf(dfm_cluster)

top_terms <- data.frame()

for (cl in levels(text_df$cluster)) {
  terms     <- topfeatures(dfm_cluster_tfidf[cl, ], n = 10)
  top_terms <- rbind(
    top_terms,
    data.frame(cluster = cl, term = names(terms), tfidf = terms)
  )
}

top_terms %>%
  group_by(cluster) %>%
  summarise(top_words = paste(term, collapse = ", ")) %>%
  print(n = Inf)
