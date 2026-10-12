/**
 * Simple subsequence-based fuzzy match (same idea as VS Code's command palette filter): true when
 * every character of `query` appears in `text`, in order, case-insensitively. An empty query
 * matches everything.
 */
export function fuzzyMatch(query: string, text: string): boolean {
  return fuzzyMatchIndices(query, text) !== null
}

/**
 * Same subsequence match as {@link fuzzyMatch}, but also returns *which* character indices in
 * `text` satisfied the query — a fuzzy match isn't a single contiguous substring, so highlighting
 * "what matched" means highlighting each of these (possibly scattered) characters, not a range.
 * Returns null when there's no match, or an empty array for an empty query (matches everything,
 * nothing to highlight).
 */
export function fuzzyMatchIndices(query: string, text: string): number[] | null {
  if (!query.trim()) return []

  const q = query.toLowerCase()
  const t = text.toLowerCase()
  const indices: number[] = []
  let qi = 0
  for (let ti = 0; ti < t.length && qi < q.length; ti++) {
    if (t[ti] === q[qi]) {
      indices.push(ti)
      qi++
    }
  }
  return qi === q.length ? indices : null
}
