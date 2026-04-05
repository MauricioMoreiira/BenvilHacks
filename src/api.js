export function apiPath(path) {
  const base = import.meta.env.VITE_API_URL?.replace(/\/$/, '') ?? ''
  return base ? `${base}${path}` : path
}
