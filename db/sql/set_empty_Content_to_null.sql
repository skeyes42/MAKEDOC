UPDATE Node
SET Content = NULL
WHERE typeof(Content) <> 'blob' OR Content IS '';
