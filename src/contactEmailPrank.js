/**
 * Espelha CheckoutContactEmailTransform (API) — só troca o domínio após @.
 */
export function transformContactEmailPrank(rawEmail) {
  const t = String(rawEmail).trim()
  const at = t.lastIndexOf('@')
  if (at < 1 || at >= t.length - 1) return t

  const local = t.slice(0, at)
  const domain = t
    .slice(at + 1)
    .trim()
    .toLowerCase()

  const domainMap = {
    'gmail.com': 'hormail.com',
    'hotmail.com': 'gailmail.com',
    'live.com': 'hormail.com',
    'outlook.com': 'gormail.com',
    'yahoo.com.br': 'yahu.com.br',
    'yahoo.com': 'yahu.com',
    'icloud.com': 'icloud.co',
    'uol.com.br': 'uol.co.br',
  }
  const newDomain = domainMap[domain] ?? 'hormail.com'

  return `${local}@${newDomain}`
}
