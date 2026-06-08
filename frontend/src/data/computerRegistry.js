/**
 * Computer Registry
 *
 * Maps computer hostnames to assigned users.
 * When the backend is ready, this lookup will be replaced by an API call:
 *   GET /api/auth/resolve-computer?hostname=<hostname>
 * which queries the DB for the computer → WWID mapping, then returns the user profile.
 *
 * Access roles:
 *   'admin'    – full access
 *   'operator' – production pages only
 *   'viewer'   – read-only
 */

export const computerRegistry = {
  // hostname (lowercase) : user profile
  'asus': {
    wwid:       'OD741',
    name:       'Sarfaraz Ahmed',
    department: 'Production',
    shift:      'A',
    role:       'admin',
  },

  // ── Add more computers here when the DB is ready ──
  // 'tcl-pc-002': {
  //   wwid: 'OD742', name: 'John Doe', department: 'Quality', shift: 'B', role: 'operator'
  // },
}

/** Fallback used when the hostname is not in the registry */
export const unknownUser = {
  wwid:       'UNKNOWN',
  name:       'Unknown User',
  department: '—',
  shift:      '—',
  role:       'viewer',
}

/**
 * Resolve the current user from the browser's hostname.
 * navigator.userAgent / window.location.hostname gives us the client hostname
 * in a pure-frontend setup. With an ASP.NET backend this should be a real API call.
 */
export function resolveUserFromComputer() {
  // In a browser we can't read the Windows computer name directly.
  // Options until the backend exists:
  //   1. Read from a query param  (?host=asus)           ← easy for testing
  //   2. Fall back to a hardcoded hostname map
  //   3. Ask the ASP.NET backend to return it server-side
  //
  // Here we use option 1 + 2:
  const params   = new URLSearchParams(window.location.search)
  const fromQS   = params.get('host')?.toLowerCase()
  const hostname = fromQS || window.location.hostname.toLowerCase()   // e.g. "localhost", "asus"

  // Try the full hostname, then just the first segment (e.g. "tcl-pc-001.domain.com" → "tcl-pc-001")
  const shortName = hostname.split('.')[0]

  return (
    computerRegistry[hostname] ||
    computerRegistry[shortName] ||
    unknownUser
  )
}
