# =============================================================================
# cluster_docs.R
# Cluster DOCX files using TF-IDF + hierarchical clustering (Ward / cosine)
# Output: cosine-similarity heatmap PNG
#
# Required packages: officer, tm, SnowballC, pheatmap
# =============================================================================

# --- 0. Install packages if missing ------------------------------------------
required_packages <- c("officer", "tm", "SnowballC", "pheatmap")
missing <- required_packages[!vapply(required_packages,
                                     requireNamespace, logical(1), quietly = TRUE)]
if (length(missing) > 0) {
  message("Installing: ", paste(missing, collapse = ", "))
  install.packages(missing, repos = "https://cloud.r-project.org")
}

library(officer)
library(tm)
library(SnowballC)
library(pheatmap)


# --- 1. Configuration --------------------------------------------------------

# Folder containing your .docx files (edit this path)
DOC_DIR      <- "C:/temp/raw_sample"

# Output files
OUT_HEATMAP  <- "similarity_heatmap.png"

# Sparsity threshold for term pruning (0–1).
# 0.95 keeps terms that appear in at least 5% of documents.
# Lower the value (e.g. 0.90) if the vocabulary is still very large.
SPARSE_CUTOFF <- 0.95

# Ward linkage is robust for document data; alternatives: "complete", "average"
LINKAGE_METHOD <- "ward.D2"


# --- 2. Extract text from every .docx ----------------------------------------

docx_files <- list.files(DOC_DIR,
                         pattern    = "\\.docx$",
                         full.names = TRUE,
                         ignore.case = TRUE)

if (length(docx_files) == 0) {
  stop("No .docx files found in: ", normalizePath(DOC_DIR))
}

extract_text <- function(path) {
  doc <- tryCatch(read_docx(path), error = function(e) {
    warning("Could not read: ", basename(path))
    return(NULL)
  })
  if (is.null(doc)) return("")
  df <- docx_summary(doc)
  paragraphs <- df[df$content_type == "paragraph", "text", drop = TRUE]
  paste(paragraphs, collapse = " ")
}

message("Reading ", length(docx_files), " document(s) ...")
texts     <- vapply(docx_files, extract_text, character(1))
doc_names <- tools::file_path_sans_ext(basename(docx_files))
names(texts) <- doc_names

# Drop any documents whose text could not be extracted
empty <- nchar(trimws(texts)) == 0
if (any(empty)) {
  warning("Skipping ", sum(empty), " empty/unreadable document(s): ",
          paste(doc_names[empty], collapse = ", "))
  texts     <- texts[!empty]
  doc_names <- doc_names[!empty]
}

if (length(texts) < 2) {
  stop("At least 2 readable documents are required for clustering.")
}

message(length(texts), " document(s) loaded.")


# --- 3. Build TF-IDF document-term matrix ------------------------------------

corpus <- VCorpus(VectorSource(unname(texts)))

# Standard preprocessing pipeline
corpus <- tm_map(corpus, content_transformer(tolower))
corpus <- tm_map(corpus, removePunctuation)
corpus <- tm_map(corpus, removeNumbers)
corpus <- tm_map(corpus, removeWords, stopwords("en"))
corpus <- tm_map(corpus, stripWhitespace)
corpus <- tm_map(corpus, stemDocument)         # Porter stemmer via SnowballC

# Build weighted DTM; drop terms that appear in only one document
dtm <- DocumentTermMatrix(corpus, control = list(
  weighting = weightTfIdf,
  bounds    = list(global = c(2, Inf))
))

# Remove very sparse terms
dtm <- removeSparseTerms(dtm, sparse = SPARSE_CUTOFF)

message("Vocabulary after pruning: ", ncol(dtm), " term(s).")

if (ncol(dtm) == 0) {
  stop("No terms survived pruning. Try raising SPARSE_CUTOFF (e.g. to 0.99).")
}

mat           <- as.matrix(dtm)
rownames(mat) <- doc_names


# --- 4. Cosine distance + hierarchical clustering ----------------------------

cosine_distance <- function(m) {
  norms <- sqrt(rowSums(m^2))
  norms[norms == 0] <- 1e-10          # avoid division by zero
  sim <- (m / norms) %*% t(m / norms)
  sim <- pmin(pmax(sim, -1), 1)       # numerical clamp
  as.dist(1 - sim)
}

d  <- cosine_distance(mat)
hc <- hclust(d, method = LINKAGE_METHOD)


# --- 5. Similarity heatmap ---------------------------------------------------

# Convert distance matrix back to a similarity matrix for display
sim_mat <- as.matrix(1 - d)              # cosine similarity, 0–1
diag(sim_mat) <- 1                       # ensure exact 1 on diagonal

# Scale PNG so row/column labels are readable
cell_size  <- max(14, 600 / length(doc_names))   # pixels per cell
plot_dim   <- max(800, length(doc_names) * cell_size + 200)

# Blue-white-red palette: white = 0 similarity, deep red = identical
colors <- colorRampPalette(c("#2166ac", "#f7f7f7", "#b2182b"))(100)

pheatmap(
  sim_mat,
  color            = colors,
  breaks           = seq(0, 1, length.out = 101),
  clustering_distance_rows = as.dist(1 - sim_mat),
  clustering_distance_cols = as.dist(1 - sim_mat),
  clustering_method        = LINKAGE_METHOD,
  display_numbers  = nrow(sim_mat) <= 20,   # show values only for small sets
  number_format    = "%.2f",
  number_color     = "black",
  fontsize         = 9,
  fontsize_row     = 8,
  fontsize_col     = 8,
  angle_col        = 45,
  main             = paste0("Document Cosine-Similarity Heatmap\n",
                            "Clustering: Ward.D2  |  n = ",
                            length(doc_names), " docs"),
  filename         = OUT_HEATMAP,
  width            = plot_dim / 72,         # pheatmap uses inches @ 72 dpi
  height           = plot_dim / 72
)
message("Heatmap saved to: ", normalizePath(OUT_HEATMAP))


# --- 6. Optional: print cluster assignments at a chosen cut height -----------
#
# Uncomment and set K to the number of clusters you want, then run this block:
#
# K <- 3
# clusters <- cutree(hc, k = K)
# assignment <- data.frame(
#   document = names(clusters),
#   cluster  = clusters,
#   row.names = NULL
# )
# assignment <- assignment[order(assignment$cluster), ]
# print(assignment)
# write.csv(assignment, "cluster_assignments.csv", row.names = FALSE)
# message("Cluster assignments saved to cluster_assignments.csv")

message("Done.")
