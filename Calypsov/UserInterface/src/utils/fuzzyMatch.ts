/**
 * Simple subsequence-based fuzzy match (same idea as VS Code's command palette filter): true when
 * every character of `query` appears in `text`, in order, case-insensitively. An empty query
 * matches everything.
 */
export function fuzzyMatch(query: string, text: string): boolean {
  if (!query.trim()) return true

  const q = query.toLowerCase()
  const t = text.toLowerCase()
  let qi = 0
  for (let ti = 0; ti < t.length && qi < q.length; ti++) {
    if (t[ti] === q[qi]) qi++
  }
  return qi === q.length
}
